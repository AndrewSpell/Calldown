using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Calldown.AppHost.Firefall;

internal static class Preflight
{
    public static void AddFirefallPreflight(
        this IDistributedApplicationBuilder builder,
        FirefallInstall? install,
        string operatorHost
    )
    {
        builder.Eventing.Subscribe<AfterResourcesCreatedEvent>(
            (e, ct) =>
            {
                var interaction = e.Services.GetRequiredService<IInteractionService>();
                var logger = e
                    .Services.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Calldown.Preflight");

                foreach (var (title, message, intent) in Check(install, operatorHost))
                {
                    logger.Log(
                        intent == MessageIntent.Error ? LogLevel.Error : LogLevel.Warning,
                        "{Title}: {Message}",
                        title,
                        message
                    );

                    if (interaction.IsAvailable)
                    {
                        // Notifications stay until dismissed, so don't block startup on them
                        _ = interaction.PromptNotificationAsync(
                            title,
                            message,
                            new NotificationInteractionOptions { Intent = intent },
                            ct
                        );
                    }
                }

                return Task.CompletedTask;
            }
        );
    }

    private static IEnumerable<(string Title, string Message, MessageIntent Intent)> Check(
        FirefallInstall? install,
        string operatorHost
    )
    {
        if (!DevCertificate.Exists())
        {
            yield return (
                "No development certificate",
                "Kestrel serves RIN's https endpoint with the ASP.NET Core development certificate and there is none in CurrentUser\\My. "
                    + "Run 'dotnet dev-certs https', without it rin-webapi won't come up on 5001. Trusting it is optional and only matters in a browser.",
                MessageIntent.Error
            );
        }

        if (install is null || !install.IsValid)
        {
            yield return (
                "Firefall not found",
                "No Firefall install with system\\db\\clientdb.sd2 was found. Set the firefall-path parameter and restart the AppHost.",
                MessageIntent.Error
            );
            yield break;
        }

        if (RedHandedPatch.Detect(install.ClientExe) == RedHandedState.Unpatched)
        {
            yield return (
                "RedHanded not bypassed",
                $"{install.ClientExe} still runs the RedHanded anti-cheat and won't start. Run 'Configure client' on the firefall resource, it patches the client.",
                MessageIntent.Warning
            );
        }

        if (File.Exists(install.FirefallIni))
        {
            var current = IniFile.GetValue(
                File.ReadAllLines(install.FirefallIni),
                "Config",
                "OperatorHost"
            );
            if (!string.Equals(current, operatorHost, StringComparison.OrdinalIgnoreCase))
            {
                yield return (
                    "Client points elsewhere",
                    $"firefall.ini uses OperatorHost '{current}'. Run 'Configure client' on the firefall resource to use {operatorHost}.",
                    MessageIntent.Information
                );
            }
        }
    }
}
