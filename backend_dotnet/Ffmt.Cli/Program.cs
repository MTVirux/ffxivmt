using Ffmt.Core.DI;
using Ffmt.Core.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

// Phase-1 placeholder. System.CommandLine wiring + the `updatedb` subcommands land in Phase 4.
var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

builder.Services.AddFfmtCore(builder.Configuration);

builder.Services.AddSerilog((services, logger) =>
    SerilogBootstrap.Configure(logger, builder.Configuration, builder.Environment.EnvironmentName));

using var host = builder.Build();
await host.StartAsync();

Console.WriteLine("ffmt CLI: Phase 1 placeholder. Subcommands land in Phase 4.");
Console.WriteLine($"args: [{string.Join(", ", args)}]");

await host.StopAsync();
return 0;
