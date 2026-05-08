using Microsoft.Extensions.Options;
using Serilog;
using WsWorker.Options;

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

// TODO: builder.Services.AddSingleton<ScyllaService>();
// TODO: builder.Services.AddSingleton<WorldDataCache>();
// TODO: builder.Services.AddSingleton<GilfluxCoalescer>();
// TODO: builder.Services.AddHostedService<UniversalisWsConsumer>();
// TODO: builder.Services.AddHostedService<SalesBackfillService>();
// TODO: health checks for Scylla and WS consumer

builder.Services.AddHealthChecks();

builder.WebHost.UseUrls("http://0.0.0.0:8080");

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
