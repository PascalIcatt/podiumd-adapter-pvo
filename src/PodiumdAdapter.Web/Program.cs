using Generated.Esuite.ContactmomentenClient;
using Generated.Esuite.KlantenClient;
using PodiumdAdapter.Web.Auth;
using PodiumdAdapter.Web.Endpoints;
using PodiumdAdapter.Web.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddHealthChecks();
builder.Services.AddHttpClient();
builder.Services.AddTransient<ESuiteRequestAdapter>();
builder.Services.AddTransient(s => new ContactmomentenClient(s.GetEsuiteRequestAdapter("ESUITE_CONTACTMOMENTEN_BASE_URL")));
builder.Services.AddTransient(s => new KlantenClient(s.GetEsuiteRequestAdapter("ESUITE_KLANTEN_BASE_URL")));

if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddAuth(builder.Configuration);
}

var app = builder.Build();
// Configure the HTTP request pipeline.

app.UseSerilogRequestLogging();

app.Map(Contactmomenten.Api);
app.Map(Klanten.Api);

app.MapHealthChecks("/healthz").AllowAnonymous();

app.Run();

public partial class Program { }
