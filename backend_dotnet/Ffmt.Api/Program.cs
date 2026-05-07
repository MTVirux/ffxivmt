using Ffmt.Api;
using Ffmt.Api.Endpoints;
using Ffmt.Core.DI;
using Ffmt.Core.HealthChecks;
using Ffmt.Core.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) =>
    SerilogBootstrap.Configure(logger, context.Configuration, context.HostingEnvironment.EnvironmentName));

builder.Services.AddFfmtCore(builder.Configuration);

builder.Services.AddExceptionHandler<BackendUnavailableExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services
    .AddHealthChecks()
    .AddCheck<ScyllaHealthCheck>("scylla", tags: ["ready", "scylla"])
    .AddCheck<ElasticHealthCheck>("elastic", tags: ["ready", "elastic"]);

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

// Liveness: process is up.
app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false,
});

// Readiness: dependencies (Scylla, Elastic) are reachable.
app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready"),
});

// Compatibility shorthand for /health used during Phase 1; equivalent to /health/live.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapWorldsEndpoints();
app.MapItemEndpoints();

app.Run();

public partial class Program;
