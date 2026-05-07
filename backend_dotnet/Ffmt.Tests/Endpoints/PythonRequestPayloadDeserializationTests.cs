using System.Text.Json;
using Ffmt.Api.Endpoints;

namespace Ffmt.Tests.Endpoints;

/// <summary>
/// Locks the JSON binding rules of the Universalis-style payload that the Python sales importer
/// forwards to <c>/api/v1/updatedb/python_request</c>. The serializer options mirror what
/// <c>Ffmt.Api/Program.cs</c> configures (snake_case naming policy + case-insensitive matching).
/// </summary>
public sealed class PythonRequestPayloadDeserializationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void Binds_Universalis_World_History_Payload()
    {
        const string body = """
        {
            "itemID": 12345,
            "worldID": 21,
            "worldName": "Ravana",
            "items": {
                "12345": {
                    "entries": [
                        {
                            "buyerName": "Alice",
                            "hq": 1,
                            "onMannequin": false,
                            "pricePerUnit": 100,
                            "quantity": 5,
                            "timestamp": 1700000000
                        }
                    ]
                }
            }
        }
        """;

        var payload = JsonSerializer.Deserialize<PythonRequestPayload>(body, Options);

        payload.Should().NotBeNull();
        payload!.WorldId.Should().Be(21);
        payload.ItemId.Should().Be(12345);
        payload.Items.Should().NotBeNull().And.HaveCount(1);
        var entries = payload.Items!["12345"].Entries;
        entries.Should().HaveCount(1);
        entries[0].BuyerName.Should().Be("Alice");
        entries[0].Hq.Should().Be(1);
        entries[0].OnMannequin.Should().BeFalse();
        entries[0].PricePerUnit.Should().Be(100);
        entries[0].Quantity.Should().Be(5);
        entries[0].Timestamp.Should().Be(1_700_000_000);
        entries[0].WorldId.Should().BeNull();
    }

    [Fact]
    public void Binds_Universalis_Datacenter_History_Payload_With_Per_Entry_World()
    {
        // DC-scope queries omit the top-level worldID and instead carry per-entry worldId.
        const string body = """
        {
            "items": {
                "12345": {
                    "entries": [
                        { "worldId": 42, "buyerName": "Bob", "hq": 0, "onMannequin": false,
                          "pricePerUnit": 50, "quantity": 2, "timestamp": 1700000123 }
                    ]
                }
            }
        }
        """;

        var payload = JsonSerializer.Deserialize<PythonRequestPayload>(body, Options);

        payload.Should().NotBeNull();
        payload!.WorldId.Should().BeNull();
        payload.Items!["12345"].Entries[0].WorldId.Should().Be(42);
    }

    [Fact]
    public void Empty_Object_Binds_With_All_Properties_Null()
    {
        var payload = JsonSerializer.Deserialize<PythonRequestPayload>("{}", Options);

        payload.Should().NotBeNull();
        payload!.WorldId.Should().BeNull();
        payload.Items.Should().BeNull();
    }

    [Fact]
    public void Unknown_Top_Level_Fields_Are_Captured_Without_Failure()
    {
        const string body = """
        {
            "worldID": 21,
            "stale_legacy_field": "ignored",
            "items": {}
        }
        """;

        var payload = JsonSerializer.Deserialize<PythonRequestPayload>(body, Options);

        payload.Should().NotBeNull();
        payload!.WorldId.Should().Be(21);
        payload.Items.Should().BeEmpty();
    }
}
