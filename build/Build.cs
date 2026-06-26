using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Serilog;
using static Nuke.Common.Tooling.ProcessTasks;

namespace _build;

[GitHubActions(
    "ci",
    GitHubActionsImage.UbuntuLatest,
    AutoGenerate = true,
    FetchDepth = 0,
    OnPushBranches = new[] { "master" },
    OnPullRequestBranches = new[] { "master" },
    OnPushIncludePaths = new[] { "**" },
    OnPullRequestIncludePaths = new[] { "**" },
    OnWorkflowDispatchOptionalInputs = new[] { "reason" },
    OnPushExcludePaths = new[] { "README.md", "docs/**" },
    OnPullRequestExcludePaths = new[] { "README.md", "docs/**" },
    InvokedTargets = new[] { nameof(Pipeline) },
    ImportSecrets = new[] { nameof(CodecovToken), nameof(ReleaseTagToken) },
    EnableGitHubToken = true,
    WritePermissions = new[] { GitHubActionsPermissions.Contents },
    PublishArtifacts = true,
    CacheKeyFiles = new[] { "global.json", "Directory.Packages.props", "**/*.csproj" })]
[GitHubActions(
    "release",
    GitHubActionsImage.UbuntuLatest,
    AutoGenerate = true,
    FetchDepth = 0,
    OnPushTags = new[] { "v*" },
    OnWorkflowDispatchOptionalInputs = new[] { "reason" },
    InvokedTargets = new[] { nameof(Release) },
    EnableGitHubToken = true,
    WritePermissions = new[] { GitHubActionsPermissions.Contents, GitHubActionsPermissions.IdToken },
    ReadPermissions = new[] { GitHubActionsPermissions.Packages },
    CacheKeyFiles = new[] { "global.json", "Directory.Packages.props", "**/*.csproj" })]
class Build : NukeBuild
{
    const string Configuration = "Release";
    const string PackageId = "RawSqlMcp";
    const string ServerName = "io.github.KifoPL/RawSqlMcp";
    const string NuGetUser = "KifoPL";
    const string NuGetSource = "https://api.nuget.org/v3/index.json";
    const string McpPublisherUrl = "https://github.com/modelcontextprotocol/registry/releases/latest/download/mcp-publisher_linux_amd64.tar.gz";
    const string VersionPlaceholder = "0.0.0-placeholder";
    const int McpRegistryDescriptionMaxLength = 100;

    static readonly string[] RequiredServerProperties = ["name", "description", "version"];
    static readonly string[] RequiredPackageProperties = ["registryType", "identifier", "transport"];
    static readonly string[] RequiredRepositoryProperties = ["url", "source"];

    AbsolutePath SolutionFile => RootDirectory / "RawSqlMcp.slnx";
    AbsolutePath CliProject => RootDirectory / "src/RawSqlMcp.Cli/RawSqlMcp.Cli.csproj";
    AbsolutePath UnitTestProject => RootDirectory / "tests/RawSqlMcp.Cli.Tests.Unit/RawSqlMcp.Cli.Tests.Unit.csproj";
    AbsolutePath IntegrationTestProject => RootDirectory / "tests/RawSqlMcp.Cli.Tests.Integration/RawSqlMcp.Cli.Tests.Integration.csproj";
    AbsolutePath ServerJsonFile => RootDirectory / "src/RawSqlMcp.Cli/.mcp/server.json";
    AbsolutePath ReadmeFile => RootDirectory / "README.md";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath PackagesDirectory => ArtifactsDirectory / "packages";
    AbsolutePath CoverageDirectory => ArtifactsDirectory / "coverage";
    AbsolutePath CoverageFile => CoverageDirectory / "coverage.cobertura.xml";
    AbsolutePath VhsDirectory => RootDirectory / "docs/vhs";
    AbsolutePath InstallRunTape => VhsDirectory / "install-run.tape";
    AbsolutePath InstallRunGif => VhsDirectory / "install-run.gif";
    AbsolutePath EndToEndTape => VhsDirectory / "end-to-end.tape";
    AbsolutePath EndToEndGif => VhsDirectory / "end-to-end.gif";

