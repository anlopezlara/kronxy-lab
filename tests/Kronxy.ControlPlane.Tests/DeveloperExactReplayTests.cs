using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kronxy.Application.AI;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Infrastructure.AI;
using Kronxy.Infrastructure.AI.Ollama;
using Kronxy.Infrastructure.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Kronxy.ControlPlane.Tests;

public sealed class DeveloperExactReplayTests(ITestOutputHelper output)
{
    private const string TargetPath =
        "src/Kronxy.Web/Components/Pages/JobDetail.razor";
    private const string PhysicalModel = "qwen2.5-coder:14b";
    [Fact]
    public void Deterministic_fixture_uses_production_builder_and_emits_safe_fingerprint()
    {
        ReplayFixture fixture = CreateFixture();
        DeveloperAiRequestFingerprint fingerprint = fixture.Fingerprint;
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(
            OllamaProvider.CreateRequestPayload(
                fixture.Request, PhysicalModel, fixture.Options),
            OllamaProvider.RequestJsonOptions);
        string serializedText = Encoding.UTF8.GetString(serialized);

        Assert.Equal(AiLogicalModel.CodingQuality, fixture.Request.Model);
        Assert.Equal(PhysicalModel, fingerprint.Model);
        Assert.Equal(2_048, fingerprint.NumPredict);
        Assert.Equal(16_384, fingerprint.NumContext);
        Assert.Equal(0, fingerprint.Temperature);
        Assert.True(fingerprint.FormatSchemaPresent);
        Assert.False(fingerprint.Think);
        Assert.Equal(TargetPath, fingerprint.TargetPath);
        Assert.Equal("LocalizedPatch", fingerprint.OperationType);
        Assert.Equal(fixture.Source.Length, fingerprint.SourceContentCharacters);
        Assert.Equal(0, fingerprint.PriorTargetEffectCharacters);
        Assert.Contains(
            JsonSerializer.Serialize(
                fixture.Source,
                DeveloperExecutionService.PromptJsonOptions),
            fixture.Request.UserContent);
        Assert.Equal(
            DeveloperExecutionService.InitialSchema(
                new DeveloperTargetContext(
                    TargetPath, true, fixture.Source, Sha256(fixture.Source),
                    "WritableCandidate", "PlanningPlan+ContextManifest"))
                .GetRawText(),
            fixture.Request.StructuredOutput!.Schema.GetRawText());
        Assert.DoesNotContain(fixture.Source, JsonSerializer.Serialize(fingerprint));
        Assert.DoesNotContain("JOB REQUEST", JsonSerializer.Serialize(fingerprint));
        Assert.DoesNotContain("\"jobId\":", serializedText);
        Assert.DoesNotContain("\"runId\":", serializedText);
        Assert.DoesNotContain("\"correlationId\":", serializedText);
        Assert.Equal(serialized.Length, fingerprint.SerializedRequestBytes);
        Assert.Equal(Sha256(fixture.Source),
            Sha256(File.ReadAllText(fixture.SourcePath)));
    }

