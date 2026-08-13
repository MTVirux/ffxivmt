using Ffmt.Cli.Commands;
using Ffmt.Cli.Items;
using Ffmt.Cli.Stages;
using Microsoft.Extensions.DependencyInjection;

namespace Ffmt.Cli.DI;

public static class FfmtCliServiceCollectionExtensions
{
    public static IServiceCollection AddFfmtCli(this IServiceCollection services)
    {
        // No Polly retry: GitHub raw is reliable, and the loader downloads every source in parallel anyway.
        services.AddHttpClient(ItemCsvSource.HttpClientName, http => http.Timeout = TimeSpan.FromSeconds(60));

        services.AddSingleton<ItemCsvSource>();

        services.AddSingleton<UpdateWorldsStage>();
        services.AddSingleton<UpdateItemsStage>();
        services.AddSingleton<UpdateElasticStage>();
        services.AddSingleton<UpdateGarlandStage>();
        services.AddSingleton<UpdateMarketabilityStage>();
        services.AddSingleton<UpdatedbOrchestrator>();

        services.AddSingleton<ArchiveCommand>();
        services.AddSingleton<ArchiveMergeCommand>();
        services.AddSingleton<UpdateBaselinesCommand>();
        services.AddSingleton<QuarantineScrubCommand>();

        return services;
    }
}
