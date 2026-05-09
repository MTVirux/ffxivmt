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
        var root = new RootCommand("ffmt - FFXIV Market Tools CLI (replaces the legacy PHP Updatedb controller)");

        root.AddCommand(BuildUpdatedb(services));
        root.AddCommand(BuildUpdateWorlds(services));
        root.AddCommand(BuildUpdateItems(services));
        root.AddCommand(BuildUpdateElastic(services));
        root.AddCommand(BuildUpdateGarland(services));
        root.AddCommand(BuildUpdateMarketability(services));

        return root;
    }

    private static Command BuildUpdatedb(IServiceProvider services)
    {
        var cmd = new Command("updatedb", "Run every updatedb stage in order (PHP Updatedb::index equivalent).");
        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            await Run(services, ctx, async (sp, ct) =>
                await sp.GetRequiredService<UpdatedbOrchestrator>().RunAllAsync(ct).ConfigureAwait(false));
        });
        return cmd;
    }

    private static Command BuildUpdateWorlds(IServiceProvider services)
    {
        var cmd = new Command("update-worlds", "Refresh the worlds table from the Universalis topology.");
        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            await Run(services, ctx, async (sp, ct) =>
                await sp.GetRequiredService<UpdateWorldsStage>().RunAsync(ct).ConfigureAwait(false));
        });
        return cmd;
    }

    private static Command BuildUpdateItems(IServiceProvider services)
    {
        var cmd = new Command("update-items", "Reseed the items table from the FFXIV datamining CSV.");
        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            await Run(services, ctx, async (sp, ct) =>
            {
                var rows = await sp.GetRequiredService<ItemCsvSource>().LoadAsync(ct).ConfigureAwait(false);
                await sp.GetRequiredService<UpdateItemsStage>().RunAsync(rows, ct).ConfigureAwait(false);
            });
        });
        return cmd;
    }

    private static Command BuildUpdateElastic(IServiceProvider services)
    {
        var cmd = new Command("update-elastic", "Reindex the items index in Elasticsearch from the FFXIV datamining CSV.");
        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            await Run(services, ctx, async (sp, ct) =>
            {
                var rows = await sp.GetRequiredService<ItemCsvSource>().LoadAsync(ct).ConfigureAwait(false);
                await sp.GetRequiredService<UpdateElasticStage>().RunAsync(rows, ct).ConfigureAwait(false);
            });
        });
        return cmd;
    }

    private static Command BuildUpdateGarland(IServiceProvider services)
    {
        var cmd = new Command("update-garland", "Flip the craftable flag on items with a Garland recipe.");
        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            await Run(services, ctx, async (sp, ct) =>
                await sp.GetRequiredService<UpdateGarlandStage>().RunAsync(ct).ConfigureAwait(false));
        });
        return cmd;
    }

    private static Command BuildUpdateMarketability(IServiceProvider services)
    {
        var cmd = new Command("update-marketability", "Flip the marketable flag from the Universalis marketable id list.");
        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            await Run(services, ctx, async (sp, ct) =>
                await sp.GetRequiredService<UpdateMarketabilityStage>().RunAsync(ct).ConfigureAwait(false));
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
