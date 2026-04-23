using Soenneker.Tests.HostedUnit;

namespace Soenneker.Zoho.Runners.OpenApiClient.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class ZohoOpenApiClientRunnerTests : HostedUnitTest
{
    public ZohoOpenApiClientRunnerTests(Host host) : base(host)
    {
    }

    [Test]
    public void Default()
    {

    }
}
