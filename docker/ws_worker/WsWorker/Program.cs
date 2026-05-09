using Ffmt.Core.DI;
using Ffmt.Core.Gilflux;
using Ffmt.Core.HealthChecks;
using Serilog;
using WsWorker.Health;
using WsWorker.Options;
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

builder.Services.AddFfmtCore(builder.Configuration);

builder.Services.Configure<BackfillOptions>(builder.Configuration.GetSection("Backfill"));

builder.Services.AddHttpClient("backfill_universalis", (_, client) =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddSingleton<RankingCoalescer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RankingCoalescer>());

builder.Services.AddHostedService<DeferredSweepWorker>();

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
