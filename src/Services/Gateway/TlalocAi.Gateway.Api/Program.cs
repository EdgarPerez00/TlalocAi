using System.Text;
using TlalocAi.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddTlalocServiceDefaults("TlalocAi.Gateway.Api");
builder.Services.AddHttpClient();

var app = builder.Build();
app.UseTlalocServiceDefaults();

app.MapGet("/api/gateway/routes", (IConfiguration configuration) => new
{
    identity = configuration["Services:Identity"],
    devices = configuration["Services:Devices"],
    telemetry = configuration["Services:Telemetry"],
    control = configuration["Services:Control"],
    analytics = configuration["Services:Analytics"]
}).WithTags("Gateway");

app.MapMethods("/api/{**path}", ["GET", "POST", "PUT", "PATCH", "DELETE"], async (
    string path,
    HttpRequest request,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var targetBaseUrl = ResolveTarget(path, configuration);
    if (targetBaseUrl is null)
    {
        return Results.Problem($"No gateway route configured for /api/{path}", statusCode: StatusCodes.Status404NotFound);
    }

    var query = request.QueryString.HasValue ? request.QueryString.Value : string.Empty;
    using var forward = new HttpRequestMessage(new HttpMethod(request.Method), $"{targetBaseUrl.TrimEnd('/')}/api/{path}{query}");

    foreach (var header in request.Headers)
    {
        forward.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    }

    if (request.ContentLength > 0)
    {
        forward.Content = new StreamContent(request.Body);
        if (!string.IsNullOrWhiteSpace(request.ContentType))
        {
            forward.Content.Headers.TryAddWithoutValidation("Content-Type", request.ContentType);
        }
    }

    var response = await httpClientFactory.CreateClient().SendAsync(forward, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
    return Results.Content(Encoding.UTF8.GetString(bytes), response.Content.Headers.ContentType?.ToString(), statusCode: (int)response.StatusCode);
}).WithTags("Gateway");

app.Run();

static string? ResolveTarget(string path, IConfiguration configuration)
{
    if (path.StartsWith("auth", StringComparison.OrdinalIgnoreCase))
    {
        return configuration["Services:Identity"];
    }

    if (path.StartsWith("devices", StringComparison.OrdinalIgnoreCase) && path.Contains("commands", StringComparison.OrdinalIgnoreCase))
    {
        return configuration["Services:Control"];
    }

    if (path.StartsWith("devices", StringComparison.OrdinalIgnoreCase))
    {
        return configuration["Services:Devices"];
    }

    if (path.StartsWith("telemetry", StringComparison.OrdinalIgnoreCase) || path.StartsWith("experiments", StringComparison.OrdinalIgnoreCase))
    {
        return configuration["Services:Telemetry"];
    }

    if (path.StartsWith("commands", StringComparison.OrdinalIgnoreCase))
    {
        return configuration["Services:Control"];
    }

    if (path.StartsWith("analytics", StringComparison.OrdinalIgnoreCase))
    {
        return configuration["Services:Analytics"];
    }

    return null;
}
