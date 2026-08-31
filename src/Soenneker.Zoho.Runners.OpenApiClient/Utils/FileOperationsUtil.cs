using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Soenneker.Extensions.String;
using Soenneker.Extensions.ValueTask;
using Soenneker.Kiota.Util.Abstract;
using Soenneker.Git.Util.Abstract;
using Soenneker.OpenApi.Fixer.Abstract;
using Soenneker.OpenApi.Merger.Abstract;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.Dotnet.Abstract;
using Soenneker.Utils.Environment;
using Soenneker.Utils.File.Abstract;
using Soenneker.Zoho.Runners.OpenApiClient.Utils.Abstract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Zoho.Runners.OpenApiClient.Utils;

public sealed class FileOperationsUtil : IFileOperationsUtil
{
    private readonly ILogger<FileOperationsUtil> _logger;
    private readonly IGitUtil _gitUtil;
    private readonly IDotnetUtil _dotnetUtil;
    private readonly IKiotaUtil _kiotaUtil;
    private readonly IFileUtil _fileUtil;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly IOpenApiMerger _openApiMerger;
    private readonly IOpenApiFixer _openApiFixer;

    public FileOperationsUtil(ILogger<FileOperationsUtil> logger, IGitUtil gitUtil, IDotnetUtil dotnetUtil, IFileUtil fileUtil,
        IDirectoryUtil directoryUtil, IOpenApiMerger openApiMerger, IKiotaUtil kiotaUtil, IOpenApiFixer openApiFixer)
    {
        _logger = logger;
        _gitUtil = gitUtil;
        _dotnetUtil = dotnetUtil;
        _kiotaUtil = kiotaUtil;
        _fileUtil = fileUtil;
        _directoryUtil = directoryUtil;
        _openApiMerger = openApiMerger;
        _openApiFixer = openApiFixer;
    }

    public async ValueTask Process(CancellationToken cancellationToken = default)
    {
        string gitHubToken = EnvironmentUtil.GetVariableStrict("GH__TOKEN");
        string name = EnvironmentUtil.GetVariableStrict("GIT__NAME");
        string email = EnvironmentUtil.GetVariableStrict("GIT__EMAIL");

        string gitDirectory = await _gitUtil.CloneToTempDirectory($"https://github.com/soenneker/{Constants.Library.ToLowerInvariantFast()}",
            cancellationToken: cancellationToken);

        string targetFilePath = Path.Combine(gitDirectory, "openapi.json");

        await _fileUtil.DeleteIfExists(targetFilePath, cancellationToken: cancellationToken);

        OpenApiDocument merged = await _openApiMerger.MergeGitUrl("https://github.com/zoho/crm-oas", "v8.0", cancellationToken);

        string json = _openApiMerger.ToJson(merged);

        await _fileUtil.Write(targetFilePath, json, true, cancellationToken);

        string fixedFilePath = Path.Combine(gitDirectory, "fixed.json");

        await _openApiFixer.Fix(targetFilePath, fixedFilePath, cancellationToken);

        await _kiotaUtil.EnsureInstalled(cancellationToken);

        string srcDirectory = Path.Combine(gitDirectory, "src", Constants.Library);

        await DeleteAllExceptCsproj(srcDirectory, cancellationToken);

        await _kiotaUtil.Generate(fixedFilePath, "ZohoOpenApiClient", Constants.Library, gitDirectory, cancellationToken).NoSync();

        await BuildAndPush(gitDirectory, gitHubToken, name, email, cancellationToken)
            .NoSync();
    }

    /// <summary>
    /// Deletes all except csproj.
    /// </summary>
    /// <param name="directoryPath">The directory path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask DeleteAllExceptCsproj(string directoryPath, CancellationToken cancellationToken = default)
    {
        if (!(await _directoryUtil.Exists(directoryPath, cancellationToken)))
            throw new DirectoryNotFoundException($"Generated source directory does not exist: {directoryPath}");

        List<string> files = await _directoryUtil.GetFilesByExtension(directoryPath, "", true, cancellationToken);
        foreach (string file in files)
        {
            if (!file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                await _fileUtil.Delete(file, ignoreMissing: true, log: false, cancellationToken);
                _logger.LogInformation("Deleted file: {FilePath}", file);
            }
        }

        List<string> dirs = await _directoryUtil.GetAllDirectoriesRecursively(directoryPath, cancellationToken);
        foreach (string dir in dirs.OrderByDescending(d => d.Length))
        {
            List<string> dirFiles = await _directoryUtil.GetFilesByExtension(dir, "", false, cancellationToken);
            List<string> subDirs = await _directoryUtil.GetAllDirectories(dir, cancellationToken);
            if (dirFiles.Count == 0 && subDirs.Count == 0)
            {
                await _directoryUtil.Delete(dir, cancellationToken);
                _logger.LogInformation("Deleted empty directory: {DirectoryPath}", dir);
            }
        }
    }

    private async ValueTask BuildAndPush(string gitDirectory, string gitHubToken, string name, string email, CancellationToken cancellationToken)
    {
        string projFilePath = Path.Combine(gitDirectory, "src", Constants.Library, $"{Constants.Library}.csproj");

        await _dotnetUtil.Restore(projFilePath, cancellationToken: cancellationToken);

        bool successful = await _dotnetUtil.Build(projFilePath, true, "Release", false, cancellationToken: cancellationToken);

        if (!successful)
            throw new InvalidOperationException("The generated Zoho OpenAPI client did not build successfully.");

        await _gitUtil.CommitAndPush(gitDirectory, "Automated update", gitHubToken, name, email, cancellationToken);
    }
}
