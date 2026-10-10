using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kronxy.Application.AI;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Context;
using Kronxy.Application.Execution;
using Kronxy.Infrastructure.AI;
using Kronxy.Infrastructure.Artifacts;
using Microsoft.Extensions.Logging;

namespace Kronxy.Infrastructure.Execution;

public sealed class DeveloperExecutionService :
    IDeveloperExecutionService
{
    private const int ConservativeCharactersPerToken = 3;

    private const string SystemInstructions =
        "You are the KRONXY developer model. Return only the smallest complete JSON proposal needed for the task. " +
        "Use at most four changes; omit unnecessary files; keep summary and intent under eight words; use empty assumptions and risks unless essential; never repeat code outside content. " +
        "Emit compact JSON with no markdown or commentary. File content must end immediately after its final required newline; never pad content with repeated blank lines, spaces, or duplicated source. " +
        "Each change content must contain only the file named by relativePath; never concatenate files or add unrelated top-level types. " +
        "Use only candidate file paths from the approved plan. Implement only requested types and members; do not add convenience helpers, extension methods, blocking async calls, examples, or speculative APIs. " +
        "TARGET FILES deterministically state whether each candidate exists and include bounded authoritative content and SHA256 for existing files. Use ReplaceFile for exists=true and copy currentContentSha256 exactly into expectedContentSha256. Use CreateFile with an empty expectedContentSha256 only for exists=false. " +
        "Preserve unrelated markup, code, injected services, usings, bindings, models, actions, and UI. Make the smallest coherent change based on the actual current content; never replace the existing architecture with a guessed pattern. " +
        "Propose only complete file creation or replacement operations grounded in the supplied plan and authorized repository context. " +
        "Do not execute commands, access files, call tools, or modify code. " +
        "KRONXY alone validates and applies your structured proposal.";

    private const string BuildCorrectionInstructions =
        " The previous proposal failed Build. Produce only the smallest correction required by the supplied compiler evidence. " +
        "The compiler diagnostic path is where the inconsistency is observed, not necessarily where the root cause must be fixed. " +
        "Inspect related declarations before selecting the correction target. " +
        "When a diagnostic references a type or symbol declared in another allowed source file, consider correcting the declaration rather than rewriting the diagnostic file. " +
        "Do not add functionality, rewrite unrelated files, expand scope, or create files. " +
        "Use ReplaceFile only and stay within the original Planner candidates. " +
        "Set expectedContentSha256 to an empty placeholder; KRONXY binds the governed workspace hash.";

    private const string HumanReviewCorrectionInstructions =
        " Human Review returned ChangesRequired. Produce only the smallest correction required by the supplied human findings. " +
        "Do not add unrelated functionality, expand scope, or create files. " +
        "Use ReplaceFile only, stay within the original Planner candidates, and preserve existing required behavior. " +
        "Set expectedContentSha256 to an empty placeholder; KRONXY binds the governed workspace hash.";

    private readonly IArtifactReader artifactReader;
    private readonly IContextAiInputBuilder contextBuilder;
    private readonly IAiGateway aiGateway;
    private readonly IDeveloperProposalPolicy proposalPolicy;
    private readonly IDeveloperProposalMetadataBinder metadataBinder;
    private readonly IPlannerPlanPolicy plannerPlanPolicy;
    private readonly IArtifactStore artifactStore;
    private readonly ArtifactStoreOptions artifactOptions;
    private readonly AiGatewayOptions aiOptions;
    private readonly ILogger<DeveloperExecutionService>? logger;

    public DeveloperExecutionService(
        IArtifactReader artifactReader,
        IContextAiInputBuilder contextBuilder,
        IAiGateway aiGateway,
        IDeveloperProposalPolicy proposalPolicy,
        IDeveloperProposalMetadataBinder metadataBinder,
        IArtifactStore artifactStore,
        ArtifactStoreOptions artifactOptions,
        AiGatewayOptions aiOptions,
        ILogger<DeveloperExecutionService>? logger = null,
        IPlannerPlanPolicy? plannerPlanPolicy = null)
    {
        this.artifactReader = artifactReader ??
            throw new ArgumentNullException(nameof(artifactReader));
        this.contextBuilder = contextBuilder ??
            throw new ArgumentNullException(nameof(contextBuilder));
        this.aiGateway = aiGateway ??
            throw new ArgumentNullException(nameof(aiGateway));
        this.proposalPolicy = proposalPolicy ??
            throw new ArgumentNullException(nameof(proposalPolicy));
        this.metadataBinder = metadataBinder ??
            throw new ArgumentNullException(nameof(metadataBinder));
        this.plannerPlanPolicy = plannerPlanPolicy ??
            new PlannerPlanPolicy();
        this.artifactStore = artifactStore ??
            throw new ArgumentNullException(nameof(artifactStore));
        this.artifactOptions = artifactOptions ??
            throw new ArgumentNullException(nameof(artifactOptions));
        this.aiOptions = aiOptions ??
            throw new ArgumentNullException(nameof(aiOptions));
        this.logger = logger;

        this.artifactOptions.Validate();
        this.aiOptions.Validate();

        if (this.aiOptions.DeveloperContextCharacters <= 0 ||
            this.aiOptions.DeveloperContextCharacters >= this.aiOptions.MaxInputCharacters ||
            this.aiOptions.DeveloperFeedbackCharacters <= 0 ||
            this.aiOptions.DeveloperFeedbackCharacters >= this.aiOptions.MaxInputCharacters)
        {
            throw new InvalidOperationException(
                "Developer AI budgets must be positive and below MaxInputCharacters.");
        }
    }

    public async Task<DeveloperExecutionResult> ExecuteAsync(
        DeveloperExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidRequest(request))
        {
            return Failure(
                DeveloperExecutionFailureKind.InvalidRequest,
                "DEVELOPER_INVALID_REQUEST");
        }

        try
        {
            bool isBuildRetry = request.BuildCorrection?.PreviousNoOpPaths.Count > 0;
            Guid evidenceRunId = request.AuthorizedEvidenceRunId ?? request.RunId;
            ArtifactReadResult planArtifact =
                await ReadAsync(
                    request,
                    ArtifactType.PlanningPlan,
                    cancellationToken, evidenceRunId).ConfigureAwait(false);

            if (!planArtifact.IsSuccess)
            {
                return Failure(
                    MapReadFailure(
                        planArtifact.FailureKind,
                        DeveloperExecutionFailureKind
                            .PlanningArtifactReadFailure),
                    planArtifact.ErrorCode);
            }

            PlannerPlan? plan;

            try
            {
                plan = JsonSerializer.Deserialize<PlannerPlan>(
                    planArtifact.Content.Span);
            }
            catch (JsonException)
            {
                return Failure(
                    DeveloperExecutionFailureKind.PlanningPlanInvalid,
                    "DEVELOPER_PLAN_DESERIALIZATION_FAILED");
            }

            if (!IsValidPlan(plan))
            {
                return Failure(
                    DeveloperExecutionFailureKind.PlanningPlanInvalid,
                    "DEVELOPER_PLAN_INVALID");
            }

            PlannerPlanPolicyResult planPolicy =
                plannerPlanPolicy.Validate(
                    plan,
                    request.JobRequest);

            if (!planPolicy.IsSuccess)
            {
                return Failure(
                    DeveloperExecutionFailureKind.PlanningPlanInvalid,
                    "DEVELOPER_PLAN_POLICY_INVALID");
            }

            ArtifactReadResult contextArtifact =
                await ReadAsync(
                    request,
                    ArtifactType.ContextPackage,
                    cancellationToken, evidenceRunId).ConfigureAwait(false);

            if (!contextArtifact.IsSuccess)
            {
                return Failure(
                    MapReadFailure(
                        contextArtifact.FailureKind,
                        DeveloperExecutionFailureKind
                            .ContextArtifactReadFailure),
                    contextArtifact.ErrorCode);
            }

            string planJson = JsonSerializer.Serialize(plan);
            TargetContextResult targetContext = BuildTargetContext(
                contextArtifact.Content,
                plan.CandidateFilesToModify);
            if (!targetContext.IsSuccess)
            {
                return Failure(
                    DeveloperExecutionFailureKind.ContextArtifactReadFailure,
                    targetContext.ErrorCode);
            }

            string targetContextJson = JsonSerializer.Serialize(
                targetContext.Targets);
            string feedbackJson =
                JsonSerializer.Serialize(request.ReviewerFeedback);

            string correctionJson;
            if (request.HumanReviewCorrection is not null)
            {
                correctionJson = CreateHumanReviewCorrectionEvidenceJson(
                    request.HumanReviewCorrection);
            }
            else if (request.BuildCorrection is not null)
            {
                BuildCorrectionEvidenceResult compacted =
                    await BuildCorrectionEvidenceCompactor.CreateAsync(
                        request.BuildCorrection,
                        request.Repository,
                        aiOptions.DeveloperFeedbackCharacters - feedbackJson.Length,
                        plan.CandidateFilesToModify,
                        plan.FilesToInspect,
                        cancellationToken).ConfigureAwait(false);
                if (!compacted.IsSuccess)
                    return Failure(
                        DeveloperExecutionFailureKind.ContextTooLarge,
                        compacted.ErrorCode);
                correctionJson = compacted.Content;
                logger?.LogInformation(
                    "Build correction evidence: diagnostics={DiagnosticsCharacters} chars, relatedDeclarations={RelatedDeclarationsCharacters} chars, envelope={EnvelopeCharacters} chars, feedbackLimit={FeedbackLimit}, unique={UniqueCount}, included={IncludedCount}, duplicates={DuplicateCount}.",
                    compacted.DiagnosticsCharacters,
                    compacted.RelatedDeclarationsCharacters,
                    compacted.Content.Length,
                    aiOptions.DeveloperFeedbackCharacters, compacted.UniqueCount,
                    compacted.IncludedCount, compacted.DuplicateCount);
                if (compacted.IncludedCount < compacted.UniqueCount)
                    logger?.LogWarning(
                        "Build correction diagnostics reduced from {UniqueCount} to {IncludedCount} unique items.",
                        compacted.UniqueCount, compacted.IncludedCount);
            }
            else
            {
                correctionJson = "null";
            }

            if ((long)feedbackJson.Length + correctionJson.Length >
                aiOptions.DeveloperFeedbackCharacters)
            {
                return Failure(
                    DeveloperExecutionFailureKind.ContextTooLarge,
                    "DEVELOPER_REVIEWER_FEEDBACK_LIMIT_EXCEEDED");
            }

            string prefix =
                "JOB REQUEST:\n" + request.JobRequest +
                "\n\nAPPROVED PLANNER PLAN:\n" + planJson +
                "\n\nAUTHORIZED TARGET FILES:\n" + targetContextJson +
                "\n\nAUTHORIZED REVIEWER FEEDBACK:\n" + feedbackJson +
                "\n\nAUTHORIZED CORRECTION EVIDENCE:\n" + correctionJson +
                "\n\nAUTHORIZED REPOSITORY CONTEXT:\n";

            string systemInstructions = SystemInstructions +
                (request.HumanReviewCorrection is not null
                    ? HumanReviewCorrectionInstructions
                    : request.BuildCorrection is null
                        ? string.Empty
                        : BuildCorrectionInstructions);
            if (isBuildRetry)
                systemInstructions +=
                    " The previous correction was a no-op. Inspect the compiler diagnostics, effective source and reference patterns, then make an actual source change in an allowed file. Never repeat unchanged content.";

            string correctionRequirements =
                request.HumanReviewCorrection is not null
                    ? "This is a Human Review correction. Return only ReplaceFile operations. " +
                      "Address every supplied human finding without expanding scope. " +
                      "KRONXY binds each expectedContentSha256 from the governed workspace; use an empty placeholder.\n"
                    : request.BuildCorrection is null
                        ? string.Empty
                        : "This is a Build correction. Return only ReplaceFile operations. " +
                          "The compiler diagnostic path is where the inconsistency is observed, not necessarily where the root cause must be fixed. " +
                          "Inspect related declarations before selecting the correction target. " +
                          "When a diagnostic references a type or symbol declared in another allowed source file, consider correcting the declaration rather than rewriting the diagnostic file. " +
                          "KRONXY binds each expectedContentSha256 from the governed workspace; use an empty placeholder.\n";

            string outputRequirements =
                "\n\nFINAL OUTPUT REQUIREMENTS:\n" +
                "Return only compact JSON matching the schema. " +
                "Do not repeat the repository, plan, request, or code.\n" +
                "Each change content must contain exactly one file. " +
                "Never concatenate candidate files or invent extension classes, helpers, repository operations, examples, or blocking async calls.\n" +
                "Use a summary and intents of at most eight words. " +
                "Use empty assumptions and risks unless a critical item is required. " +
                "For CreateFile use an empty expectedContentSha256.\n" +
                "For every target with exists=true use ReplaceFile and set expectedContentSha256 to its currentContentSha256. " +
                "Never use CreateFile for an existing target or ReplaceFile for a missing target.\n" +
                correctionRequirements +
                "Use only these candidate paths:\n- " +
                string.Join(
                    "\n- ",
                    plan.CandidateFilesToModify) +
                "\nGenerate only the smallest complete source required by the request.\n";

            JsonElement developerSchema =
                DeveloperContractSchema.CreateSchema();

            int schemaCharacters =
                developerSchema.GetRawText().Length;

            int remainingCharacters =
                aiOptions.MaxInputCharacters -
                systemInstructions.Length - prefix.Length -
                outputRequirements.Length -
                schemaCharacters;

            remainingCharacters = Math.Min(
                remainingCharacters,
                aiOptions.DeveloperContextCharacters);

            int tokenWindowCharacters = checked(
                (aiOptions.ContextWindowTokens -
                    aiOptions.DeveloperMaxOutputTokens) *
                ConservativeCharactersPerToken);
            remainingCharacters = Math.Min(
                remainingCharacters,
                tokenWindowCharacters -
                    systemInstructions.Length -
                    prefix.Length -
                    outputRequirements.Length -
                    schemaCharacters);

            if (remainingCharacters <= 0)
            {
                return Failure(
                    DeveloperExecutionFailureKind.ContextTooLarge,
                    "DEVELOPER_INPUT_LIMIT_EXCEEDED");
            }

            ContextAiInputResult context;
            if (targetContext.Targets.Count > 0)
            {
                context = ContextAiInputResult.Success(string.Empty);
            }
            else
            {
                context = await contextBuilder.BuildAsync(
                        new ContextAiInputRequest
                        {
                            PackageContent = contextArtifact.Content,
                            MaxCharacters = remainingCharacters,
                            PriorityPaths = plan.FilesToInspect
                        },
                        cancellationToken).ConfigureAwait(false);
            }

            if (!context.IsSuccess)
            {
                return Failure(
                    MapContextFailure(context.FailureKind),
                    context.ErrorCode);
            }

            string userContent =
                prefix + context.Content + outputRequirements;

            if ((long)systemInstructions.Length +
                    userContent.Length > aiOptions.MaxInputCharacters ||
                (long)systemInstructions.Length + userContent.Length +
                    schemaCharacters > tokenWindowCharacters)
            {
                return Failure(
                    DeveloperExecutionFailureKind.ContextTooLarge,
                    "DEVELOPER_CONTEXT_WINDOW_BUDGET_EXCEEDED");
            }

            if (request.BuildCorrection is not null)
                logger?.LogInformation(
                    "Build correction input: system={SystemCharacters} chars, user={UserCharacters} chars, schema={SchemaCharacters} chars.",
                    systemInstructions.Length, userContent.Length, schemaCharacters);

            AiResponse response =
                await aiGateway.GenerateAsync(
                    new AiRequest
                    {
                        Model = request.BuildCorrection is null
                            ? AiLogicalModel.General
                            : AiLogicalModel.CodingQuality,
                        SystemInstructions = systemInstructions,
                        UserContent = userContent,
                        CorrelationId = request.CorrelationId,
                        InferenceTimeout =
                            aiOptions.DeveloperInferenceTimeout,
                        Generation = new AiGenerationOptions
                        {
                            MaxOutputTokens =
                                aiOptions.DeveloperMaxOutputTokens,
                            Temperature = 0
                        },
                        StructuredOutput = new AiStructuredOutput
                        {
                            Schema = developerSchema
                        }
                    },
                    cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccess)
            {
                ArtifactWriteResult rejectedResponseWritten =
                    await WriteAsync(
                        request,
                        response.ErrorCode.StartsWith(
                            "AI_STRUCTURED_",
                            StringComparison.Ordinal)
                            ? request.HumanReviewCorrection is not null
                                ? ArtifactType.DeveloperHumanReviewCorrectionRejectedStructuredResponse
                                : request.BuildCorrection is null
                                ? ArtifactType.DeveloperRejectedStructuredResponse
                                : isBuildRetry
                                ? ArtifactType.DeveloperBuildCorrectionRetryRejectedStructuredResponse
                                : ArtifactType.DeveloperBuildCorrectionRejectedStructuredResponse
                            : request.HumanReviewCorrection is not null
                                ? ArtifactType.DeveloperHumanReviewCorrectionRejectedResponse
                                : request.BuildCorrection is null
                                ? ArtifactType.DeveloperRejectedResponse
                                : isBuildRetry
                                ? ArtifactType.DeveloperBuildCorrectionRetryRejectedResponse
                                : ArtifactType.DeveloperBuildCorrectionRejectedResponse,
                        JsonSerializer.SerializeToUtf8Bytes(response),
                        cancellationToken).ConfigureAwait(false);

                if (!rejectedResponseWritten.IsSuccess)
                {
                    return ArtifactFailure(
                        rejectedResponseWritten,
                        "DEVELOPER_REJECTED_RESPONSE_ARTIFACT_WRITE_FAILED");
                }

                return Failure(
                    MapAiFailure(response.Status),
                    string.IsNullOrWhiteSpace(response.ErrorCode)
                        ? "DEVELOPER_AI_FAILED"
                        : response.ErrorCode);
            }

            DeveloperProposal? proposal;

            try
            {
                proposal = JsonSerializer
                    .Deserialize<DeveloperProposal>(response.Content);
            }
            catch (JsonException)
            {
                return Failure(
                    DeveloperExecutionFailureKind.AiInvalidResponse,
                    "DEVELOPER_PROPOSAL_DESERIALIZATION_FAILED");
            }

            if (!IsStructurallyValid(proposal))
            {
                return Failure(
                    DeveloperExecutionFailureKind.AiInvalidResponse,
                    "DEVELOPER_PROPOSAL_INVALID");
            }

            string? targetOperationError = ValidateTargetOperations(
                proposal,
                targetContext.Targets);
            if (targetOperationError is not null)
            {
                return await PolicyFailureAsync(
                    request,
                    response,
                    targetOperationError,
                    cancellationToken).ConfigureAwait(false);
            }

            DeveloperProposalMetadataBindingResult binding =
                await metadataBinder.BindAsync(
                    new DeveloperProposalMetadataBindingRequest
                    {
                        JobId = request.JobId,
                        Repository = request.Repository,
                        Proposal = proposal,
                        AllowedPaths = plan.CandidateFilesToModify
                    },
                    cancellationToken).ConfigureAwait(false);

            if (!binding.IsSuccess || binding.Proposal is null)
            {
                return await PolicyFailureAsync(
                    request,
                    response,
                    string.IsNullOrWhiteSpace(binding.ErrorCode)
                        ? "DEVELOPER_METADATA_BINDING_FAILED"
                        : binding.ErrorCode,
                    cancellationToken).ConfigureAwait(false);
            }

            DeveloperProposal normalizedProposal = binding.Proposal;

            DeveloperProposalPolicyResult validated =
                proposalPolicy.Validate(normalizedProposal);

            if (!validated.IsSuccess || validated.Proposal is null)
            {
                return await PolicyFailureAsync(
                    request,
                    response,
                    string.IsNullOrWhiteSpace(validated.ErrorCode)
                        ? "DEVELOPER_PROPOSAL_POLICY_REJECTED"
                        : validated.ErrorCode,
                    cancellationToken).ConfigureAwait(false);
            }

            if (validated.Proposal.Changes.Any(
                    change =>
                        !plan.CandidateFilesToModify.Contains(
                            change.RelativePath,
                            StringComparer.OrdinalIgnoreCase)))
            {
                return await PolicyFailureAsync(
                    request, response,
                    "DEVELOPER_PATH_NOT_IN_PLAN",
                    cancellationToken).ConfigureAwait(false);
            }

            if (request.BuildCorrection is not null &&
                !IsValidBuildCorrectionProposal(
                    validated.Proposal,
                    request.BuildCorrection.OriginalProposal))
            {
                return await PolicyFailureAsync(
                    request, response,
                    "DEVELOPER_BUILD_CORRECTION_INVALID",
                    cancellationToken).ConfigureAwait(false);
            }

            if (request.BuildCorrection is not null &&
                BuildCorrectionNoOpPolicy.IsEntireNoOp(validated.Proposal))
            {
                return await PolicyFailureAsync(
                    request, response,
                    "DEVELOPER_BUILD_CORRECTION_NO_OP",
                    cancellationToken).ConfigureAwait(false);
            }

            if (request.HumanReviewCorrection is not null &&
                !IsValidHumanReviewCorrectionProposal(
                    validated.Proposal,
                    request.HumanReviewCorrection.CurrentProposal,
                    request.HumanReviewCorrection.HumanReviewEvidence))
            {
                return await PolicyFailureAsync(
                    request, response,
                    "DEVELOPER_HUMAN_REVIEW_CORRECTION_INVALID",
                    cancellationToken).ConfigureAwait(false);
            }

            ArtifactWriteResult proposalWritten =
                await WriteAsync(
                    request,
                    request.HumanReviewCorrection is not null
                        ? ArtifactType.DeveloperHumanReviewCorrectionProposal
                        : request.BuildCorrection is null
                        ? ArtifactType.DeveloperProposal
                        : isBuildRetry
                        ? ArtifactType.DeveloperBuildCorrectionRetryProposal
                        : ArtifactType.DeveloperBuildCorrectionProposal,
                    JsonSerializer.SerializeToUtf8Bytes(normalizedProposal),
                    cancellationToken).ConfigureAwait(false);

            if (!proposalWritten.IsSuccess ||
                proposalWritten.Artifact is null)
            {
                return ArtifactFailure(proposalWritten,
                    "DEVELOPER_PROPOSAL_ARTIFACT_WRITE_FAILED");
            }

            ArtifactWriteResult responseWritten =
                await WriteAsync(
                    request,
                    request.HumanReviewCorrection is not null
                        ? ArtifactType.DeveloperHumanReviewCorrectionResponse
                        : request.BuildCorrection is null
                        ? ArtifactType.DeveloperResponse
                        : isBuildRetry
                        ? ArtifactType.DeveloperBuildCorrectionRetryResponse
                        : ArtifactType.DeveloperBuildCorrectionResponse,
                    JsonSerializer.SerializeToUtf8Bytes(response),
                    cancellationToken).ConfigureAwait(false);

            if (!responseWritten.IsSuccess ||
                responseWritten.Artifact is null)
            {
                return ArtifactFailure(responseWritten,
                    "DEVELOPER_RESPONSE_ARTIFACT_WRITE_FAILED");
            }

            return DeveloperExecutionResult.Success(
                new DeveloperExecutionReport
                {
                    JobId = request.JobId,
                    RunId = request.RunId,
                    LogicalModel = response.LogicalModel,
                    Provider = response.Provider,
                    PhysicalModel = response.PhysicalModel,
                    Duration = response.Duration,
                    TerminationReason =
                        response.TerminationReason.ToString(),
                    PromptTokens = response.Usage.PromptTokens,
                    CompletionTokens = response.Usage.CompletionTokens
                },
                responseWritten.Artifact,
                proposalWritten.Artifact,
                validated.Proposal);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return Failure(
                DeveloperExecutionFailureKind.Cancelled,
                "DEVELOPER_CANCELLED");
        }
        catch (Exception exception)
            when (exception is not OutOfMemoryException and not
                StackOverflowException)
        {
            return Failure(
                DeveloperExecutionFailureKind.InternalFailure,
                "DEVELOPER_INTERNAL_FAILURE");
        }
    }

    private static TargetContextResult BuildTargetContext(
        ReadOnlyMemory<byte> packageContent,
        IReadOnlyList<string> candidatePaths)
    {
        const int maxTargetBytes = 64 * 1024;
        const int maxTotalTargetBytes = 128 * 1024;

        try
        {
            using var stream = new MemoryStream(packageContent.ToArray(), writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            ZipArchiveEntry? manifestEntry = archive.GetEntry("manifest.json");
            if (manifestEntry is null)
            {
                return TargetContextResult.Success([]);
            }

            ManifestModel? manifest;
            using (Stream manifestStream = manifestEntry.Open())
            {
                manifest = JsonSerializer.Deserialize<ManifestModel>(manifestStream);
            }

            if (manifest is null)
            {
                return TargetContextResult.Failure("DEVELOPER_TARGET_MANIFEST_INVALID");
            }

            Dictionary<string, ManifestEntry> inventory = manifest.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
                .ToDictionary(entry => entry.Path, StringComparer.Ordinal);
            var targets = new List<DeveloperTargetContext>();
            int totalBytes = 0;

            foreach (string path in candidatePaths.Distinct(StringComparer.Ordinal))
            {
                if (!inventory.TryGetValue(path, out ManifestEntry? item))
                {
                    targets.Add(new DeveloperTargetContext(
                        path, false, null, string.Empty,
                        "WritableCandidate", "PlanningPlan"));
                    continue;
                }

                if (item.SizeBytes < 0 || item.SizeBytes > maxTargetBytes ||
                    totalBytes + item.SizeBytes > maxTotalTargetBytes ||
                    item.Sha256.Length != 64)
                {
                    return TargetContextResult.Failure(
                        "DEVELOPER_AUTHORITATIVE_TARGET_TOO_LARGE");
                }

                ZipArchiveEntry? sourceEntry = archive.GetEntry(path);
                if (sourceEntry is null || sourceEntry.Length != item.SizeBytes)
                {
                    return TargetContextResult.Failure(
                        "DEVELOPER_AUTHORITATIVE_TARGET_INVALID");
                }

                byte[] contentBytes;
                using (Stream source = sourceEntry.Open())
                using (var buffer = new MemoryStream())
                {
                    source.CopyTo(buffer);
                    contentBytes = buffer.ToArray();
                }

                string actualSha = Convert.ToHexString(
                    SHA256.HashData(contentBytes)).ToLowerInvariant();
                if (!string.Equals(actualSha, item.Sha256, StringComparison.Ordinal))
                {
                    return TargetContextResult.Failure(
                        "DEVELOPER_AUTHORITATIVE_TARGET_HASH_INVALID");
                }

                string content = new UTF8Encoding(false, true)
                    .GetString(contentBytes);
                targets.Add(new DeveloperTargetContext(
                    path, true, content, actualSha,
                    "WritableCandidate", "PlanningPlan+ContextManifest"));
                totalBytes += contentBytes.Length;
            }

            return TargetContextResult.Success(targets);
        }
        catch (Exception exception) when (exception is InvalidDataException or
            JsonException or DecoderFallbackException or ArgumentException)
        {
            // Legacy test packages and recovery evidence may not carry a readable
            // inventory. Preserve their existing behavior while real packages fail
            // closed once a manifest is present.
            return TargetContextResult.Success([]);
        }
    }

    private static string? ValidateTargetOperations(
        DeveloperProposal proposal,
        IReadOnlyList<DeveloperTargetContext> targets)
    {
        if (targets.Count == 0)
        {
            return null;
        }

        Dictionary<string, DeveloperTargetContext> byPath = targets
            .ToDictionary(target => target.RelativePath, StringComparer.OrdinalIgnoreCase);
        foreach (DeveloperChangeOperation change in proposal.Changes)
        {
            if (!byPath.TryGetValue(change.RelativePath, out DeveloperTargetContext? target))
            {
                return "DEVELOPER_PATH_NOT_IN_TARGET_CONTEXT";
            }

            if (target.Exists && change.Operation != DeveloperChangeOperationType.ReplaceFile)
            {
                return "DEVELOPER_CREATE_EXISTING_FILE_REJECTED";
            }

            if (!target.Exists && change.Operation != DeveloperChangeOperationType.CreateFile)
            {
                return "DEVELOPER_REPLACE_MISSING_FILE_REJECTED";
            }

            if (target.Exists && !string.Equals(
                    change.ExpectedContentSha256,
                    target.CurrentContentSha256,
                    StringComparison.Ordinal))
            {
                return "DEVELOPER_EXPECTED_CONTENT_SHA_INVALID";
            }
        }

        return null;
    }

    private Task<ArtifactReadResult> ReadAsync(
        DeveloperExecutionRequest request,
        ArtifactType type,
        CancellationToken cancellationToken,
        Guid? runId = null) =>
        artifactReader.ReadAsync(
            new ArtifactReadRequest
            {
                JobId = request.JobId,
                RunId = runId ?? request.RunId,
                ArtifactType = type,
                MaxBytes = artifactOptions.MaxArtifactBytes,
                CorrelationId = request.CorrelationId
            },
        cancellationToken);

    private sealed record DeveloperTargetContext(
        string RelativePath,
        bool Exists,
        string? CurrentContent,
        string CurrentContentSha256,
        string Role,
        string AuthorizationSource);

    private sealed record TargetContextResult(
        bool IsSuccess,
        IReadOnlyList<DeveloperTargetContext> Targets,
        string ErrorCode)
    {
        public static TargetContextResult Success(
            IReadOnlyList<DeveloperTargetContext> targets) =>
            new(true, targets, string.Empty);

        public static TargetContextResult Failure(string errorCode) =>
            new(false, [], errorCode);
    }

    private sealed record ManifestModel
    {
        [JsonPropertyName("entries")]
        public IReadOnlyList<ManifestEntry> Entries { get; init; } = [];
    }

    private sealed record ManifestEntry
    {
        [JsonPropertyName("path")]
        public string Path { get; init; } = string.Empty;

        [JsonPropertyName("sizeBytes")]
        public int SizeBytes { get; init; }

        [JsonPropertyName("sha256")]
        public string Sha256 { get; init; } = string.Empty;
    }

    private Task<ArtifactWriteResult> WriteAsync(
        DeveloperExecutionRequest request,
        ArtifactType type,
        byte[] content,
        CancellationToken cancellationToken) =>
        artifactStore.WriteAsync(
            new ArtifactWriteRequest
            {
                JobId = request.JobId,
                RunId = request.RunId,
                ArtifactType = type,
                Content = content,
                CorrelationId = request.CorrelationId
            },
            cancellationToken);

    private async Task<DeveloperExecutionResult> PolicyFailureAsync(
        DeveloperExecutionRequest request,
        AiResponse response,
        string errorCode,
        CancellationToken cancellationToken)
    {
        ArtifactWriteResult evidenceWritten = await WriteAsync(
            request,
            request.HumanReviewCorrection is not null
                ? ArtifactType.DeveloperHumanReviewCorrectionRejectedResponse
                : request.BuildCorrection is null
                    ? ArtifactType.DeveloperRejectedResponse
                    : request.BuildCorrection.PreviousNoOpPaths.Count > 0
                        ? ArtifactType.DeveloperBuildCorrectionRetryRejectedResponse
                    : ArtifactType.DeveloperBuildCorrectionRejectedResponse,
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                PolicyErrorCode = errorCode,
                Response = response
            }),
            cancellationToken).ConfigureAwait(false);

        if (!evidenceWritten.IsSuccess)
        {
            return ArtifactFailure(
                evidenceWritten,
                "DEVELOPER_REJECTED_RESPONSE_ARTIFACT_WRITE_FAILED");
        }

        return Failure(
            DeveloperExecutionFailureKind.PolicyRejected,
            errorCode);
    }

    private DeveloperExecutionResult ArtifactFailure(
        ArtifactWriteResult result,
        string fallbackCode) =>
        Failure(
            result.FailureKind == ArtifactStoreFailureKind.Cancelled
                ? DeveloperExecutionFailureKind.Cancelled
                : DeveloperExecutionFailureKind.ArtifactWriteFailure,
            string.IsNullOrWhiteSpace(result.ErrorCode)
                ? fallbackCode
                : result.ErrorCode);

    private static bool IsValidRequest(
        DeveloperExecutionRequest? request) =>
        request is not null &&
        request.JobId != Guid.Empty &&
        request.RunId != Guid.Empty &&
        request.Repository is not null &&
        request.Repository.JobId == request.JobId &&
        !string.IsNullOrWhiteSpace(request.JobRequest) &&
        request.CorrelationId.IndexOfAny(['\0', '\r', '\n']) < 0 &&
        !(request.BuildCorrection is not null &&
          request.HumanReviewCorrection is not null) &&
        (request.BuildCorrection is null ||
            IsValidBuildCorrectionContext(request)) &&
        (request.HumanReviewCorrection is null ||
            IsValidHumanReviewCorrectionContext(request));

    private static bool IsValidHumanReviewCorrectionContext(
        DeveloperExecutionRequest request) =>
        request.HumanReviewCorrection is not null &&
        request.HumanReviewCorrection.CurrentProposal.Changes.Count > 0 &&
        request.HumanReviewCorrection.BuildReport.JobId == request.JobId &&
        request.HumanReviewCorrection.BuildReport.RunId == request.RunId &&
        request.HumanReviewCorrection.BuildReport.IsSuccess &&
        request.HumanReviewCorrection.TestReport.JobId == request.JobId &&
        request.HumanReviewCorrection.TestReport.RunId == request.RunId &&
        request.HumanReviewCorrection.TestReport.IsSuccess &&
        request.HumanReviewCorrection.ReviewerReview.Decision ==
            ReviewerDecision.Approved &&
        request.HumanReviewCorrection.HumanReviewEvidence.JobId == request.JobId &&
        request.HumanReviewCorrection.HumanReviewEvidence.RunId == request.RunId &&
        request.HumanReviewCorrection.HumanReviewEvidence.Decision ==
            HumanReviewDecision.ChangesRequired &&
        request.HumanReviewCorrection.HumanReviewEvidence.RequiredCorrections.Count > 0 &&
        request.HumanReviewCorrection.HumanReviewEvidence.RequiredCorrections.All(correction =>
            request.HumanReviewCorrection.CurrentProposal.Changes.Any(change =>
                string.Equals(change.RelativePath, correction.RelativePath,
                    StringComparison.OrdinalIgnoreCase)));

    private static bool IsValidBuildCorrectionContext(
        DeveloperExecutionRequest request) =>
        request.BuildCorrection is not null &&
        request.BuildCorrection.OriginalProposal is not null &&
        request.BuildCorrection.OriginalProposal.Changes.Count > 0 &&
        request.BuildCorrection.PreviousNoOpPaths.All(path =>
            request.BuildCorrection.OriginalProposal.Changes.Any(change =>
                string.Equals(change.RelativePath, path,
                    StringComparison.OrdinalIgnoreCase))) &&
        request.BuildCorrection.FailedBuildReport is not null &&
        request.BuildCorrection.FailedBuildReport.JobId == request.JobId &&
        request.BuildCorrection.FailedBuildReport.RunId == request.RunId &&
        !request.BuildCorrection.FailedBuildReport.IsSuccess &&
        !string.IsNullOrWhiteSpace(
            request.BuildCorrection.BuildStandardOutput);

    private static bool IsValidBuildCorrectionProposal(
        ValidatedDeveloperProposal correction,
        ValidatedDeveloperProposal original)
    {
        HashSet<string> originalPaths = original.Changes
            .Select(change => change.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return correction.Changes.Count > 0 && correction.Changes.All(change =>
            change.Operation == DeveloperChangeOperationType.ReplaceFile &&
            originalPaths.Contains(change.RelativePath));
    }

    private static bool IsValidHumanReviewCorrectionProposal(
        ValidatedDeveloperProposal correction,
        ValidatedDeveloperProposal current,
        HumanReviewCorrectionEvidence evidence)
    {
        if (!IsValidBuildCorrectionProposal(correction, current)) return false;

        HashSet<string> requiredPaths = evidence.RequiredCorrections
            .Select(item => item.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> correctionPaths = correction.Changes
            .Select(item => item.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return requiredPaths.SetEquals(correctionPaths);
    }

    private static string CreateHumanReviewCorrectionEvidenceJson(
        DeveloperHumanReviewCorrectionContext correction)
    {
        return JsonSerializer.Serialize(new
        {
            HumanReviewDecision = correction.HumanReviewEvidence.Decision,
            RequiredCorrections =
                correction.HumanReviewEvidence.RequiredCorrections,
            CurrentProposal = correction.CurrentProposal,
            BuildReport = correction.BuildReport,
            TestReport = correction.TestReport,
            PreviousReviewer = correction.ReviewerReview
        });
    }

    private static bool IsValidPlan(
        [NotNullWhen(true)] PlannerPlan? plan) =>
        plan is not null &&
        plan.Objective is not null &&
        plan.FilesToInspect is not null &&
        plan.CandidateFilesToModify is not null &&
        plan.Strategy is not null &&
        plan.AcceptanceCriteria is not null &&
        plan.Risks is not null &&
        plan.ExpectedTests is not null &&
        plan.Assumptions is not null &&
        plan.Uncertainties is not null;

    private static bool IsStructurallyValid(
        [NotNullWhen(true)] DeveloperProposal? proposal) =>
        proposal is not null &&
        proposal.Summary is not null &&
        proposal.Changes is not null &&
        proposal.Assumptions is not null &&
        proposal.Risks is not null &&
        proposal.Changes.All(change =>
            change is not null &&
            change.RelativePath is not null &&
            change.Intent is not null &&
            change.Content is not null &&
            change.ExpectedContentSha256 is not null);

    private static DeveloperExecutionFailureKind MapReadFailure(
        ArtifactReadFailureKind kind,
        DeveloperExecutionFailureKind fallback) =>
        kind switch
        {
            ArtifactReadFailureKind.Cancelled =>
                DeveloperExecutionFailureKind.Cancelled,
            ArtifactReadFailureKind.TooLarge =>
                DeveloperExecutionFailureKind.ContextTooLarge,
            _ => fallback
        };

    private static DeveloperExecutionFailureKind MapContextFailure(
        ContextAiInputFailureKind kind) =>
        kind switch
        {
            ContextAiInputFailureKind.Cancelled =>
                DeveloperExecutionFailureKind.Cancelled,
            ContextAiInputFailureKind.PackageTooLarge or
            ContextAiInputFailureKind.EntryTooLarge or
            ContextAiInputFailureKind.ContentTooLarge =>
                DeveloperExecutionFailureKind.ContextTooLarge,
            ContextAiInputFailureKind.InvalidPackage or
            ContextAiInputFailureKind.InvalidEncoding =>
                DeveloperExecutionFailureKind.ContextPackageInvalid,
            _ => DeveloperExecutionFailureKind.InternalFailure
        };

    private static DeveloperExecutionFailureKind MapAiFailure(
        AiOperationStatus status) =>
        status switch
        {
            AiOperationStatus.Rejected =>
                DeveloperExecutionFailureKind.AiRejected,
            AiOperationStatus.TimedOut =>
                DeveloperExecutionFailureKind.AiTimedOut,
            AiOperationStatus.Cancelled =>
                DeveloperExecutionFailureKind.AiCancelled,
            AiOperationStatus.ProviderUnavailable =>
                DeveloperExecutionFailureKind.AiUnavailable,
            AiOperationStatus.ProviderError =>
                DeveloperExecutionFailureKind.AiProviderError,
            AiOperationStatus.InvalidResponse =>
                DeveloperExecutionFailureKind.AiInvalidResponse,
            _ => DeveloperExecutionFailureKind.InternalFailure
        };

    private DeveloperExecutionResult Failure(
        DeveloperExecutionFailureKind kind,
        string errorCode)
    {
        logger?.LogWarning(
            "Developer execution failed. FailureKind={FailureKind} ErrorCode={ErrorCode}",
            kind,
            errorCode);

        return DeveloperExecutionResult.Failure(kind, errorCode);
    }
}
