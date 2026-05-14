using System.Text.Json;
using Ffmt.Api;
using Ffmt.Api.Endpoints;
using Ffmt.Core.DI;
using Ffmt.Core.Metrics;
using Ffmt.Core.HealthChecks;
using Ffmt.Core.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Serilog;

// Keep enough threads warm to prevent starvation under Scylla driver I/O bursts.
var minThreads = int.TryParse(Environment.GetEnvironmentVariable("FFMT_MIN_THREADPOOL_SIZE"), out var n)
    ? n
    : Environment.ProcessorCount * 4;
ThreadPool.SetMinThreads(minThreads, minThreads);

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) =>
    SerilogBootstrap.Configure(logger, context.Configuration, context.HostingEnvironment.EnvironmentName));

builder.Services.AddFfmtCore(builder.Configuration);
builder.Services.AddFfmtMetrics();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // C# property names serialize as snake_case (item_id, world_name, ...) so the
    // legacy PHP API consumers see byte-compatible field names. Dictionary keys are deliberately
    // left as-is so /api/v1/worlds keeps preserving original region/datacenter casing.
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;

    // Inbound payloads from Universalis (and the Python importer that forwards them) mix
    // `worldID` and `worldId` casings depending on the upstream endpoint hit. Match
    // case-insensitively so both bind to the same C# property.
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddExceptionHandler<BackendUnavailableExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddRequestTimeouts();

// `/openapi/v1.json` is always-on (public read-only API, no secret schema); UI is dev-only.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "FFMT API",
        Version = "v1",
        Description = "Public read-only API for FFXIV Market Tools (mtvirux.app).",
    });
});

builder.Services
    .AddHealthChecks()
    .AddCheck<ScyllaHealthCheck>("scylla", tags: ["ready", "scylla"])
    .AddCheck<ElasticHealthCheck>("elastic", tags: ["ready", "elastic"]);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseFfmtHttpMetrics();
app.UseRequestTimeouts();

app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "FFMT API v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false,
});

app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapFfmtMetrics();

app.MapWorldsEndpoints();
app.MapItemEndpoints();
app.MapGilfluxEndpoints();
app.MapUpdatedbEndpoints();
app.MapSearchBuyerEndpoints();
app.MapToolsEndpoints();
app.MapStatusEndpoints();
app.MapConfigEndpoints();

app.Run();

public partial class Program;
