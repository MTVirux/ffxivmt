using Microsoft.Extensions.Options;
using Serilog;
using WsWorker.Health;
using WsWorker.Options;
using WsWorker.Services;
using WsWorker.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, logger) =>
    logger
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: "logs/wsworker-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            fileSizeLimitBytes: 100 * 1024 * 1024));

builder.Services.Configure<ScyllaOptions>(builder.Configuration.GetSection("Scylla"));
builder.Services.Configure<UniversalisOptions>(builder.Configuration.GetSection("Universalis"));
builder.Services.Configure<GilfluxOptions>(builder.Configuration.GetSection("Gilflux"));
builder.Services.Configure<BackfillOptions>(builder.Configuration.GetSection("Backfill"));
builder.Services.Configure<BackendOptions>(builder.Configuration.GetSection("Backend"));

builder.Services.AddHttpClient("gilflux", (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<GilfluxOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(opts.HttpTimeoutSeconds);
});

builder.Services.AddHttpClient("backfill_gilflux", (_, client) =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient("backfill_universalis", (_, client) =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddSingleton<ScyllaService>();
builder.Services.AddSingleton<WorldDataCache>();

// GilfluxCoalescer implements IHostedService — register as singleton so it can be
// resolved by name, then add as hosted service using the existing instance.
builder.Services.AddSingleton<GilfluxCoalescer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GilfluxCoalescer>());

// UniversalisWsConsumer — same pattern so WsConsumerHealthCheck can resolve it.
builder.Services.AddSingleton<UniversalisWsConsumer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<UniversalisWsConsumer>());

builder.Services.AddHostedService<SalesBackfillService>();

builder.Services.AddHealthChecks()
    .AddCheck<ScyllaHealthCheck>("scylla")
    .AddCheck<WsConsumerHealthCheck>("ws_consumer");

builder.WebHost.UseUrls("http://0.0.0.0:8080");

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
