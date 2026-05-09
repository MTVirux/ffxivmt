using System.CommandLine;
using Ffmt.Cli.Commands;
using Ffmt.Cli.Items;
using Ffmt.Cli.Stages;
using Ffmt.Core.DI;
using Ffmt.Core.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

builder.Services.AddFfmtCore(builder.Configuration);

builder.Services.AddSerilog((services, logger) =>
    SerilogBootstrap.Configure(logger, builder.Configuration, builder.Environment.EnvironmentName));

// CLI-only HTTP client for the datamining CSV mirrors (no Polly retry — GitHub raw is reliable
// enough that a single attempt with a generous timeout is fine; the parser races two URLs anyway).
builder.Services.AddHttpClient(ItemCsvSource.HttpClientName, http => http.Timeout = TimeSpan.FromSeconds(60));

builder.Services.AddSingleton<ItemCsvSource>();
builder.Services.AddSingleton<UpdateWorldsStage>();
builder.Services.AddSingleton<UpdateItemsStage>();
builder.Services.AddSingleton<UpdateElasticStage>();
builder.Services.AddSingleton<UpdateGarlandStage>();
builder.Services.AddSingleton<UpdateMarketabilityStage>();
builder.Services.AddSingleton<UpdatedbOrchestrator>();

using var host = builder.Build();
await host.StartAsync();

var rootCommand = RootCommandBuilder.Build(host.Services);
var exitCode = await rootCommand.InvokeAsync(args);

await host.StopAsync();
return exitCode;
