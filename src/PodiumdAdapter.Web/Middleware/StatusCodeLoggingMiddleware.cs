namespace PodiumdAdapter.Web.Middleware;

public class StatusCodeLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<StatusCodeLoggingMiddleware> _logger;

    public StatusCodeLoggingMiddleware(RequestDelegate next, ILogger<StatusCodeLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        _logger.LogDebug("StatusCodeLoggingMiddleware invoked.");

        await _next(context);

        _logger.LogDebug("StatusCodeLoggingMiddleware processing response. Status Code: {StatusCode}", context.Response.StatusCode);

        if (context.Response.StatusCode == 400 ||
            context.Response.StatusCode == 401 ||
            context.Response.StatusCode == 403 ||
            context.Response.StatusCode == 404 ||
            context.Response.StatusCode == 502 ||
            context.Response.StatusCode == 500)
        {
            _logger.LogInformation("HTTP {StatusCode} response: {Path}, TraceIdentifier: {TraceIdentifier}",
                context.Response.StatusCode, context.Request.Path, context.TraceIdentifier);
        }
    }
}