    [Fact]
    public async Task Exact_direct_replay_is_opt_in_read_only_and_reports_native_metrics()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("KRONXY_RUN_EXACT_OLLAMA_REPLAY"),
                "1", StringComparison.Ordinal))
        {
            return;
        }

        ReplayFixture fixture = CreateFixture();
        int estimatedInputTokens = DivideRoundUp(
            fixture.Request.SystemInstructions.Length +
            fixture.Request.UserContent.Length +
            fixture.Request.StructuredOutput!.Schema.GetRawText().Length,
            3);
        if (string.Equals(
                Environment.GetEnvironmentVariable("KRONXY_REPLAY_METADATA_ONLY"),
                "1", StringComparison.Ordinal))
        {
            output.WriteLine(JsonSerializer.Serialize(new
            {
                Fingerprint = fixture.Fingerprint,
                KronxyEstimatedInputTokens = estimatedInputTokens
            }));
            return;
        }

        string sourceHashBefore = Sha256(fixture.Source);
        ReplayResult withSchema = await ReplayAsync(
            fixture.Request, fixture.Options);
        ReplayResult withoutSchema = await ReplayAsync(
            fixture.Request with { StructuredOutput = null },
            fixture.Options);
        string sourceHashAfter = Sha256(File.ReadAllText(fixture.SourcePath));

        Assert.Equal(sourceHashBefore, sourceHashAfter);
        Assert.Equal(2_048, fixture.Request.Generation.MaxOutputTokens);
        Assert.True(withSchema.TimedOut || withSchema.Model == PhysicalModel);
        Assert.True(withoutSchema.TimedOut || withoutSchema.Model == PhysicalModel);

        output.WriteLine(JsonSerializer.Serialize(new
        {
            Fingerprint = fixture.Fingerprint,
            KronxyEstimatedInputTokens = estimatedInputTokens,
            WithSchema = withSchema,
            WithoutSchema = withoutSchema,
            RepositorySourceUnchanged = true
        }));
    }

    [Fact]
    public async Task Streaming_prompt_ablation_reports_progress_and_validity()
    {
        string? variant = Environment.GetEnvironmentVariable(
            "KRONXY_REPLAY_STREAM_VARIANT");
        if (string.IsNullOrWhiteSpace(variant))
            return;

        ReplayFixture fixture = CreateFixture();
        AiRequest request = CreateVariant(fixture, variant);
        StreamingReplayResult result = await ReplayStreamingAsync(
            request, fixture.Options, fixture.Source);
        output.WriteLine(JsonSerializer.Serialize(new
        {
            Variant = variant,
            SystemCharacters = request.SystemInstructions.Length,
            UserCharacters = request.UserContent.Length,
            RequestBytes = JsonSerializer.SerializeToUtf8Bytes(
                OllamaProvider.CreateRequestPayload(
                    request, PhysicalModel, fixture.Options),
                OllamaProvider.RequestJsonOptions).Length,
            Result = result
        }));
    }

    private static AiRequest CreateVariant(
        ReplayFixture fixture,
        string variant)
    {
        string jobRequest = Section(
            fixture.Prompt.Prefix,
            "JOB REQUEST:\n",
            "\n\nAPPROVED PLANNER PLAN:\n");
        string compactUser =
            "REQUEST:\n" + jobRequest +
            "\n\nAUTHORIZED TARGET (includes governed source and base SHA):\n" +
            fixture.Prompt.TargetJson +
            "\n\nOUTPUT:\nReturn one LocalizedPatch change for the target path. " +
            "Use the smallest exact edits that implement the request.";

        return variant switch
        {
            "current" => fixture.Request,
            "compact-system" => fixture.Request with
            {
                SystemInstructions = DeveloperExecutionService.LocalizedPatchSystemInstructions
            },
            "no-plan" => fixture.Request with
            {
                UserContent = compactUser
            },
            "compact-all" => fixture.Request with
            {
                SystemInstructions = DeveloperExecutionService.LocalizedPatchSystemInstructions,
                UserContent = compactUser
            },
            "minimal-task" => fixture.Request with
            {
                SystemInstructions = DeveloperExecutionService.LocalizedPatchSystemInstructions,
                UserContent =
                    "Implement this request using one LocalizedPatch JSON change:\n" +
                    jobRequest + "\n\nTARGET:\n" + fixture.Prompt.TargetJson
            },
            _ => throw new InvalidOperationException(
                $"Unknown replay variant: {variant}")
        };
    }

    private static string Section(
        string value,
        string startMarker,
        string endMarker)
    {
        int start = value.IndexOf(startMarker, StringComparison.Ordinal) +
            startMarker.Length;
        int end = value.IndexOf(endMarker, start, StringComparison.Ordinal);
        return value[start..end];
    }

    private static async Task<StreamingReplayResult> ReplayStreamingAsync(
        AiRequest request,
        AiGatewayOptions options,
        string source)
    {
        JsonNode payload = JsonSerializer.SerializeToNode(
            OllamaProvider.CreateRequestPayload(request, PhysicalModel, options),
            OllamaProvider.RequestJsonOptions)!;
        payload["stream"] = true;
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            payload, OllamaProvider.RequestJsonOptions);
        using var client = new HttpClient
        {
            BaseAddress = new Uri("http://kronxy-server:11434/"),
            Timeout = TimeSpan.FromMinutes(5)
        };
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        Stopwatch stopwatch = Stopwatch.StartNew();
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/chat")
        {
            Content = content
        };
        try
        {
            using HttpResponseMessage response = await client.SendAsync(
                message, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using Stream stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            var generated = new StringBuilder();
            double? firstToken = null;
            int chunks = 0;
            int at60 = 0;
            int at120 = 0;
            int at180 = 0;
            int at300 = 0;
            JsonElement? final = null;
            while (await reader.ReadLineAsync() is { } line)
            {
                if (line.Length == 0)
                    continue;
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                string token = root.GetProperty("message")
                    .GetProperty("content").GetString() ?? string.Empty;
                if (token.Length > 0)
                {
                    firstToken ??= stopwatch.Elapsed.TotalSeconds;
                    generated.Append(token);
                    chunks++;
                }
                double elapsed = stopwatch.Elapsed.TotalSeconds;
                if (elapsed <= 60) at60 = chunks;
                if (elapsed <= 120) at120 = chunks;
                if (elapsed <= 180) at180 = chunks;
                if (elapsed <= 300) at300 = chunks;
                if (root.TryGetProperty("done", out JsonElement done) &&
                    done.GetBoolean())
                {
                    final = root.Clone();
                    break;
                }
            }
            stopwatch.Stop();
            string outputText = generated.ToString();
            bool schemaValid = request.StructuredOutput is not null &&
                new AiStructuredOutputValidator().TryValidate(
                    request.StructuredOutput.Schema, outputText, out _);
            PatchValidationResult patch = ValidatePatch(outputText, source);
            return StreamingReplayResult.Completed(
                stopwatch.Elapsed.TotalSeconds, firstToken, at60, at120,
                at180, at300, chunks, outputText.Length,
                Pattern(outputText), schemaValid, patch, final);
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            return StreamingReplayResult.Timeout(stopwatch.Elapsed.TotalSeconds);
        }
    }

    private static string Pattern(string value)
    {
        string trimmed = value.TrimStart();
        if (trimmed.StartsWith("{", StringComparison.Ordinal)) return "json";
        if (trimmed.StartsWith("```", StringComparison.Ordinal)) return "markdown";
        return string.IsNullOrEmpty(trimmed) ? "empty" : "prose-or-other";
    }

    private static PatchValidationResult ValidatePatch(
        string outputText,
        string source)
    {
        try
        {
            DeveloperProposal? proposal =
                JsonSerializer.Deserialize<DeveloperProposal>(outputText);
            if (proposal?.Changes.Count != 1)
                return PatchValidationResult.Invalid("change_count");

            DeveloperChangeOperation change = proposal.Changes[0];
            if (change.Operation != DeveloperChangeOperationType.LocalizedPatch ||
                change.RelativePath != TargetPath || change.Content.Length != 0)
                return PatchValidationResult.Invalid("authorization_or_operation");
            if (change.ExpectedContentSha256 != Sha256(source))
                return PatchValidationResult.Invalid("base_sha");
            if (change.Edits.Count is < 1 or > 8)
                return PatchValidationResult.Invalid("edit_count");

            var matches = new List<(int Start, int Length, string NewText)>();
            foreach (DeveloperLocalizedEdit edit in change.Edits)
            {
                if (string.IsNullOrEmpty(edit.OldText) ||
                    edit.NewText is null || edit.OldText == edit.NewText)
                    return PatchValidationResult.Invalid("invalid_edit");
                int first = source.IndexOf(edit.OldText, StringComparison.Ordinal);
                if (first < 0 || source.IndexOf(
                        edit.OldText, first + edit.OldText.Length,
                        StringComparison.Ordinal) >= 0)
                    return PatchValidationResult.Invalid("non_unique_old_text");
                matches.Add((first, edit.OldText.Length, edit.NewText));
            }

            var ordered = matches.OrderBy(match => match.Start).ToArray();
            for (int index = 1; index < ordered.Length; index++)
            {
                if (ordered[index].Start <
                    ordered[index - 1].Start + ordered[index - 1].Length)
                    return PatchValidationResult.Invalid("overlap");
            }

            string result = source;
            foreach (var match in ordered.Reverse())
                result = result.Remove(match.Start, match.Length)
                    .Insert(match.Start, match.NewText);

            bool clipboard = Contains(result, "clipboard");
            bool jobId = Contains(result, "JobId");
            bool runId = Contains(result, "RunId");
            bool confirmation = Contains(result, "copied") ||
                Contains(result, "copy feedback") ||
                Contains(result, "copy status") || Contains(result, "confirmation") ||
                Contains(result, "success") || Contains(result, "message");
            bool missingRunSafe = Contains(result, "RunId.HasValue") ||
                Contains(result, "RunId is not null") ||
                Contains(result, "RunId is null") ||
                Contains(result, "Guid.Empty") ||
                (Contains(result, "RunId") && Contains(result, "default"));
            bool copyFeature = clipboard && jobId && runId && confirmation &&
                missingRunSafe;
            return new PatchValidationResult(
                true, true, true, true, true, Sha256(result), copyFeature,
                copyFeature ? string.Empty :
                    $"copy_feature_incomplete:clipboard={clipboard},jobId={jobId},runId={runId},confirmation={confirmation},missingRunSafe={missingRunSafe}");
        }
        catch (JsonException)
        {
            return PatchValidationResult.Invalid("invalid_json");
        }
    }

    private static bool Contains(string value, string expected) =>
        value.Contains(expected, StringComparison.OrdinalIgnoreCase);

    private static ReplayFixture CreateFixture()
    {
        string root = RepositoryRoot();
        string sourcePath = Path.Combine(root, TargetPath);
        string source = File.ReadAllText(sourcePath);
        string sourceHash = Sha256(source);
        string? planPath = Environment.GetEnvironmentVariable(
            "KRONXY_REPLAY_PLAN_PATH");
        PlannerPlan plan = !string.IsNullOrWhiteSpace(planPath)
            ? JsonSerializer.Deserialize<PlannerPlan>(File.ReadAllText(planPath))!
            : new PlannerPlan
            {
                Objective = "Add copy controls safely.",
                FilesToInspect = [TargetPath],
                CandidateFilesToModify = [TargetPath],
                Strategy = "Make the smallest localized UI change.",
                AcceptanceCriteria = ["Requested copy controls work."],
                Risks = [],
                ExpectedTests = [],
                Assumptions = [],
                Uncertainties = []
            };
        string jobRequest = Environment.GetEnvironmentVariable(
            "KRONXY_REPLAY_JOB_REQUEST") ??
            "Add the requested copy controls to JobDetail safely.";
        var request = new DeveloperExecutionRequest
        {
            JobId = Guid.Empty,
            RunId = Guid.Empty,
            JobRequest = jobRequest,
            CorrelationId = "developer-exact-replay",
            Repository = new RepositoryWorktreeHandle(
                Guid.Empty, "REPLAY", root, root, "replay/read-only",
                new string('0', 40), RepositoryWorktreeOperationKind.Existing)
        };
        var target = new DeveloperTargetContext(
            TargetPath, true, source, sourceHash,
            "WritableCandidate", "PlanningPlan+ContextManifest");
        JsonElement schema = DeveloperExecutionService.InitialSchema(target);
        var capability = new DeveloperAiCapability(
            AiLogicalModel.CodingQuality, 16_384, 2_048, 2_048, 512,
            TimeSpan.FromMinutes(5));
        DeveloperPromptParts prompt =
            DeveloperExecutionService.BuildSingleInitialPromptParts(
                request, JsonSerializer.Serialize(plan), [target],
                plan.CandidateFilesToModify);
        AiRequest aiRequest = DeveloperExecutionService.CreateAiRequest(
            DeveloperExecutionService.InitialSystemInstructions(target),
            prompt.UserContent,
            request.CorrelationId,
            schema,
            2_048,
            capability);
        AiGatewayOptions options = Options();
        DeveloperAiRequestFingerprint fingerprint =
            DeveloperAiRequestDiagnostics.Create(
                aiRequest, prompt, target, PhysicalModel, options);
        return new ReplayFixture(
            aiRequest, prompt, fingerprint, options, source, sourcePath);
    }

    private static async Task<ReplayResult> ReplayAsync(
        AiRequest request,
        AiGatewayOptions options)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            OllamaProvider.CreateRequestPayload(request, PhysicalModel, options),
            OllamaProvider.RequestJsonOptions);
        using var client = new HttpClient
        {
            BaseAddress = new Uri("http://kronxy-server:11434/"),
            Timeout = TimeSpan.FromMinutes(5)
        };
        using var content = new ByteArrayContent(payload);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        Stopwatch stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync("api/chat", content);
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            return new ReplayResult(
                null, false, "http_timeout", null, null, null, null, null, null,
                null, null, stopwatch.Elapsed.TotalSeconds, 0, 0, false,
                payload.Length, true);
        }

        using (response)
        {
        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
        stopwatch.Stop();
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(responseBytes);
        JsonElement root = document.RootElement;
        string responseContent = root.GetProperty("message")
            .GetProperty("content").GetString() ?? string.Empty;
        bool schemaValid = request.StructuredOutput is not null &&
            new AiStructuredOutputValidator().TryValidate(
                request.StructuredOutput.Schema, responseContent, out _);
        long? promptDuration = Long(root, "prompt_eval_duration");
        long? evalDuration = Long(root, "eval_duration");
        long? promptCount = Long(root, "prompt_eval_count");
        long? evalCount = Long(root, "eval_count");

        return new ReplayResult(
            root.TryGetProperty("model", out JsonElement model)
                ? model.GetString() : null,
            root.TryGetProperty("done", out JsonElement done) && done.GetBoolean(),
            root.TryGetProperty("done_reason", out JsonElement reason)
                ? reason.GetString() : null,
            Long(root, "total_duration"),
            Long(root, "load_duration"),
            promptCount,
            promptDuration,
            evalCount,
            evalDuration,
            Rate(promptCount, promptDuration),
            Rate(evalCount, evalDuration),
            stopwatch.Elapsed.TotalSeconds,
            responseBytes.Length,
            responseContent.Length,
            schemaValid,
            payload.Length,
            false);
        }
    }

    private static long? Long(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long result)
            ? result : null;

    private static double? Rate(long? count, long? durationNanoseconds) =>
        count.HasValue && durationNanoseconds > 0
            ? count.Value * 1_000_000_000d / durationNanoseconds.Value
            : null;

    private static int DivideRoundUp(int value, int divisor) =>
        checked((value + divisor - 1) / divisor);

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "Kronxy.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("Repository root was not found.");
    }

    private static AiGatewayOptions Options() => new()
    {
        Provider = "Ollama",
        Endpoint = "http://kronxy-server:11434",
        Models = new Dictionary<string, string>
        {
            ["CodingFast"] = "qwen2.5-coder:7b",
            ["CodingQuality"] = PhysicalModel,
            ["General"] = "qwen2.5:14b"
        },
        MaxConcurrentInferences = 1,
        ConnectionTimeout = TimeSpan.FromSeconds(5),
        InferenceTimeout = TimeSpan.FromMinutes(5),
        PlanningInferenceTimeout = TimeSpan.FromMinutes(3),
        DeveloperInferenceTimeout = TimeSpan.FromMinutes(3),
        CodingQualityInferenceTimeout = TimeSpan.FromMinutes(5),
        QueueWaitTimeout = TimeSpan.FromSeconds(5),
        MaxOutputTokens = 2_048,
        DeveloperMaxOutputTokens = 2_048,
        DeveloperMinimumOutputTokens = 2_048,
        MaxInputCharacters = 65_536,
        DeveloperContextCharacters = 48_000,
        DeveloperFeedbackCharacters = 8_192,
        MaxResponseBytes = 1_048_576
    };

    private sealed record ReplayFixture(
        AiRequest Request,
        DeveloperPromptParts Prompt,
        DeveloperAiRequestFingerprint Fingerprint,
        AiGatewayOptions Options,
        string Source,
        string SourcePath);

    private sealed record ReplayResult(
        string? Model,
        bool Done,
        string? DoneReason,
        long? TotalDurationNanoseconds,
        long? LoadDurationNanoseconds,
        long? PromptEvalCount,
        long? PromptEvalDurationNanoseconds,
        long? EvalCount,
        long? EvalDurationNanoseconds,
        double? PromptTokensPerSecond,
        double? GenerationTokensPerSecond,
        double HttpDurationSeconds,
        int ResponseBytes,
        int ResponseContentCharacters,
        bool StructuredSchemaValid,
        int RequestBytes,
        bool TimedOut);

    private sealed record StreamingReplayResult(
        bool TimedOut,
        double HttpDurationSeconds,
        double? TimeToFirstTokenSeconds,
        int ChunksAt60Seconds,
        int ChunksAt120Seconds,
        int ChunksAt180Seconds,
        int ChunksAt300Seconds,
        int GeneratedChunks,
        int ResponseContentCharacters,
        string OutputPattern,
        bool StructuredSchemaValid,
        PatchValidationResult PatchValidation,
        bool? Done,
        string? DoneReason,
        long? TotalDurationNanoseconds,
        long? PromptEvalCount,
        long? PromptEvalDurationNanoseconds,
        long? EvalCount,
        long? EvalDurationNanoseconds)
    {
        public static StreamingReplayResult Timeout(double duration) =>
            new(true, duration, null, 0, 0, 0, 0, 0, 0, "timeout", false,
                PatchValidationResult.Invalid("timeout"), null, "http_timeout",
                null, null, null, null, null);

        public static StreamingReplayResult Completed(
            double duration,
            double? firstToken,
            int at60,
            int at120,
            int at180,
            int at300,
            int chunks,
            int characters,
            string pattern,
            bool schemaValid,
            PatchValidationResult patchValidation,
            JsonElement? final) =>
            new(
                false, duration, firstToken, at60, at120, at180, at300,
                chunks, characters, pattern, schemaValid, patchValidation,
                Bool(final, "done"), String(final, "done_reason"),
                Long(final, "total_duration"),
                Long(final, "prompt_eval_count"),
                Long(final, "prompt_eval_duration"),
                Long(final, "eval_count"),
                Long(final, "eval_duration"));

        private static bool? Bool(JsonElement? value, string name) =>
            value.HasValue && value.Value.TryGetProperty(name, out JsonElement item)
                ? item.GetBoolean() : null;

        private static string? String(JsonElement? value, string name) =>
            value.HasValue && value.Value.TryGetProperty(name, out JsonElement item)
                ? item.GetString() : null;

        private static long? Long(JsonElement? value, string name) =>
            value.HasValue && value.Value.TryGetProperty(name, out JsonElement item) &&
            item.TryGetInt64(out long result) ? result : null;
    }

    private sealed record PatchValidationResult(
        bool LocalizedPatchValid,
        bool AuthorizedTargetValid,
        bool BaseShaValid,
        bool ExactSingleMatchValid,
        bool AppliedInMemory,
        string? ResultSha256,
        bool ActualCopyFeatureAddressed,
        string Failure)
    {
        public static PatchValidationResult Invalid(string failure) =>
            new(false, false, false, false, false, null, false, failure);
    }
}
