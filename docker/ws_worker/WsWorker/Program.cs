using Ffmt.Core.Configuration;
using Ffmt.Core.External;
using Ffmt.Core.DI;
using Ffmt.Core.Gilflux;
using Ffmt.Core.HealthChecks;
using Ffmt.Core.Logging;
using Ffmt.Core.Metrics;
using Microsoft.Extensions.Options;
using Serilog;
using WsWorker.Health;
using WsWorker.Options;
using WsWorker.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, logger) =>
    SerilogBootstrap.Configure(logger, context.Configuration, context.HostingEnvironment.EnvironmentName));

builder.Services.AddFfmtCore(builder.Configuration);

builder.Services.AddFfmtMetrics();

builder.Services.Configure<BackfillOptions>(builder.Configuration.GetSection("Backfill"));

// Without a retry policy every transient 429/504 from Universalis becomes a permanently
// missing chunk, which is indistinguishable from "this window held no sales".
builder.Services.AddHttpClient("backfill_universalis", (_, client) =>
{
    // SalesBackfillService budgets each request from the window it asks for (BackfillTuning), so
    // this is only a backstop and must stay above BackfillTuning.MaxRequestTimeoutSeconds. A fixed
    // budget here also has to cover every Polly retry below, which is why it is not the real limit.
    client.Timeout = TimeSpan.FromSeconds(360);
})
.AddPolicyHandler((sp, _) =>
{
    var opts = sp.GetRequiredService<IOptions<UniversalisOptions>>().Value;
    return HttpRetryPolicy.Build(opts.MaxRetries, opts.InitialBackoffSeconds, opts.MaxBackoffSeconds);
});

builder.Services.AddSingleton<RankingCoalescer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RankingCoalescer>());

builder.Services.AddHostedService<DeferredSweepWorker>();

builder.Services.AddHostedService<RankingDecaySweepWorker>();

builder.Services.AddSingleton<UniversalisWsConsumer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<UniversalisWsConsumer>());

builder.Services.AddHostedService<SalesBackfillService>();

builder.Services.AddHealthChecks()
    .AddCheck<ScyllaHealthCheck>("scylla")
    .AddCheck<WsConsumerHealthCheck>("ws_consumer");

builder.WebHost.UseUrls("http://0.0.0.0:8080");

var app = builder.Build();

app.MapHealthChecks("/health");

app.MapFfmtMetrics();

app.Run();
