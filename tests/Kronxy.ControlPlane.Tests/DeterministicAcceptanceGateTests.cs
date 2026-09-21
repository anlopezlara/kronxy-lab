using System.Security.Cryptography;
using System.Text;
using Kronxy.Application.Execution;
using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class DeterministicAcceptanceGateTests
{
    private const string ModelPath = "src/Domain/Modules/Module.cs";
    private const string ErrorsPath = "src/Domain/Modules/ModuleErrors.cs";
    private const string RepositoryPath = "src/Domain/Modules/IModuleRepository.cs";
    private static readonly Guid JobId = Guid.NewGuid();
    private static readonly Guid RunId = Guid.NewGuid();

    [Fact]
    public void Existing_properties_and_create_method_pass()
    {
        DeterministicAcceptanceGateResult result = Evaluate(Model());
        AssertPass(result, "ProjectId");
        AssertPass(result, "Create");
    }

    [Fact]
    public void Missing_project_id_fails()
    {
        DeterministicAcceptanceGateResult result = Evaluate(
            Model().Replace("public Guid ProjectId { get; private set; }", string.Empty));
        AssertFail(result, "ProjectId");
    }

    [Fact]
    public void Repository_contract_methods_pass()
    {
        DeterministicAcceptanceGateResult result = Evaluate(Model());
        AssertPass(result, "GetByIdAsync");
        AssertPass(result, "Add");
    }

    [Fact]
    public void Five_error_codes_pass()
    {
        DeterministicAcceptanceGateResult result = Evaluate(Model());
        foreach (string code in Codes()) AssertPass(result, code);
    }

    [Fact]
    public void Missing_error_code_fails()
    {
        DeterministicAcceptanceGateResult result = Evaluate(
            Model(), Errors().Replace("\"Module.ParentInactive\"", "\"other\""));
        AssertFail(result, "Module.ParentInactive");
    }

    [Fact]
    public void Reviewer_missing_claim_is_recorded_and_does_not_reverse_pass()
    {
        DeterministicAcceptanceGateResult gate = Evaluate(Model());
        ReviewerReview review = Review(
            "ProjectId is missing from the entity.", "Add missing ProjectId.");

        ReviewerReview reconciled = ReviewerExecutionService
            .ReconcileDeterministicEvidence(review, gate);

        Assert.Equal(ReviewerDecision.Approved, reconciled.Decision);
        Assert.True(reconciled.ReviewerContradictsDeterministicEvidence);
        Assert.Empty(reconciled.Findings);
        Assert.Empty(reconciled.RequiredCorrections);
        AssertPass(reconciled.DeterministicAcceptanceGate!, "ProjectId");
    }

    [Fact]
    public void Semantic_finding_remains_changes_required()
    {
        ReviewerReview review = Review(
            "The aggregate design violates the established architecture.",
            "Align the aggregate with the architecture.");

        ReviewerReview reconciled = ReviewerExecutionService
            .ReconcileDeterministicEvidence(review, Evaluate(Model()));

        Assert.Equal(ReviewerDecision.ChangesRequired, reconciled.Decision);
        Assert.False(reconciled.ReviewerContradictsDeterministicEvidence);
        Assert.Single(reconciled.Findings);
    }

    [Fact]
    public void Source_hash_mismatch_fails_closed()
    {
        ReviewerEffectiveSourceSnapshot snapshot = Snapshot(Model());
        ReviewerEffectiveSourceFile first = snapshot.Files[0] with { Sha256 = new string('0', 64) };
        snapshot = snapshot with { Files = [first, .. snapshot.Files.Skip(1)] };

        DeterministicAcceptanceGateResult result = new DeterministicAcceptanceGate()
            .Evaluate(Request(), Plan(), snapshot);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Criteria, criterion =>
            criterion.Status == DeterministicCriterionStatus.Unevaluable);
    }

    [Fact]
    public void Unresolvable_deterministic_section_fails_closed()
    {
        PlannerPlan plan = Plan() with { CandidateFilesToModify = [ErrorsPath] };
        ReviewerEffectiveSourceSnapshot snapshot = new(JobId, RunId,
            DeveloperProposalLineage.Original, [Source(ErrorsPath, Errors())]);

        DeterministicAcceptanceGateResult result = new DeterministicAcceptanceGate()
            .Evaluate(Request(), plan, snapshot);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Criteria, criterion =>
            criterion.Criterion.Expected == string.Empty &&
            criterion.Status != DeterministicCriterionStatus.Pass);
    }

    [Fact]
    public void Failed_gate_overrides_model_approval()
    {
        DeterministicAcceptanceGateResult gate = Evaluate(
            Model().Replace("public Guid ProjectId { get; private set; }", string.Empty));
        ReviewerReview approved = new()
        {
            Decision = ReviewerDecision.Approved,
            Findings = [], RequiredCorrections = [],
            RiskAssessment = "low", Summary = "ok"
        };

        ReviewerReview reconciled = ReviewerExecutionService
            .ReconcileDeterministicEvidence(approved, gate);

        Assert.Equal(ReviewerDecision.ChangesRequired, reconciled.Decision);
        Assert.Contains(reconciled.Findings, finding =>
            finding.Category == "DeterministicAcceptance");
    }

    private static DeterministicAcceptanceGateResult Evaluate(
        string model, string? errors = null) => new DeterministicAcceptanceGate()
        .Evaluate(Request(), Plan(), Snapshot(model, errors));

    private static ReviewerEffectiveSourceSnapshot Snapshot(
        string model, string? errors = null) => new(JobId, RunId,
        DeveloperProposalLineage.Original,
        [Source(ModelPath, model), Source(ErrorsPath, errors ?? Errors()),
         Source(RepositoryPath, Repository())]);

    private static ReviewerEffectiveSourceFile Source(string path, string content)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        return new(path,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            bytes.LongLength, content);
    }

    private static PlannerPlan Plan() => new()
    {
        Objective = "Create module", FilesToInspect = [],
        CandidateFilesToModify = [ModelPath, ErrorsPath, RepositoryPath],
        Strategy = "Follow conventions",
        AcceptanceCriteria = ["ProjectId is mandatory", "architecture follows conventions"],
        Risks = [], ExpectedTests = [], Assumptions = [], Uncertainties = []
    };

    private static string Request() => """
        REQUIRED FIRST-CLASS FIELDS
        Guid Id
        Guid ProjectId
        string Name
        string? Description
        bool IsActive
        DateTime CreatedOnUtc
        DateTime? UpdatedOnUtc
        DateTime? DeletedOnUtc

        DOMAIN CREATION
        Module.Create(

        DOMAIN UPDATE
        Update(

        SOFT DELETE
        Activate(utcNow)
        Deactivate(utcNow)

        DOMAIN ERRORS
        Module.NotFound
        Module.WrongParent
        Module.ParentNotFound
        Module.ParentInactive
        Module.HasActiveChildren

        REPOSITORY CONTRACT
        Task<Module?> GetByIdAsync(
        void Add(Module module);
        """;

    private static string Model() => """
        public sealed class Module
        {
            public Guid Id { get; private set; }
            public Guid ProjectId { get; private set; }
            public string Name { get; private set; }
            public string? Description { get; private set; }
            public bool IsActive { get; private set; }
            public DateTime CreatedOnUtc { get; private set; }
            public DateTime? UpdatedOnUtc { get; private set; }
            public DateTime? DeletedOnUtc { get; private set; }
            public static Module Create() { return new(); }
            public void Update() { }
            public void Activate() { }
            public void Deactivate() { }
        }
        """;

    private static string Errors() =>
        "public static class ModuleErrors { " +
        string.Join(' ', Codes().Select(code => $"public const string X{code.Length} = \"{code}\";")) + " }";
    private static string Repository() => """
        public interface IModuleRepository
        {
            Task<Module?> GetByIdAsync(Guid id);
            void Add(Module module);
        }
        """;
    private static string[] Codes() =>
        ["Module.NotFound", "Module.WrongParent", "Module.ParentNotFound",
         "Module.ParentInactive", "Module.HasActiveChildren"];

    private static ReviewerReview Review(string finding, string correction) => new()
    {
        Decision = ReviewerDecision.ChangesRequired,
        Findings = [new ReviewerFinding { Category = "review", Description = finding }],
        RequiredCorrections = [new ReviewerCorrection
            { RelativePath = ModelPath, Instruction = correction }],
        RiskAssessment = "medium", Summary = "changes"
    };

    private static void AssertPass(DeterministicAcceptanceGateResult result, string expected) =>
        Assert.Contains(result.Criteria, criterion =>
            criterion.Criterion.Expected == expected &&
            criterion.Status == DeterministicCriterionStatus.Pass);
    private static void AssertFail(DeterministicAcceptanceGateResult result, string expected) =>
        Assert.Contains(result.Criteria, criterion =>
            criterion.Criterion.Expected == expected &&
            criterion.Status == DeterministicCriterionStatus.Fail);
}
