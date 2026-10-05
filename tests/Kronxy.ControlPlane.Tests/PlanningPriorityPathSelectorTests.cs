using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kronxy.Application.Execution;
using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class PlanningPriorityPathSelectorTests
{
    [Fact]
    public void KRX_request_prioritizes_job_external_id()
    {
        byte[] package =
            CreatePackage(
                [
                    "src/Kronxy.Api/Controllers/Projects/ProjectsController.cs",
                    "src/Kronxy.Application/Abstractions/Behaviors/ValidationBehavior.cs",
                    "src/Kronxy.Application/Jobs/JobExternalId.cs",
                    "src/Kronxy.Application/Jobs/DefaultJobIdGenerator.cs",
                    "tests/Kronxy.ControlPlane.Tests/JobApplicationTests.cs",
                    "tests/Kronxy.ControlPlane.Tests/JobTests.cs"
                ]);

        var selector =
            new PlanningPriorityPathSelector();

        IReadOnlyList<string> result =
            selector.Select(
                package,
                "Harden external KRX job identifier validation so lowercase prefixes are rejected while canonical valid KRX identifiers remain accepted. Add the focused automated test required to demonstrate the behavior. Make only the smallest complete change necessary.");

        Assert.NotEmpty(result);

        Assert.Equal(
            "src/Kronxy.Application/Jobs/JobExternalId.cs",
            result[0]);

        string[] ordered =
            result.ToArray();

        Assert.True(
            Array.IndexOf(
                ordered,
                "tests/Kronxy.ControlPlane.Tests/JobApplicationTests.cs") <
            Array.IndexOf(
                ordered,
                "src/Kronxy.Application/Abstractions/Behaviors/ValidationBehavior.cs"));

        Assert.True(
            Array.IndexOf(
                ordered,
                "tests/Kronxy.ControlPlane.Tests/JobTests.cs") <
            Array.IndexOf(
                ordered,
                "src/Kronxy.Application/Abstractions/Behaviors/ValidationBehavior.cs"));

        Assert.DoesNotContain(
            "src/Kronxy.Api/Controllers/Projects/ProjectsController.cs",
            result);
    }

    [Fact]
    public void Equal_score_prefers_smaller_files_before_path_order()
    {
        byte[] package =
            CreatePackage(
                [
                    "tests/z/JobLargeTests.cs",
                    "tests/a/JobSmallTests.cs"
                ],
                new Dictionary<string, int>
                {
                    ["tests/z/JobLargeTests.cs"] =
                        4_000,
                    ["tests/a/JobSmallTests.cs"] =
                        100
                });

        var selector =
            new PlanningPriorityPathSelector();

        IReadOnlyList<string> result =
            selector.Select(
                package,
                "Update job behavior and add automated tests.");

        Assert.Equal(
            "tests/a/JobSmallTests.cs",
            result[0]);

        Assert.Equal(
            "tests/z/JobLargeTests.cs",
            result[1]);
    }

    [Fact]
    public void Explicitly_named_repository_paths_are_prioritized()
    {
        const string explicitlyNamed =
            "src/Kronxy.Domain/Projects/Project.cs";

        byte[] package =
            CreatePackage(
                [
                    "src/Kronxy.Domain/Projects/IProjectTypeRepository.cs",
                    explicitlyNamed
                ],
                new Dictionary<string, int>
                {
                    ["src/Kronxy.Domain/Projects/IProjectTypeRepository.cs"] =
                        10,
                    [explicitlyNamed] =
                        4_000
                });

        var selector =
            new PlanningPriorityPathSelector();

        IReadOnlyList<string> result =
            selector.Select(
                package,
                $"Inspect {explicitlyNamed} for the existing Project pattern.");

        Assert.Equal(
            explicitlyNamed,
            result[0]);
    }

    [Fact]
    public void Explicit_pattern_reference_strongly_prioritizes_matching_files()
    {
        byte[] package = CreatePackage(
            [
                "src/Kronxy.Domain/Projects/Project.cs",
                "src/Kronxy.Domain/ProjectModules/ProjectModule.cs",
                "src/Kronxy.Domain/ProjectModules/ProjectModuleErrors.cs",
                "src/Kronxy.Domain/ProjectTasks/ProjectTask.cs"
            ],
            new Dictionary<string, int>
            {
                ["src/Kronxy.Domain/Projects/Project.cs"] = 10,
                ["src/Kronxy.Domain/ProjectModules/ProjectModule.cs"] = 4_000,
                ["src/Kronxy.Domain/ProjectModules/ProjectModuleErrors.cs"] = 2_000,
                ["src/Kronxy.Domain/ProjectTasks/ProjectTask.cs"] = 20
            });

        var selector = new PlanningPriorityPathSelector();

        IReadOnlyList<string> result = selector.Select(
            package,
            "Implement ProjectArea Domain-only. Use ProjectModule as the primary implementation pattern.");

        Assert.StartsWith(
            "src/Kronxy.Domain/ProjectModules/",
            result[0],
            StringComparison.Ordinal);
        Assert.StartsWith(
            "src/Kronxy.Domain/ProjectModules/",
            result[1],
            StringComparison.Ordinal);
    }

    [Fact]
    public void Request_without_explicit_reference_preserves_normal_ranking()
    {
        byte[] package = CreatePackage(
            [
                "src/Kronxy.Domain/Projects/Project.cs",
                "src/Kronxy.Domain/ProjectModules/ProjectModule.cs"
            ],
            new Dictionary<string, int>
            {
                ["src/Kronxy.Domain/Projects/Project.cs"] = 10,
                ["src/Kronxy.Domain/ProjectModules/ProjectModule.cs"] = 4_000
            });

        var selector = new PlanningPriorityPathSelector();

        IReadOnlyList<string> result = selector.Select(
            package,
            "Implement project hierarchy domain behavior.");

        Assert.Empty(
            PlanningPriorityPathSelector
                .ExtractExplicitReferenceTerms(
                    "Implement project hierarchy domain behavior."));
        Assert.Equal(
            "src/Kronxy.Domain/Projects/Project.cs",
            result[0]);
    }

    [Fact]
    public void Explicit_reference_priority_is_not_domain_name_hardcoded()
    {
        byte[] package = CreatePackage(
            [
                "src/Records/Record.cs",
                "src/Records/CustomerRecord.cs"
            ],
            new Dictionary<string, int>
            {
                ["src/Records/Record.cs"] = 10,
                ["src/Records/CustomerRecord.cs"] = 3_000
            });

        var selector = new PlanningPriorityPathSelector();

        IReadOnlyList<string> result = selector.Select(
            package,
            "Implement a record feature. Use CustomerRecord as the implementation pattern.");

        Assert.Equal(
            "src/Records/CustomerRecord.cs",
            result[0]);
    }

    [Fact]
    public void Selection_is_deterministic()
    {
        byte[] package =
            CreatePackage(
                [
                    "src/z/JobB.cs",
                    "src/a/JobA.cs"
                ]);

        var selector =
            new PlanningPriorityPathSelector();

        IReadOnlyList<string> first =
            selector.Select(
                package,
                "job");

        IReadOnlyList<string> second =
            selector.Select(
                package,
                "job");

        Assert.Equal(
            first,
            second);
    }

    [Fact]
    public void Top_five_overlap_rejects_divergent_plan_and_position_six()
    {
        string[] priorityPaths =
        [
            "src/Kronxy.Application/Jobs/JobExternalId.cs",
            "tests/Kronxy.ControlPlane.Tests/JobLimitTests.cs",
            "tests/Kronxy.ControlPlane.Tests/JobRunIdProviderTests.cs",
            "tests/Kronxy.ControlPlane.Tests/JobStateMachineTests.cs",
            "tests/Kronxy.ControlPlane.Tests/JobDependencyInjectionTests.cs",
            "src/Kronxy.Application/Jobs/JobRepository.cs"
        ];

        PlannerPlan plan =
            Plan(
                ["src/Kronxy.Application/Jobs/Job.cs"],
                [
                    "src/Kronxy.Application/Jobs/JobService.cs",
                    "src/Kronxy.Application/Jobs/JobRepository.cs"
                ]);

        Assert.False(
            PlanningPriorityPathSelector
                .HasTopFivePlanPathOverlap(
                    plan,
                    priorityPaths));
    }

    [Fact]
    public void Top_five_overlap_accepts_inspection_or_candidate_match()
    {
        string[] priorityPaths =
        [
            "src/Kronxy.Application/Jobs/JobExternalId.cs"
        ];

        Assert.True(
            PlanningPriorityPathSelector
                .HasTopFivePlanPathOverlap(
                    Plan(
                        [priorityPaths[0]],
                        Array.Empty<string>()),
                    priorityPaths));

        Assert.True(
            PlanningPriorityPathSelector
                .HasTopFivePlanPathOverlap(
                    Plan(
                        Array.Empty<string>(),
                        [priorityPaths[0]]),
                    priorityPaths));
    }

    [Fact]
    public void Top_five_overlap_is_ordinal_and_fails_closed()
    {
        string[] priorityPaths =
        [
            "src/Kronxy.Application/Jobs/JobExternalId.cs"
        ];

        Assert.False(
            PlanningPriorityPathSelector
                .HasTopFivePlanPathOverlap(
                    Plan(
                        ["src/kronxy.application/jobs/jobexternalid.cs"],
                        Array.Empty<string>()),
                    priorityPaths));

        Assert.False(
            PlanningPriorityPathSelector
                .HasTopFivePlanPathOverlap(
                    null,
                    priorityPaths));

        Assert.False(
            PlanningPriorityPathSelector
                .HasTopFivePlanPathOverlap(
                    Plan(
                        Array.Empty<string>(),
                        Array.Empty<string>()),
                    null));

        Assert.False(
            PlanningPriorityPathSelector
                .HasTopFivePlanPathOverlap(
                    Plan(
                        Array.Empty<string>(),
                        Array.Empty<string>()),
                    Array.Empty<string>()));
    }

    private static PlannerPlan Plan(
        IReadOnlyList<string> filesToInspect,
        IReadOnlyList<string> candidateFilesToModify) =>
        new()
        {
            Objective = "Validate planning path coherence.",
            FilesToInspect = filesToInspect,
            CandidateFilesToModify = candidateFilesToModify,
            Strategy = "Use the selected paths.",
            AcceptanceCriteria = Array.Empty<string>(),
            Risks = Array.Empty<string>(),
            ExpectedTests = Array.Empty<string>(),
            Assumptions = Array.Empty<string>(),
            Uncertainties = Array.Empty<string>()
        };

    private static byte[] CreatePackage(
        IReadOnlyList<string> paths,
        IReadOnlyDictionary<string, int>? sizes = null)
    {
        using var output =
            new MemoryStream();

        using (
            var archive =
                new ZipArchive(
                    output,
                    ZipArchiveMode.Create,
                    leaveOpen: true))
        {
            var entries =
                new List<object>();

            foreach (
                string path
                in paths)
            {
                int contentSize =
                    sizes is not null &&
                    sizes.TryGetValue(
                        path,
                        out int requestedSize)
                        ? requestedSize
                        : 10;

                byte[] content =
                    Encoding.UTF8.GetBytes(
                        new string(
                            '/',
                            contentSize));

                ZipArchiveEntry file =
                    archive.CreateEntry(path);

                using (
                    Stream stream =
                        file.Open())
                {
                    stream.Write(
                        content);
                }

                entries.Add(
                    new
                    {
                        path,
                        kind = "Text",
                        sizeBytes =
                            content.Length,
                        sha256 =
                            Convert.ToHexString(
                                    SHA256.HashData(
                                        content))
                                .ToLowerInvariant()
                    });
            }

            byte[] manifest =
                JsonSerializer.SerializeToUtf8Bytes(
                    new
                    {
                        fileCount =
                            paths.Count,
                        entries
                    });

            ZipArchiveEntry manifestEntry =
                archive.CreateEntry(
                    "manifest.json");

            using Stream manifestStream =
                manifestEntry.Open();

            manifestStream.Write(
                manifest);
        }

        return output.ToArray();
    }

    [Fact]
    public void Explicit_domain_scope_is_detected()
    {
        string? prefix =
            PlanningPriorityPathSelector
                .ResolveExplicitLayerPrefix(
                    """
                    Implement the Domain layer for ProjectModule.

                    This Job is deliberately limited to Domain code.

                    Do NOT implement API endpoints or persistence.
                    """);

        Assert.Equal(
            "src/Kronxy.Domain/",
            prefix);
    }

    [Fact]
    public void Generic_request_has_no_explicit_layer_scope()
    {
        string? prefix =
            PlanningPriorityPathSelector
                .ResolveExplicitLayerPrefix(
                    "Implement project hierarchy safely.");

        Assert.Null(prefix);
    }


    [Fact]
    public void Explicit_domain_scope_filters_non_domain_paths()
    {
        byte[] package =
            CreatePackage(
                [
                    "src/Kronxy.Api/Controllers/Projects/ProjectsController.cs",
                    "src/Kronxy.Application/Projects/GetProject/GetProjectQuery.cs",
                    "src/Kronxy.Infrastructure/Configurations/ProjectConfiguration.cs",
                    "src/Kronxy.Domain/Projects/Project.cs",
                    "src/Kronxy.Domain/Projects/ProjectErrors.cs",
                    "src/Kronxy.Domain/ProjectTasks/ProjectTask.cs"
                ]);

        var selector =
            new PlanningPriorityPathSelector();

        IReadOnlyList<string> paths =
            selector.Select(
                package,
                """
                Implement the Domain layer for ProjectModule.

                This Job is deliberately limited to Domain code.

                Inspect existing Project and ProjectTask patterns.
                """);

        Assert.NotEmpty(paths);

        Assert.All(
            paths,
            path =>
                Assert.StartsWith(
                    "src/Kronxy.Domain/",
                    path,
                    StringComparison.Ordinal));

        Assert.DoesNotContain(
            paths,
            path =>
                path.StartsWith(
                    "src/Kronxy.Api/",
                    StringComparison.Ordinal));

        Assert.DoesNotContain(
            paths,
            path =>
                path.StartsWith(
                    "src/Kronxy.Application/",
                    StringComparison.Ordinal));

        Assert.DoesNotContain(
            paths,
            path =>
                path.StartsWith(
                    "src/Kronxy.Infrastructure/",
                    StringComparison.Ordinal));
    }

}
