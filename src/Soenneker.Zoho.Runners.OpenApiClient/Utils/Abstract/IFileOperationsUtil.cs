using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Zoho.Runners.OpenApiClient.Utils.Abstract;

/// <summary>
/// Regenerates, validates, and publishes the Zoho OpenAPI client.
/// </summary>
public interface IFileOperationsUtil
{
    /// <summary>
    /// Runs the client regeneration and publishing workflow.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask Process(CancellationToken cancellationToken = default);
}
