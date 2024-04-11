using PodiumdAdapter.Web.Auth;
using PodiumdAdapter.Web.Endpoints;
using PodiumdAdapter.Web.Endpoints.ObjectenEndpoints;
using PodiumdAdapter.Web.Infrastructure;
using PodiumdAdapter.Web.Infrastructure.UrlRewriter;
using Serilog;
using Serilog.Events;

using var logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .WriteTo.Console()
    .CreateLogger();

try
{
    logger.Write(LogEventLevel.Information, "Starting web application");

    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.
    builder.Host.UseSerilog(logger);

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddHealthChecks();
    builder.Services.AddReverseProxy().AddEsuiteToken().AddEsuiteHeader();
    builder.Services.AddESuiteClient(new ContactmomentenClientConfig());
    builder.Services.AddESuiteClient(new KlantenClientConfig());
    builder.Services.AddESuiteClient(new ZaakZrcClientConfig());
    builder.Services.AddESuiteClient(new CatalogusZtcClientConfig());
    builder.Services.AddESuiteClient(new DocumentenDrcClientConfig());
    builder.Services.AddAfdelingenClient(builder.Configuration);
    builder.Services.AddGroepenClient(builder.Configuration);
    builder.Services.AddSmoelenboekClient(builder.Configuration);
    builder.Services.AddUrlRewriter(EsuiteUrlRewriteMaps.GetRewriters);

    //comment this line if you want to use PodiumdAdapter.Web.http
    builder.Services.AddAuth(builder.Configuration);


    var app = builder.Build();
    // Configure the HTTP request pipeline.
    app.UseSerilogRequestLogging();
    app.UseUrlRewriter();

    app.MapHealthChecks("/healthz").AllowAnonymous();
    app.MapEsuiteEndpoints();
    app.MapObjectenEndpoints();
    app.MapReverseProxy();


    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    logger.Write(LogEventLevel.Fatal, ex, "Application terminated unexpectedly");
}

public partial class Program { }
