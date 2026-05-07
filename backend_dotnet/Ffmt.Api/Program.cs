using Ffmt.Core.DI;
using Ffmt.Core.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) =>
    SerilogBootstrap.Configure(logger, context.Configuration, context.HostingEnvironment.EnvironmentName));

builder.Services.AddFfmtCore(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSerilogRequestLogging();

// Liveness: process is up.
app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false,
});

// Readiness: dependencies (Scylla, Elastic) are reachable. Filled in by later phases.
app.MapHealthChecks("/health/ready");

// Compatibility shorthand for /health used during Phase 1; equivalent to /health/live.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
