using System.CommandLine;
using System.CommandLine.Invocation;
using Ffmt.Cli.Items;
using Ffmt.Cli.Stages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ffmt.Cli.Commands;

internal static class RootCommandBuilder
{
    public static RootCommand Build(IServiceProvider services)
    {
        var root = new RootCommand("ffmt - FFXIV Market Tools CLI");

        root.AddCommand(Simple<UpdatedbOrchestrator>(services,
            "updatedb", "Run every updatedb stage in order.",
            (orchestrator, ct) => orchestrator.RunAllAsync(ct)));

        root.AddCommand(Simple<UpdateWorldsStage>(services,
            "update-worlds", "Refresh the worlds table from the Universalis topology.",
            (stage, ct) => stage.RunAsync(ct)));

        root.AddCommand(Simple(services,
            "update-items", "Reseed the items table from the FFXIV datamining CSV.",
            async (sp, ct) =>
            {
                var rows = await sp.GetRequiredService<ItemCsvSource>().LoadAsync(ct).ConfigureAwait(false);
                await sp.GetRequiredService<UpdateItemsStage>().RunAsync(rows, ct).ConfigureAwait(false);
            }));

        root.AddCommand(Simple(services,
            "update-elastic", "Reindex the items index in Elasticsearch from the FFXIV datamining CSV.",
            async (sp, ct) =>
            {
                var rows = await sp.GetRequiredService<ItemCsvSource>().LoadAsync(ct).ConfigureAwait(false);
                await sp.GetRequiredService<UpdateElasticStage>().RunAsync(rows, ct).ConfigureAwait(false);
            }));

        root.AddCommand(Simple<UpdateGarlandStage>(services,
            "update-garland", "Flip the craftable flag on items with a Garland recipe.",
            (stage, ct) => stage.RunAsync(ct)));

        root.AddCommand(Simple<UpdateMarketabilityStage>(services,
            "update-marketability", "Flip the marketable flag from the Universalis marketable id list.",
            (stage, ct) => stage.RunAsync(ct)));

        root.AddCommand(BuildArchive(services));

        root.AddCommand(WithDryRun<UpdateBaselinesCommand>(services,
            "update-baselines", "Recompute per (item, region, hq) median unit prices used to detect price anomalies.",
            "Log what would be computed without writing anything.",
            (cmd, dryRun, ct) => cmd.RunAsync(dryRun, ct)));

        root.AddCommand(WithDryRun<QuarantineScrubCommand>(services,
            "quarantine-scrub", "Apply the anomaly filter to the existing Scylla sales window and requeue affected gilflux pairs.",
            "Log what would be quarantined without writing or deleting anything.",
            (cmd, dryRun, ct) => cmd.RunAsync(dryRun, ct)));

        return root;
    }

    private static Command BuildArchive(IServiceProvider services)
    {
        // One Option instance spans both commands, as it always has: that is why `archive merge
        // --help` renders the parent's wording, and it keeps `archive --dry-run merge` parsing.
        var dryRunOption = new Option<bool>("--dry-run", "Log what would be exported without writing or deleting anything.");

        var archiveCmd = WithDryRun<ArchiveCommand>(services,
            "archive", "Export completed sale days to Hetzner Object Storage and prune from Scylla.",
            dryRunOption, (cmd, dryRun, ct) => cmd.RunAsync(dryRun, ct));

        archiveCmd.AddCommand(WithDryRun<ArchiveMergeCommand>(services,
            "merge", "Merge all outstanding corrections files into their main archive files.",
            dryRunOption, (cmd, dryRun, ct) => cmd.RunAsync(dryRun, ct)));

        return archiveCmd;
    }

    private static Command Simple<T>(
        IServiceProvider services, string name, string description,
        Func<T, CancellationToken, Task> run) where T : notnull =>
        Simple(services, name, description, (sp, ct) => run(sp.GetRequiredService<T>(), ct));

    private static Command Simple(
        IServiceProvider services, string name, string description,
        Func<IServiceProvider, CancellationToken, Task> run)
    {
        var cmd = new Command(name, description);
        cmd.SetHandler(async (InvocationContext ctx) => await Run(services, ctx, run).ConfigureAwait(false));
        return cmd;
    }

    private static Command WithDryRun<T>(
        IServiceProvider services, string name, string description, string dryRunHelp,
        Func<T, bool, CancellationToken, Task> run) where T : notnull =>
        WithDryRun(services, name, description, new Option<bool>("--dry-run", dryRunHelp), run);

    private static Command WithDryRun<T>(
        IServiceProvider services, string name, string description, Option<bool> dryRunOption,
        Func<T, bool, CancellationToken, Task> run) where T : notnull
    {
        var cmd = new Command(name, description);
        cmd.AddOption(dryRunOption);
        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var dryRun = ctx.ParseResult.GetValueForOption(dryRunOption);
            await Run(services, ctx, (sp, ct) => run(sp.GetRequiredService<T>(), dryRun, ct)).ConfigureAwait(false);
        });
        return cmd;
    }

    private static async Task Run(IServiceProvider services, InvocationContext ctx, Func<IServiceProvider, CancellationToken, Task> action)
    {
        var ct = ctx.GetCancellationToken();
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Ffmt.Cli");
        try
        {
            await action(sp, ct).ConfigureAwait(false);
            ctx.ExitCode = 0;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogWarning("Cancelled.");
            ctx.ExitCode = 130;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Command failed.");
            ctx.ExitCode = 1;
        }
    }
}
