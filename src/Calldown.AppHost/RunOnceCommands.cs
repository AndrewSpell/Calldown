using Microsoft.Extensions.DependencyInjection;

namespace Calldown.AppHost;

internal static class RunOnceCommands
{
    public static IResourceBuilder<ProjectResource> WithDbSetupCommands(
        this IResourceBuilder<ProjectResource> dbSetup
    )
    {
        var nextRun = dbSetup.WithNextRunFlag(flag => flag);

        dbSetup.WithCommand(
            "reimport-sdb",
            "Re-import SDB",
            ctx => nextRun.StartWith(ctx, "--force-sdb"),
            new CommandOptions
            {
                Description = "Drops the sdb schema and imports clientdb.sd2 again",
                IconName = "ArrowSync",
                UpdateState = OnlyWhenStopped,
            }
        );

        dbSetup.WithCommand(
            "reset-database",
            "Reset database",
            ctx => nextRun.StartWith(ctx, "--reset"),
            new CommandOptions
            {
                Description =
                    "Drops the webapi, sdb and calldown schemas and sets everything up from scratch",
                ConfirmationMessage =
                    "This deletes all accounts and characters. Restart rin-webapi and rin-internal afterwards. Continue?",
                IconName = "DatabaseWarning",
                UpdateState = OnlyWhenStopped,
            }
        );

        return dbSetup;
    }

    public static IResourceBuilder<ProjectResource> WithCollisionCommands(
        this IResourceBuilder<ProjectResource> collisionCache
    )
    {
        // Normal runs skip an up-to-date cache, a rebuild leaves the flag out
        var nextRun = collisionCache.WithNextRunFlag(flag =>
            flag is null ? "--skip-if-current" : null
        );

        collisionCache.WithCommand(
            "rebuild-collision",
            "Rebuild collision cache",
            ctx => nextRun.StartWith(ctx, "--rebuild"),
            new CommandOptions
            {
                Description =
                    "Regenerates the collision cache even if it looks current. Restart pin-gameserver afterwards to load it.",
                IconName = "ArrowSync",
                UpdateState = OnlyWhenStopped,
            }
        );

        return collisionCache;
    }

    private static NextRun WithNextRunFlag(
        this IResourceBuilder<ProjectResource> resource,
        Func<string?, string?> toArg
    )
    {
        var nextRun = new NextRun(resource.Resource);

        // The flag only applies to the next run, a plain restart from the dashboard runs the resource normally
        resource.WithArgs(ctx =>
        {
            if (toArg(nextRun.Take()) is { } arg)
            {
                ctx.Args.Add(arg);
            }
        });

        return nextRun;
    }

    private static ResourceCommandState OnlyWhenStopped(UpdateCommandStateContext ctx)
    {
        var state = ctx.ResourceSnapshot.State?.Text;
        return
            state == KnownResourceStates.Finished
            || state == KnownResourceStates.Exited
            || state == KnownResourceStates.FailedToStart
            ? ResourceCommandState.Enabled
            : ResourceCommandState.Disabled;
    }

    private sealed class NextRun(IResource resource)
    {
        private string? _flag;

        public string? Take() => Interlocked.Exchange(ref _flag, null);

        public Task<ExecuteCommandResult> StartWith(ExecuteCommandContext ctx, string flag)
        {
            _flag = flag;
            var commands = ctx.Services.GetRequiredService<ResourceCommandService>();
            return commands.ExecuteCommandAsync(
                resource,
                KnownResourceCommands.StartCommand,
                ctx.CancellationToken
            );
        }
    }
}