    [Parameter, Secret] readonly string? CodecovToken;
    [Parameter, Secret] readonly string? ReleaseTagToken;
    [Parameter] readonly string? CommitMessageFile;

    GitHubActions? GitHubActions => GitHubActions.Instance;

    public static int Main() => Execute<Build>(x => x.CI);

    Target Clean => _ => _
       .Executes(() =>
        {
            RecreateDirectory(ArtifactsDirectory);
        });

    Target Restore => _ => _
       .Executes(() => DotNet("restore RawSqlMcp.slnx"));

    Target Format => _ => _
                         .DependsOn(Restore)
                         .Executes(() => DotNet("format RawSqlMcp.slnx"));

    Target BuildSolution => _ => _
                                .DependsOn(Restore)
                                .Executes(() => DotNet($"build RawSqlMcp.slnx --configuration {Configuration} --no-restore"));

    Target UnitTests => _ => _
                            .DependsOn(BuildSolution)
                            .Executes(() => DotNet($"test --project {UnitTestProject} --configuration {Configuration} --no-build"));

    Target IntegrationTests => _ => _
                                   .DependsOn(BuildSolution)
                                   .Executes(() => DotNet($"test --project {IntegrationTestProject} --configuration {Configuration} --no-build"));

    Target Coverage => _ => _
                           .DependsOn(BuildSolution)
                           .Produces(CoverageFile)
                           .Executes(() =>
                            {
                                RecreateDirectory(CoverageDirectory);
                                DotNet($"test --project {UnitTestProject} --configuration {Configuration} --no-build -- --coverage --coverage-output {CoverageFile} --coverage-output-format cobertura");
                                Require(File.Exists(CoverageFile), $"Coverage file was not created: {CoverageFile}");
                            });

    Target Pack => _ => _
                       .DependsOn(BuildSolution, ValidateServerJson)
                       .Produces(PackagesDirectory / "*.nupkg")
                       .Executes(() =>
                        {
                            RecreateDirectory(PackagesDirectory);
                            DotNet($"pack {CliProject} --configuration {Configuration} --no-build --output {PackagesDirectory}");
                        });

