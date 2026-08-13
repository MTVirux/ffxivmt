using System.Text;
using System.Text.Json;
using Ffmt.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Ffmt.Tests.Endpoints;

/// <summary>Renders through the same serializer options Program.cs installs, so the shared envelope
/// stays byte-identical to the hand-written ones it replaced.</summary>
public sealed class ApiResultsTests : IDisposable
{
    private readonly ServiceProvider _services;

    public ApiResultsTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
        });
        _services = services.BuildServiceProvider();
    }

    public void Dispose() => _services.Dispose();

    private sealed record Row(int ItemId, string WorldName);

    private async Task<(int StatusCode, string Body)> RenderAsync(IResult result)
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = _services };
        context.Response.Body = body;

        await result.ExecuteAsync(context);

        return (context.Response.StatusCode, Encoding.UTF8.GetString(body.ToArray()));
    }

    [Fact]
    public async Task Fail_emits_the_documented_failure_envelope()
    {
        var rendered = await RenderAsync(
            ApiResults.Fail("No worlds found", StatusCodes.Status400BadRequest));

        rendered.StatusCode.Should().Be(400);
        rendered.Body.Should().Be("""{"status":false,"message":"No worlds found"}""");
    }

    [Fact]
    public async Task Fail_matches_the_literal_call_site_it_replaced()
    {
        var replaced = await RenderAsync(
            ApiResults.Fail("Item not found", StatusCodes.Status404NotFound));

        var original = await RenderAsync(Results.Json(
            new { status = false, message = "Item not found" },
            statusCode: StatusCodes.Status404NotFound));

        replaced.Should().Be(original);
    }

    [Fact]
    public async Task Ok_matches_the_literal_call_site_it_replaced()
    {
        var data = new[] { new Row(4, "Ravana") };

        var replaced = await RenderAsync(
            ApiResults.Ok("Sales retrieved successfully", data));

        var original = await RenderAsync(Results.Ok(new
        {
            status = true,
            message = "Sales retrieved successfully",
            data,
        }));

        replaced.Should().Be(original);
        replaced.StatusCode.Should().Be(200);
        replaced.Body.Should().Be(
            """{"status":true,"message":"Sales retrieved successfully","data":[{"item_id":4,"world_name":"Ravana"}]}""");
    }
}
