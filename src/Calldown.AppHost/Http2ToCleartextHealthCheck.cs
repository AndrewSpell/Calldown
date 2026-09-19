using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Calldown.AppHost;

internal sealed class Http2ToCleartextHealthCheck(EndpointReference endpoint, string path)
    : IHealthCheck
{
    private static readonly HttpClient Http = new();

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri(new Uri(endpoint.Url), path)
            );
            request.Version = System.Net.HttpVersion.Version20;
            request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;

            using var response = await Http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"{path} answered {(int)response.StatusCode}");
        }
        catch (Exception e)
        {
            return HealthCheckResult.Unhealthy($"{path} isn't reachable", e);
        }
    }
}

internal static class Http2ToCleartextHealthCheckExtensions
{
    public static IResourceBuilder<ProjectResource> WithHttp2ToCleartextHealthCheck(
        this IResourceBuilder<ProjectResource> resource,
        string endpointName,
        string path
    )
    {
        var name = $"{resource.Resource.Name}_h2c_check";
        var endpoint = resource.GetEndpoint(endpointName);

        resource
            .ApplicationBuilder.Services.AddHealthChecks()
            .AddCheck(name, new Http2ToCleartextHealthCheck(endpoint, path));
        return resource.WithHealthCheck(name);
    }
}
