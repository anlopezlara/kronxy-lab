using System.Text.Json;
using Kronxy.Application.Execution;
using Kronxy.Infrastructure.AI;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class DeveloperContractTests
{
    private readonly AiStructuredOutputValidator validator =
        new();

    [Fact]
    public void Valid_proposal_matches_schema_and_deserializes()
    {
        const string json =
            """
            {
              "summary": "Update the deterministic helper.",
              "changes": [
                {
                  "operation": "ReplaceFile",
                  "relativePath": "src/Example.cs",
                  "intent": "Correct the helper result.",
                  "content": "namespace Example;",
                  "expectedContentSha256": "abc123"
                }
              ],
              "assumptions": [],
              "risks": [
                "Existing callers may depend on the old result."
              ]
            }
            """;

        bool valid =
            validator.TryValidate(
                DeveloperContractSchema.CreateSchema(),
                json,
                out string error);

        DeveloperProposal? proposal =
            JsonSerializer.Deserialize<DeveloperProposal>(
                json);

        Assert.True(valid);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(proposal);
        Assert.Single(proposal.Changes);
        Assert.Equal(
            DeveloperChangeOperationType.ReplaceFile,
            proposal.Changes[0].Operation);
        Assert.Equal(
            "src/Example.cs",
            proposal.Changes[0].RelativePath);
    }

    [Fact]
    public void Verbose_metadata_and_excess_operations_are_rejected_by_schema()
    {
        var proposal = new DeveloperProposal
        {
            Summary = new string((char)120, 257),
            Changes = Enumerable.Range(0, 5).Select(index =>
                new DeveloperChangeOperation
                {
                    Operation = DeveloperChangeOperationType.CreateFile,
                    RelativePath = $"src/File{index}.cs",
                    Intent = "minimal",
                    Content = "namespace Example;",
                    ExpectedContentSha256 = string.Empty
                }).ToArray(),
            Assumptions = [],
            Risks = []
        };

        string json = JsonSerializer.Serialize(proposal);
        Assert.False(validator.TryValidate(
            DeveloperContractSchema.CreateSchema(), json, out string error));
        Assert.Equal("AI_STRUCTURED_SCHEMA_MISMATCH", error);
    }

    [Fact]
    public void Representative_two_file_proposal_serializes_within_compact_character_target()
    {
        var proposal = new DeveloperProposal
        {
            Summary = "Apply the focused change and test.",
            Changes =
            [
                new DeveloperChangeOperation
                {
                    Operation = DeveloperChangeOperationType.ReplaceFile,
                    RelativePath = "src/Target.cs",
                    Intent = "Enforce canonical identifiers.",
                    Content = new string((char)120, 3_312),
                    ExpectedContentSha256 = new string((char)97, 64)
                },
                new DeveloperChangeOperation
                {
                    Operation = DeveloperChangeOperationType.CreateFile,
                    RelativePath = "tests/TargetTests.cs",
                    Intent = "Cover lowercase rejection.",
                    Content = new string((char)121, 245),
                    ExpectedContentSha256 = string.Empty
                }
            ],
            Assumptions = [],
            Risks = []
        };

        string json = JsonSerializer.Serialize(proposal);
        Assert.True(json.Length < 8_000);
        Assert.True(validator.TryValidate(
            DeveloperContractSchema.CreateSchema(), json, out string error));
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void Compact_three_file_proposal_preserves_complete_source_content()
    {
        var proposal = new DeveloperProposal
        {
            Summary = "Create ProjectModule domain types.",
            Changes =
            [
                new DeveloperChangeOperation
                {
                    Operation = DeveloperChangeOperationType.CreateFile,
                    RelativePath = "src/ProjectModule.cs",
                    Intent = "Create the aggregate.",
                    Content = new string((char)120, 2_500),
                    ExpectedContentSha256 = string.Empty
                },
                new DeveloperChangeOperation
                {
                    Operation = DeveloperChangeOperationType.CreateFile,
                    RelativePath = "src/ProjectModuleErrors.cs",
                    Intent = "Create domain errors.",
                    Content = new string((char)121, 800),
                    ExpectedContentSha256 = string.Empty
                },
                new DeveloperChangeOperation
                {
                    Operation = DeveloperChangeOperationType.CreateFile,
                    RelativePath = "src/IProjectModuleRepository.cs",
                    Intent = "Create repository contract.",
                    Content = new string((char)122, 500),
                    ExpectedContentSha256 = string.Empty
                }
            ],
            Assumptions = [],
            Risks = []
        };

        string json = JsonSerializer.Serialize(proposal);

        Assert.True(json.Length < 6_000);
        Assert.True(validator.TryValidate(
            DeveloperContractSchema.CreateSchema(), json, out string error));
        Assert.Equal(string.Empty, error);
        Assert.Equal(3_800, proposal.Changes.Sum(change => change.Content.Length));
    }

    [Fact]
    public void Descriptive_metadata_is_bounded_for_compact_output()
    {
        const string json =
            """
            {
              "summary": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
              "changes": [{
                "operation": "CreateFile",
                "relativePath": "src/NewFile.cs",
                "intent": "Create the requested type.",
                "content": "namespace Example;",
                "expectedContentSha256": ""
              }],
              "assumptions": [],
              "risks": []
            }
            """;

        Assert.False(validator.TryValidate(
            DeveloperContractSchema.CreateSchema(), json, out string error));
        Assert.Equal("AI_STRUCTURED_SCHEMA_MISMATCH", error);
    }

    [Fact]
    public void Unknown_operation_is_rejected()
    {
        const string json =
            """
            {
              "summary": "Unsafe proposal.",
              "changes": [
                {
                  "operation": "ExecuteCommand",
                  "relativePath": "src/Example.cs",
                  "intent": "Run an arbitrary command.",
                  "content": "ignored",
                  "expectedContentSha256": ""
                }
              ],
              "assumptions": [],
              "risks": []
            }
            """;

        bool valid =
            validator.TryValidate(
                DeveloperContractSchema.CreateSchema(),
                json,
                out string error);

        Assert.False(valid);
        Assert.Equal(
            "AI_STRUCTURED_SCHEMA_MISMATCH",
            error);
    }

    [Fact]
    public void Missing_operation_field_is_rejected()
    {
        const string json =
            """
            {
              "summary": "Incomplete proposal.",
              "changes": [
                {
                  "operation": "CreateFile",
                  "relativePath": "src/NewFile.cs",
                  "intent": "Add a helper.",
                  "content": "namespace Example;"
                }
              ],
              "assumptions": [],
              "risks": []
            }
            """;

        bool valid =
            validator.TryValidate(
                DeveloperContractSchema.CreateSchema(),
                json,
                out string error);

        Assert.False(valid);
        Assert.Equal(
            "AI_STRUCTURED_SCHEMA_MISMATCH",
            error);
    }

    [Fact]
    public void Additional_operational_field_is_rejected()
    {
        const string json =
            """
            {
              "summary": "Proposal with an extra command.",
              "changes": [
                {
                  "operation": "CreateFile",
                  "relativePath": "src/NewFile.cs",
                  "intent": "Add a helper.",
                  "content": "namespace Example;",
                  "expectedContentSha256": "",
                  "command": "rm -rf /"
                }
              ],
              "assumptions": [],
              "risks": []
            }
            """;

        bool valid =
            validator.TryValidate(
                DeveloperContractSchema.CreateSchema(),
                json,
                out string error);

        Assert.False(valid);
        Assert.Equal(
            "AI_STRUCTURED_SCHEMA_MISMATCH",
            error);
    }
}