    Target ValidateServerJson => _ => _
       .Executes(() =>
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ServerJsonFile));
            JsonElement root = document.RootElement;

            RequireProperties(root, RequiredServerProperties, "server.json");
            RequireDescriptionsWithinLimit(root, "server.json");

            string name = GetRequiredString(root, "name", "server.json");
            Require(name == ServerName, $"server.json name must be '{ServerName}'. Actual: {name}");
            Require(File.ReadAllText(ReadmeFile).Contains($"mcp-name: {name}", StringComparison.Ordinal),
                    $"README.md must contain the MCP Registry ownership marker 'mcp-name: {name}'.");

            string schema = GetRequiredString(root, "$schema", "server.json");
            Require(Uri.TryCreate(schema, UriKind.Absolute, out Uri? _), "$schema must be an absolute URI.");

            string version = GetRequiredString(root, "version", "server.json");
            Require(version == VersionPlaceholder || IsSemanticVersion(version),
                    $"server.json version must be '{VersionPlaceholder}' or semantic version. Actual: {version}");

            if (root.TryGetProperty("repository", out JsonElement repository))
                RequireProperties(repository, RequiredRepositoryProperties, "server.json.repository");

            if (!root.TryGetProperty("packages", out JsonElement packages) || packages.ValueKind != JsonValueKind.Array || packages.GetArrayLength() == 0)
                Fail("server.json must contain at least one package.");

            for (int i = 0; i < packages.GetArrayLength(); i++)
            {
                JsonElement package = packages[i];
                string context = $"server.json.packages[{i}]";

                RequireProperties(package, RequiredPackageProperties, context);

                if (package.TryGetProperty("transport", out JsonElement transport))
                    RequireProperties(transport, ["type"], $"{context}.transport");

                if (package.TryGetProperty("environmentVariables", out JsonElement variables))
                {
                    for (int variableIndex = 0; variableIndex < variables.GetArrayLength(); variableIndex++)
                        RequireProperties(variables[variableIndex], ["name"], $"{context}.environmentVariables[{variableIndex}]");
                }
            }
        });

    Target InspectPackage => _ => _
                                 .DependsOn(Pack)
                                 .Executes(() =>
                                  {
                                      AbsolutePath package = GetSinglePackage();
                                      string version = GetPackageVersion(package);
                                      using ZipArchive archive = ZipFile.OpenRead(package);
                                      ZipArchiveEntry? entry = archive.GetEntry(".mcp/server.json");
                                      Require(entry != null, "Package does not contain .mcp/server.json.");
                                      ZipArchiveEntry? readmeEntry = archive.GetEntry("README.md");
                                      Require(readmeEntry != null, "Package does not contain README.md.");
                                      Require(archive.GetEntry("LICENSE") != null, "Package does not contain LICENSE.");

                                      using Stream stream = entry!.Open();
                                      using JsonDocument document = JsonDocument.Parse(stream);
                                      JsonElement root = document.RootElement;

                                      string name = GetRequiredString(root, "name", ".mcp/server.json");
                                      using Stream readmeStream = readmeEntry!.Open();
                                      using StreamReader readmeReader = new(readmeStream);
                                      Require(readmeReader.ReadToEnd().Contains($"mcp-name: {name}", StringComparison.Ordinal),
                                              $"Packaged README.md must contain the MCP Registry ownership marker 'mcp-name: {name}'.");

                                      GetRequiredString(root, "version", ".mcp/server.json").ShouldEqual(version, "Top-level server version must match package version.");
                                      JsonElement packageElement = root.GetProperty("packages")[0];
                                      GetRequiredString(packageElement, "version", ".mcp/server.json packages[0]").ShouldEqual(version, "Package server version must match package version.");
                                  });

    Target UploadCoverage => _ => _
                                 .DependsOn(Coverage)
                                 .OnlyWhenDynamic(() => GitHubActions != null && GitHubActions.Ref == "refs/heads/master")
                                 .Executes(() =>
                                  {
                                      if (string.IsNullOrWhiteSpace(CodecovToken))
                                      {
                                          Log.Warning("CODECOV_TOKEN is not available; skipping non-blocking Codecov upload.");
                                          return;
                                      }

                                      try
                                      {
                                          AbsolutePath uploader = ArtifactsDirectory / "codecov";
                                          DownloadFile("https://uploader.codecov.io/latest/linux/codecov", uploader);
                                          ChmodExecutable(uploader);
                                          RunProcess(uploader, $"--verbose upload-process --disable-search -t {CodecovToken} -f {CoverageFile}");
                                      }
                                      catch (Exception exception)
                                      {
                                          Log.Warning(exception, "Codecov upload failed and is non-blocking by design.");
                                      }
                                  });

    Target CI => _ => _
       .DependsOn(ValidateServerJson, UnitTests, IntegrationTests, Coverage, InspectPackage, UploadCoverage);

    Target Pipeline => _ => _
       .DependsOn(CI, CreateVersionTag);

    Target CreateVersionTag => _ => _
                                   .DependsOn(CI)
                                   .OnlyWhenDynamic(IsMasterPush)
                                   .Executes(() =>
                                    {
                                        // Merge commits can override the default patch bump with [release: major|minor|patch|skip|x.y.z[-pre]].
                                        RunProcess("git", "fetch origin --tags");

                                        string tags = RunProcessWithOutput("git", "tag --list v*");
                                        string message = RunProcessWithOutput("git", "log -1 --pretty=%B");
                                        ReleaseVersionPlan plan = ReleaseVersionPlanner.Plan(
                                            tags.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                                            message);

                                        if (!plan.ShouldCreateTag)
                                        {
                                            Log.Information("{Reason}", plan.Reason);
                                            return;
                                        }

                                        Require(!string.IsNullOrWhiteSpace(plan.Tag), "Release version planner did not return a tag.");
                                        Require(!string.IsNullOrWhiteSpace(plan.Version), "Release version planner did not return a version.");
                                        Require(!string.IsNullOrWhiteSpace(ReleaseTagToken), "RELEASE_TAG_TOKEN is required to push tags that trigger the release workflow.");

                                        RunProcess("git", "config user.name \"github-actions[bot]\"");
                                        RunProcess("git", "config user.email \"41898282+github-actions[bot]@users.noreply.github.com\"");
                                        RunProcess("git", $"tag {Quote(plan.Tag!)} -m {Quote($"Release {plan.Version}")}");
                                        ClearPersistedGitHubCheckoutCredentials();
                                        RunProcess("git", $"-c http.https://github.com/.extraheader= push {Quote(GetAuthenticatedGitHubRemote())} {Quote(plan.Tag!)}");
                                        Log.Information("Created release tag {Tag}; tag-triggered release workflow will publish package version {Version}.", plan.Tag, plan.Version);
                                    });

    Target GenerateVhs => _ => _
                              .DependsOn(BuildSolution)
                              .Executes(GenerateVhsGif);

    Target PreCommit => _ => _
       .Executes(() =>
        {
            string status = RunProcessWithOutput("git", "status --porcelain");
            bool generateVhs = PreCommitChangeDetector.HasChangedTapeFiles(status);
            IReadOnlyDictionary<string, string?> before = SnapshotCandidateHashes();

            DotNet("restore RawSqlMcp.slnx");
            DotNet("format RawSqlMcp.slnx");
            DotNet($"build RawSqlMcp.slnx --configuration {Configuration}");
            DotNet($"test --project {UnitTestProject} --configuration {Configuration} --no-build");
            if (generateVhs)
            {
                GenerateVhsGif();
            }
            else
            {
                Log.Information("Skipping VHS regeneration because no .tape files changed.");
            }

            StageFormattingAndGifChanges(before);
        });

    Target InstallGitHooks => _ => _
       .Executes(() =>
        {
            AbsolutePath hook = RootDirectory / ".git/hooks/pre-commit";
            Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
            File.WriteAllText(hook, """
                                    #!/usr/bin/env bash
                                    set -euo pipefail

                                    ./build.sh PreCommit
                                    """);
            ChmodExecutable(hook);
            Log.Information("Installed pre-commit hook at {Hook}", hook);

            AbsolutePath commitMessageHook = RootDirectory / ".git/hooks/commit-msg";
            File.WriteAllText(commitMessageHook, """
                                                 #!/usr/bin/env bash
                                                 set -euo pipefail

                                                 ./build.sh ValidateCommitMessage --commit-message-file "$1"
                                                 """);
            ChmodExecutable(commitMessageHook);
            Log.Information("Installed commit-msg hook at {Hook}", commitMessageHook);
        });

    Target ValidateCommitMessage => _ => _
       .Executes(() =>
        {
            string commitMessageFile = CommitMessageFile ?? string.Empty;
            Require(!string.IsNullOrWhiteSpace(commitMessageFile), "--commit-message-file is required.");
            Require(File.Exists(commitMessageFile), $"Commit message file does not exist: {commitMessageFile}");

            string message = File.ReadAllText(commitMessageFile);
            string? error = CommitMessageLengthGate.GetValidationError(message);
            if (error != null)
                Fail(error);
        });

    Target Release => _ => _
                          .DependsOn(ValidateServerJson, UnitTests, InspectPackage)
                          .Executes(async () =>
                           {
                               if (!IsPublishingRelease())
                               {
                                   await RunReleaseDryRunAsync();
                                   return;
                               }

                               AbsolutePath package = GetSinglePackage();
                               string version = GetPackageVersion(package);
                               string tag = GitHubActions!.Ref["refs/tags/".Length..];

                               Require(tag == $"v{version}", $"Tag '{tag}' must match package version '{version}'.");
                               EnsureTagIsReachableFromMaster();

                               string apiKey = await ExchangeNuGetOidcTokenAsync();
                               DotNet($"nuget push {package} --source {NuGetSource} --api-key {apiKey} --skip-duplicate");

                               PublishMcpRegistryWithRetry(package, version);
                               CreateGitHubRelease(tag, version);
                           });

    async Task RunReleaseDryRunAsync()
    {
        AbsolutePath package = GetSinglePackage();
        string version = GetPackageVersion(package);
        await CheckNuGetVersionAsync(version);
        Log.Information("Release dry run completed for {PackageId} {Version}; no publishing was performed.", PackageId, version);
    }

    static bool IsMasterPush() =>
        string.Equals(Environment.GetEnvironmentVariable("GITHUB_EVENT_NAME"), "push", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Environment.GetEnvironmentVariable("GITHUB_REF"), "refs/heads/master", StringComparison.Ordinal);

    bool IsPublishingRelease() =>
        ReleasePublishingGate.ShouldPublish(
            Environment.GetEnvironmentVariable("GITHUB_EVENT_NAME"),
            GitHubActions?.Ref);

    string GetAuthenticatedGitHubRemote()
    {
        string repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")
                         ?? RunProcessWithOutput("git", "config --get remote.origin.url")
                           .Replace("git@github.com:", string.Empty, StringComparison.Ordinal)
                           .Replace("https://github.com/", string.Empty, StringComparison.Ordinal)
                           .TrimEnd('/');

        return $"https://x-access-token:{ReleaseTagToken}@github.com/{repository}.git";
    }

    void ClearPersistedGitHubCheckoutCredentials()
    {
        TryRunProcess("git", "config --local --unset-all http.https://github.com/.extraheader");

        string configNames = RunProcessWithOutputOrEmpty("git", "config --local --name-only --get-regexp ^includeIf\\.gitdir:");
        foreach (string key in GitHubActionsCredentialCleaner.GetCheckoutCredentialKeysToUnset(configNames))
            TryRunProcess("git", $"config --local --unset-all {Quote(key)}");
    }

    void EnsureTagIsReachableFromMaster()
    {
        RunProcess("git", "fetch origin master --tags");
        RunProcess("git", "merge-base --is-ancestor HEAD origin/master");
    }

    async Task<string> ExchangeNuGetOidcTokenAsync()
    {
        string? requestUrl = Environment.GetEnvironmentVariable("ACTIONS_ID_TOKEN_REQUEST_URL");
        string? requestToken = Environment.GetEnvironmentVariable("ACTIONS_ID_TOKEN_REQUEST_TOKEN");
        Require(!string.IsNullOrWhiteSpace(requestUrl), "ACTIONS_ID_TOKEN_REQUEST_URL is required for NuGet Trusted Publishing.");
        Require(!string.IsNullOrWhiteSpace(requestToken), "ACTIONS_ID_TOKEN_REQUEST_TOKEN is required for NuGet Trusted Publishing.");

        string oidcUrl = $"{requestUrl}&audience={Uri.EscapeDataString("https://www.nuget.org")}";
        using var http = new HttpClient();
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Get, oidcUrl);
        tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", requestToken);

        using HttpResponseMessage tokenResponse = await http.SendAsync(tokenRequest);
        string tokenJson = await tokenResponse.Content.ReadAsStringAsync();
        tokenResponse.EnsureSuccessStatusCode();

        string oidcToken = JsonDocument.Parse(tokenJson).RootElement.GetProperty("value").GetString()
                        ?? throw new InvalidOperationException("GitHub OIDC response did not contain a token value.");

        using var exchangeRequest = new HttpRequestMessage(HttpMethod.Post, "https://www.nuget.org/api/v2/token");
        exchangeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", oidcToken);
        exchangeRequest.Headers.UserAgent.ParseAdd("RawSqlMcp/NukeBuild");
        exchangeRequest.Content = new StringContent(JsonSerializer.Serialize(new
        {
            username = NuGetUser,
            tokenType = "ApiKey"
        }), Encoding.UTF8, "application/json");

        using HttpResponseMessage exchangeResponse = await http.SendAsync(exchangeRequest);
        string exchangeJson = await exchangeResponse.Content.ReadAsStringAsync();
        if (!exchangeResponse.IsSuccessStatusCode)
            Fail($"NuGet OIDC token exchange failed: {(int)exchangeResponse.StatusCode} {exchangeJson}");

        string apiKey = JsonDocument.Parse(exchangeJson).RootElement.GetProperty("apiKey").GetString()
                     ?? throw new InvalidOperationException("NuGet token response did not contain apiKey.");

        return apiKey;
    }

    async Task CheckNuGetVersionAsync(string version)
    {
        string packageUrl = $"https://api.nuget.org/v3-flatcontainer/{PackageId.ToLowerInvariant()}/{version}/{PackageId.ToLowerInvariant()}.{version}.nupkg";
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Head, packageUrl);
        using HttpResponseMessage response = await http.SendAsync(request);
        Log.Information("NuGet availability check for {PackageId} {Version}: {StatusCode}", PackageId, version, response.StatusCode);
    }

    void PublishMcpRegistryWithRetry(AbsolutePath package, string version)
    {
        AbsolutePath publisherDirectory = ArtifactsDirectory / "mcp-publisher";
        RecreateDirectory(publisherDirectory);
        AbsolutePath archive = publisherDirectory / "mcp-publisher.tar.gz";
        DownloadFile(McpPublisherUrl, archive);
        RunProcess("tar", $"xzf {archive} -C {publisherDirectory}");
        AbsolutePath publisher = publisherDirectory / "mcp-publisher";
        ChmodExecutable(publisher);

        using ZipArchive packageArchive = ZipFile.OpenRead(package);
        ZipArchiveEntry? serverJsonEntry = packageArchive.GetEntry(".mcp/server.json");
        Require(serverJsonEntry != null, "Package does not contain .mcp/server.json.");
        using (Stream input = serverJsonEntry!.Open())
        using (FileStream output = File.Create(RootDirectory / "server.json"))
            input.CopyTo(output);

        try
        {
            RunProcess(publisher, "login github-oidc");

            for (int attempt = 1; attempt <= 8; attempt++)
            {
                IProcess process = StartProcess(publisher, "publish", RootDirectory, logOutput: true);
                process.WaitForExit();
                if (process.ExitCode == 0)
                    return;

                string output = string.Join(Environment.NewLine, process.Output.Select(x => x.Text));
                if (Regex.IsMatch(output, "(already exists|already published|version.*exists)", RegexOptions.IgnoreCase))
                    return;

                bool retryable = Regex.IsMatch(output, "(exists but version|version.*not.*available|eventual consistency|propagate|propagation delay)", RegexOptions.IgnoreCase);
                if (!retryable || attempt == 8)
                    Fail($"MCP Registry publish failed for {PackageId} {version}.");

                int sleepSeconds = Math.Min(180, 15 * (int)Math.Pow(2, attempt - 1));
                Log.Warning("MCP Registry cannot see NuGet version yet; retrying in {SleepSeconds}s.", sleepSeconds);
                Thread.Sleep(TimeSpan.FromSeconds(sleepSeconds));
            }
        }
        finally
        {
            File.Delete(RootDirectory / "server.json");
        }
    }

    void CreateGitHubRelease(string tag, string version)
    {
        RequireTool("gh", "GitHub CLI is required to create releases on GitHub Actions runners.");
        IReadOnlyDictionary<string, string> env = GitHubCliEnvironment.CreateForCurrentProcess();
        Require(env.ContainsKey("GH_TOKEN"), "GITHUB_TOKEN or GH_TOKEN is required to create GitHub releases.");

        string repository = GetGitHubRepository();
        IProcess existingRelease = StartProcess("gh", $"release view {Quote(tag)} --repo {Quote(repository)} --json url", RootDirectory, environmentVariables: env, logOutput: false);
        existingRelease.WaitForExit();
        if (existingRelease.ExitCode == 0)
        {
            Log.Information("GitHub release {Tag} already exists; skipping release creation.", tag);
            return;
        }

        string prerelease = version.Contains('-', StringComparison.Ordinal) ? "--prerelease" : string.Empty;
        RunProcess("gh", $"release create {Quote(tag)} --repo {Quote(repository)} --verify-tag --generate-notes {prerelease}", env);
    }

    string GetGitHubRepository() =>
        Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")
     ?? RunProcessWithOutput("git", "config --get remote.origin.url")
       .Replace("git@github.com:", string.Empty, StringComparison.Ordinal)
       .Replace("https://github.com/", string.Empty, StringComparison.Ordinal)
       .TrimEnd('/', '.')
       .Replace(".git", string.Empty, StringComparison.Ordinal);

    AbsolutePath GetSinglePackage()
    {
        AbsolutePath[] packages = Directory.GetFiles(PackagesDirectory, "*.nupkg")
                                           .Where(x => !x.ToString().EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
                                           .Select(x => (AbsolutePath)x)
                                           .ToArray();
        Require(packages.Length == 1, $"Expected one package in {PackagesDirectory}, found {packages.Length}.");
        return packages[0];
    }

    static string GetPackageVersion(string package)
    {
        string fileName = Path.GetFileName(package);
        Match match = Regex.Match(fileName, @"^RawSqlMcp\.(?<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?)\.nupkg$");
        Require(match.Success, $"Unexpected package filename: {fileName}");
        return match.Groups["version"].Value;
    }

    static bool IsSemanticVersion(string version) =>
        Regex.IsMatch(version, @"^\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?$");

    static void RequireProperties(JsonElement element, string[] properties, string context)
    {
        foreach (string property in properties)
            Require(element.TryGetProperty(property, out _), $"{context} is missing required property '{property}'.");
    }

    static string GetRequiredString(JsonElement element, string property, string context)
    {
        Require(element.TryGetProperty(property, out JsonElement value), $"{context} is missing required property '{property}'.");
        Require(value.ValueKind == JsonValueKind.String, $"{context}.{property} must be a string.");
        string? text = value.GetString();
        Require(!string.IsNullOrWhiteSpace(text), $"{context}.{property} must not be empty.");
        return text!;
    }

    static void RequireDescriptionsWithinLimit(JsonElement element, string context)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                string propertyContext = $"{context}.{property.Name}";
                if (property.NameEquals("description"))
                {
                    string description = property.Value.GetString() ?? string.Empty;
                    Require(description.Length <= McpRegistryDescriptionMaxLength,
                            $"{propertyContext} must be {McpRegistryDescriptionMaxLength} characters or fewer. Actual length: {description.Length}.");
                }

                RequireDescriptionsWithinLimit(property.Value, propertyContext);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement child in element.EnumerateArray())
            {
                RequireDescriptionsWithinLimit(child, $"{context}[{index}]");
                index++;
            }
        }
    }

    static void RequireTool(string executable, string message)
    {
        IProcess process = StartProcess("which", executable, logOutput: false);
        process.WaitForExit();
        Require(process.ExitCode == 0, message);
    }

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    static void Fail(string message) => throw new InvalidOperationException(message);

    static void DownloadFile(string url, string destination)
    {
        using var http = new HttpClient();
        byte[] bytes = http.GetByteArrayAsync(url).GetAwaiter().GetResult();
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllBytes(destination, bytes);
    }

    void GenerateVhsGif()
    {
        RequireTool("vhs", "Install VHS from https://github.com/charmbracelet/vhs to regenerate README GIFs.");
        RunProcess("vhs", InstallRunTape);
        RunProcess("vhs", EndToEndTape);
        Require(File.Exists(InstallRunGif), $"VHS did not create {InstallRunGif}");
        Require(File.Exists(EndToEndGif), $"VHS did not create {EndToEndGif}");
    }

    void StageFormattingAndGifChanges(IReadOnlyDictionary<string, string?> before)
    {
        string status = RunProcessWithOutput("git", "status --porcelain");
        foreach (string line in status.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            string path = line.Length > 3 ? line[3..] : string.Empty;
            if (!path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) && !IsFormattingCandidate(path))
                continue;

            string? previousHash = before.GetValueOrDefault(path);
            string? currentHash = GetFileHash(path);
            if (!string.Equals(previousHash, currentHash, StringComparison.Ordinal))
                RunProcess("git", $"add -- {Quote(path)}");
        }
    }

    IReadOnlyDictionary<string, string?> SnapshotCandidateHashes()
    {
        var hashes = new Dictionary<string, string?>(StringComparer.Ordinal);
        string status = RunProcessWithOutput("git", "status --porcelain");
        foreach (string line in status.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            string path = line.Length > 3 ? line[3..] : string.Empty;
            if (path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) || IsFormattingCandidate(path))
                hashes[path] = GetFileHash(path);
        }

        hashes[RootRelative(InstallRunGif)] = GetFileHash(RootRelative(InstallRunGif));
        return hashes;
    }

    string RootRelative(string path) => Path.GetRelativePath(RootDirectory, path);

    string? GetFileHash(string rootRelativePath)
    {
        AbsolutePath path = RootDirectory / rootRelativePath;
        if (!File.Exists(path))
            return null;

        using FileStream stream = File.OpenRead(path);
        byte[] hash = System.Security.Cryptography.SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    static bool IsFormattingCandidate(string path)
    {
        string extension = Path.GetExtension(path);
        return extension is ".cs" or ".csproj" or ".props" or ".json" or ".slnx" or ".md" or ".yml" or ".yaml" or ".tape";
    }

    void DotNet(string arguments) => RunProcess("dotnet", arguments);

    static void RecreateDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);

        Directory.CreateDirectory(path);
    }

    void RunProcess(string executable, object arguments, IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        IProcess process = StartProcess(executable.ToString()!, arguments.ToString()!, RootDirectory, environmentVariables: environmentVariables);
        process.AssertZeroExitCode();
    }

    string RunProcessWithOutput(string executable, string arguments)
    {
        IProcess process = StartProcess(executable, arguments, RootDirectory, logOutput: false);
        process.AssertZeroExitCode();
        return string.Join(Environment.NewLine, process.Output.Select(x => x.Text));
    }

    string RunProcessWithOutputOrEmpty(string executable, string arguments)
    {
        IProcess process = StartProcess(executable, arguments, RootDirectory, logOutput: false);
        process.WaitForExit();
        return process.ExitCode == 0
                   ? string.Join(Environment.NewLine, process.Output.Select(x => x.Text))
                   : string.Empty;
    }

    void TryRunProcess(string executable, string arguments)
    {
        IProcess process = StartProcess(executable, arguments, RootDirectory, logOutput: false);
        process.WaitForExit();
    }

    static void ChmodExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        IProcess process = StartProcess("chmod", $"+x {Quote(path)}", logOutput: false);
        process.AssertZeroExitCode();
    }

    static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}