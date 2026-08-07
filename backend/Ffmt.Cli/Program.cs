using System.CommandLine;
using Ffmt.Cli.Commands;
using Ffmt.Cli.DI;
using Ffmt.Core.DI;
using Ffmt.Core.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

builder.Services.AddFfmtCore(builder.Configuration);
builder.Services.AddFfmtCli();

builder.Services.AddSerilog((services, logger) =>
    SerilogBootstrap.Configure(logger, builder.Configuration, builder.Environment.EnvironmentName));

using var host = builder.Build();
await host.StartAsync();

var rootCommand = RootCommandBuilder.Build(host.Services);
var exitCode = await rootCommand.InvokeAsync(args);

await host.StopAsync();
return exitCode;
