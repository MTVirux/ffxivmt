using System.Net;
using Ffmt.Core.External;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ffmt.Tests.External;

public sealed class XivapiClientTests
{
    private sealed class QueueHandler(params string[] bodies) : HttpMessageHandler
    {
        private readonly Queue<string> _bodies = new(bodies);

        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            var body = _bodies.Count > 0 ? _bodies.Dequeue() : """{"rows":[],"results":[]}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private static (XivapiClient Client, QueueHandler Handler) Create(params string[] bodies)
    {
        var handler = new QueueHandler(bodies);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://v2.xivapi.com/api/") };
        return (new XivapiClient(http, NullLogger<XivapiClient>.Instance), handler);
    }

    [Fact]
    public async Task Candidates_follow_the_cursor_and_resend_fields()
    {
        var (client, handler) = Create(
            """{"next":"abc","results":[{"row_id":1214,"fields":{"ItemResult":{"value":1670,"row_id":1670,"fields":{"EquipSlotCategory@as(raw)":1,"ItemUICategory@as(raw)":2,"LevelItem@as(raw)":55,"Rarity":2}}}}]}""",
            """{"results":[{"row_id":1300,"fields":{"ItemResult":{"value":2339,"row_id":2339,"fields":{"EquipSlotCategory@as(raw)":2,"ItemUICategory@as(raw)":13,"LevelItem@as(raw)":55,"Rarity":2}}}}]}""");

        var candidates = await client.GetEquipmentCandidatesAsync();

        candidates.Should().Equal(
            new XivapiEquipmentCandidate(1670, 2, 55, 1, 2),
            new XivapiEquipmentCandidate(2339, 2, 55, 2, 13));
        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].AbsolutePath.Should().Be("/api/search");
        Uri.UnescapeDataString(handler.Requests[0].Query).Should()
            .Contain("sheets=Recipe")
            .And.Contain("query=+ItemResult.Rarity>=2 +ItemResult.PriceLow>0 +ItemResult.EquipSlotCategory>0");
        Uri.UnescapeDataString(handler.Requests[1].Query).Should()
            .Contain("cursor=abc")
            .And.Contain("fields=ItemResult.Rarity,ItemResult.LevelItem@as(raw)");
    }

    [Fact]
    public async Task Recipes_page_by_last_row_id_and_drop_empty_slots()
    {
        var (client, handler) = Create(
            """{"rows":[{"row_id":43,"fields":{"AmountIngredient":[3,0,0,0,0,0,1,0],"AmountResult":1,"Ingredient@as(raw)":[5111,0,0,0,0,0,2,-1],"ItemResult@as(raw)":5057}},{"row_id":44,"fields":{"AmountIngredient":[0,0,0,0,0,0,0,0],"AmountResult":0,"Ingredient@as(raw)":[0,0,0,0,0,0,0,0],"ItemResult@as(raw)":0}}]}""",
            """{"rows":[{"row_id":900,"fields":{"AmountIngredient":[2,0,0,0,0,0,0,0],"AmountResult":3,"Ingredient@as(raw)":[5057,0,0,0,0,0,0,0],"ItemResult@as(raw)":5100}}]}""",
            """{"rows":[]}""");

        var recipes = await client.GetAllRecipesAsync();

        recipes.Should().BeEquivalentTo(
            new[]
            {
                new XivapiRecipe(5057, 1, [new XivapiIngredient(5111, 3), new XivapiIngredient(2, 1)]),
                new XivapiRecipe(5100, 3, [new XivapiIngredient(5057, 2)]),
            },
            o => o.WithStrictOrdering());
        handler.Requests.Select(r => Uri.UnescapeDataString(r.Query)).Should().SatisfyRespectively(
            q => q.Should().NotContain("after="),
            q => q.Should().Contain("after=44"),
            q => q.Should().Contain("after=900"));
    }

    [Fact]
    public async Task Seals_are_keyed_by_item_level_and_skip_zero_rows()
    {
        var (client, _) = Create(
            """{"rows":[{"row_id":0,"fields":{"SealsExpertDelivery":0}},{"row_id":1,"fields":{"SealsExpertDelivery":6}},{"row_id":770,"fields":{"SealsExpertDelivery":2109}}]}""",
            """{"rows":[]}""");

        var seals = await client.GetExpertDeliverySealsAsync();

        seals.Should().BeEquivalentTo(new Dictionary<int, int> { [1] = 6, [770] = 2109 });
    }

    [Fact]
    public async Task A_page_without_row_ids_stops_the_walk()
    {
        var (client, handler) = Create("""{"rows":[{"fields":{"SealsExpertDelivery":5}}]}""");

        var seals = await client.GetExpertDeliverySealsAsync();

        seals.Should().BeEmpty();
        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task A_repeated_cursor_stops_the_search()
    {
        const string page = """{"next":"abc","results":[{"row_id":1214,"fields":{"ItemResult":{"row_id":1670,"fields":{"Rarity":2}}}}]}""";
        var (client, handler) = Create(page, page, page);

        var candidates = await client.GetEquipmentCandidatesAsync();

        candidates.Should().HaveCount(2);
        handler.Requests.Should().HaveCount(2);
    }

    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("""{"rows":[]}""") });
    }

    [Fact]
    public async Task A_non_success_response_throws()
    {
        var http = new HttpClient(new StatusHandler(HttpStatusCode.InternalServerError)) { BaseAddress = new Uri("https://v2.xivapi.com/api/") };
        var client = new XivapiClient(http, NullLogger<XivapiClient>.Instance);

        var act = () => client.GetAllRecipesAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
