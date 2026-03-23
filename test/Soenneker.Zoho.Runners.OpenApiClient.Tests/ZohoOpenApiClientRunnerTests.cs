using Soenneker.Tests.FixturedUnit;
using Xunit;

namespace Soenneker.Zoho.Runners.OpenApiClient.Tests;

[Collection("Collection")]
public sealed class ZohoOpenApiClientRunnerTests : FixturedUnitTest
{
    public ZohoOpenApiClientRunnerTests(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    [Fact]
    public void Default()
    {

    }
}
