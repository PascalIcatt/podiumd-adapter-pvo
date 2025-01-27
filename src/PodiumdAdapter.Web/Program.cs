using PodiumdAdapter.Web.Auth;
using PodiumdAdapter.Web.Endpoints;
using PodiumdAdapter.Web.Endpoints.ObjectenEndpoints;
using PodiumdAdapter.Web.Infrastructure;
using PodiumdAdapter.Web.Infrastructure.UrlRewriter;
using PodiumdAdapter.Web.Middleware;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

var logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore.Mvc", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", LogEventLevel.Warning)
    .WriteTo.Console(new JsonFormatter())
    .CreateLogger();

try
{
    logger.Write(LogEventLevel.Information, "Starting web application");

    var builder = WebApplication.CreateBuilder(args);

    // Services toevoegen aan de container.
    builder.Host.UseSerilog(logger);

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddHealthChecks();
    builder.Services.AddReverseProxy().AddEsuiteToken().AddEsuiteHeader();
    builder.Services.AddESuiteClient(new ContactmomentenClientConfig());
    builder.Services.AddESuiteClient(new KlantenClientConfig());
    builder.Services.AddESuiteClient(new ZaakZrcClientConfig());
    builder.Services.AddESuiteClient(new CatalogusZtcClientConfig());
    builder.Services.AddESuiteClient(new DocumentenDrcClientConfig());
    builder.Services.AddSmoelenboekClient(builder.Configuration);
    builder.Services.AddUrlRewriter(EsuiteUrlRewriteMaps.GetRewriters);

    // Deze regel uitcommentariëren als je PodiumdAdapter.Web.http wilt gebruiken
    builder.Services.AddAuth(builder.Configuration);

    var app = builder.Build();

    // Configureer de HTTP request pipeline.
    app.UseSerilogRequestLogging(x=> x.Logger = logger);
    app.UseRouting(); // Zorg ervoor dat routing is ingesteld voordat middleware wordt gebruikt die afhankelijk is van route-informatie
    app.UseMiddleware<StatusCodeLoggingMiddleware>(); // Middleware om HTTP-statuscodes toe te voegen aan Serilog JSON-logs, aangezien deze standaard niet zijn opgenomen
    app.UseAuthentication(); // Zorg ervoor dat de authenticatiemiddleware voor de autorisatie wordt aangeroepen
    app.UseAuthorization();
    app.UseUrlRewriter();

    // Top-level route registrations
    app.MapHealthChecks("/healthz").AllowAnonymous();
    app.MapEsuiteEndpoints();
    app.MapObjectenEndpoints();
    app.MapReverseProxy();

    // Configure logger for ObjectenEndpoints
    ObjectenEndpoints.InitializeLogger(app.Services.GetRequiredService<ILoggerFactory>());

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    logger.Write(LogEventLevel.Fatal, ex, "Application terminated unexpectedly");
}

public partial class Program { }
