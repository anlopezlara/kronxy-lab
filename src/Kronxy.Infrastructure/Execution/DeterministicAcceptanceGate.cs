using System.Security.Cryptography;
using System.Text;
using Kronxy.Application.Execution;

namespace Kronxy.Infrastructure.Execution;

public sealed class DeterministicAcceptanceGate : IDeterministicAcceptanceGate
{
    public DeterministicAcceptanceGateResult Evaluate(
        string jobRequest,
        PlannerPlan plan,
        ReviewerEffectiveSourceSnapshot snapshot)
    {
        IReadOnlyList<DeterministicAcceptanceCriterion> criteria =
            DeterministicCriteriaParser.Parse(jobRequest, plan);
        var results = new List<DeterministicAcceptanceCriterionResult>();
        Dictionary<string, ReviewerEffectiveSourceFile> files = snapshot.Files
            .GroupBy(file => Normalize(file.RelativePath), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);

        foreach (DeterministicAcceptanceCriterion criterion in criteria)
        {
            string path = Normalize(criterion.RelativePath);
            if (!files.TryGetValue(path, out ReviewerEffectiveSourceFile? file))
            {
                results.Add(Result(criterion, DeterministicCriterionStatus.Fail,
                    "FILE_NOT_PRESENT_IN_EFFECTIVE_SOURCE"));
                continue;
            }

            if (!ValidSource(file))
            {
                results.Add(Result(criterion, DeterministicCriterionStatus.Unevaluable,
                    "EFFECTIVE_SOURCE_HASH_OR_SIZE_MISMATCH"));
                continue;
            }

            if (criterion.Kind == DeterministicCriterionKind.FileExists)
            {
                results.Add(Result(criterion, DeterministicCriterionStatus.Pass,
                    $"sha256:{file.Sha256}"));
                continue;
            }

            if (!CSharpTokens.TryParse(file.Content, out CSharpTokens? tokens))
            {
                results.Add(Result(criterion, DeterministicCriterionStatus.Unevaluable,
                    "CSHARP_SOURCE_COULD_NOT_BE_TOKENIZED"));
                continue;
            }
            CSharpTokens parsed = tokens!;

            bool present = criterion.Kind switch
            {
                DeterministicCriterionKind.CSharpPropertyExists =>
                    parsed.HasProperty(criterion.Expected),
                DeterministicCriterionKind.CSharpMethodExists =>
                    parsed.HasMethod(criterion.Expected),
                DeterministicCriterionKind.StringLiteralExists =>
                    parsed.HasStringLiteral(criterion.Expected),
                _ => false
            };
            results.Add(Result(criterion,
                present ? DeterministicCriterionStatus.Pass : DeterministicCriterionStatus.Fail,
                present ? "SYMBOL_PRESENT" : "SYMBOL_ABSENT"));
        }

        string[] semantic = plan.AcceptanceCriteria
            .Where(criterion => !criteria.Any(deterministic =>
                string.Equals(deterministic.SourceRequirement, criterion,
                    StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(deterministic.Expected) &&
                 criterion.Contains(deterministic.Expected,
                    StringComparison.OrdinalIgnoreCase))))
            .ToArray();
        return new DeterministicAcceptanceGateResult(results, semantic);
    }

    private static bool ValidSource(ReviewerEffectiveSourceFile file)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(file.Content);
        return file.SizeBytes == bytes.LongLength &&
            string.Equals(file.Sha256,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                StringComparison.OrdinalIgnoreCase);
    }

    private static DeterministicAcceptanceCriterionResult Result(
        DeterministicAcceptanceCriterion criterion,
        DeterministicCriterionStatus status,
        string evidence) => new(criterion, status, evidence);

    private static string Normalize(string value) =>
        (value ?? string.Empty).Replace('\\', '/').Trim();

    private static class DeterministicCriteriaParser
    {
        public static IReadOnlyList<DeterministicAcceptanceCriterion> Parse(
            string request,
            PlannerPlan plan)
        {
            var criteria = new List<DeterministicAcceptanceCriterion>();
            foreach (string path in plan.CandidateFilesToModify.Distinct(StringComparer.OrdinalIgnoreCase))
                criteria.Add(Criterion(DeterministicCriterionKind.FileExists, path, path,
                    "candidate file exists"));

            Dictionary<string, string> sections = Sections(request);
            string? entityPath = plan.CandidateFilesToModify.FirstOrDefault(path =>
                Path.GetFileName(path).EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                !Path.GetFileName(path).StartsWith('I') &&
                !Path.GetFileNameWithoutExtension(path).EndsWith("Errors", StringComparison.OrdinalIgnoreCase));
            string? errorsPath = plan.CandidateFilesToModify.FirstOrDefault(path =>
                Path.GetFileNameWithoutExtension(path).EndsWith("Errors", StringComparison.OrdinalIgnoreCase));
            string? repositoryPath = plan.CandidateFilesToModify.FirstOrDefault(path =>
                Path.GetFileName(path).StartsWith('I') &&
                Path.GetFileNameWithoutExtension(path).EndsWith("Repository", StringComparison.OrdinalIgnoreCase));

            AddFields(criteria, sections.GetValueOrDefault("REQUIRED FIRST-CLASS FIELDS"), entityPath);
            AddMethods(criteria, sections, entityPath);
            AddErrorCodes(criteria, sections.GetValueOrDefault("DOMAIN ERRORS"), errorsPath);
            AddRepositoryMethods(criteria, sections.GetValueOrDefault("REPOSITORY CONTRACT"), repositoryPath);
            return criteria;
        }

        private static void AddFields(List<DeterministicAcceptanceCriterion> criteria,
            string? section, string? path)
        {
            if (string.IsNullOrWhiteSpace(section)) return;
            if (string.IsNullOrWhiteSpace(path))
            {
                criteria.Add(Criterion(DeterministicCriterionKind.CSharpPropertyExists,
                    string.Empty, string.Empty, section));
                return;
            }
            foreach (string line in Lines(section))
            {
                string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 2 && Identifier(tokens[1]) && TypeLike(tokens[0]))
                    criteria.Add(Criterion(DeterministicCriterionKind.CSharpPropertyExists,
                        path, tokens[1], line));
            }
        }

        private static void AddMethods(List<DeterministicAcceptanceCriterion> criteria,
            Dictionary<string, string> sections, string? path)
        {
            foreach (string title in new[] { "DOMAIN CREATION", "DOMAIN UPDATE", "SOFT DELETE" })
            {
                string? section = sections.GetValueOrDefault(title);
                if (string.IsNullOrWhiteSpace(section)) continue;
                foreach (string line in Lines(section))
                {
                    int open = line.IndexOf('(');
                    if (open <= 0) continue;
                    string prefix = line[..open].Trim();
                    string name = prefix.Split('.').Last().Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
                    if (!Identifier(name) || name is "if" or "while" or "for") continue;
                    criteria.Add(Criterion(DeterministicCriterionKind.CSharpMethodExists,
                        path ?? string.Empty, name, line));
                }
            }
        }

        private static void AddErrorCodes(List<DeterministicAcceptanceCriterion> criteria,
            string? section, string? path)
        {
            if (string.IsNullOrWhiteSpace(section)) return;
            foreach (string line in Lines(section))
            {
                string[] parts = line.Split('.');
                if (parts.Length == 2 && parts.All(Identifier))
                    criteria.Add(Criterion(DeterministicCriterionKind.StringLiteralExists,
                        path ?? string.Empty, line, line));
            }
        }

        private static void AddRepositoryMethods(List<DeterministicAcceptanceCriterion> criteria,
            string? section, string? path)
        {
            if (string.IsNullOrWhiteSpace(section)) return;
            foreach (string line in Lines(section))
            {
                int open = line.IndexOf('(');
                if (open <= 0) continue;
                string name = line[..open].Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
                if (Identifier(name))
                    criteria.Add(Criterion(DeterministicCriterionKind.CSharpMethodExists,
                        path ?? string.Empty, name, line));
            }
        }

        private static Dictionary<string, string> Sections(string request)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string? title = null;
            var body = new StringBuilder();
            foreach (string raw in request.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length > 2 && line.All(ch => ch == '='))
                    continue;
                if (line.Length > 0 && line.All(ch => !char.IsLetter(ch) || char.IsUpper(ch)) &&
                    line.Any(char.IsLetter))
                {
                    if (title is not null && body.Length > 0) result[title] = body.ToString();
                    title = line.TrimStart('#', ' '); body.Clear(); continue;
                }
                if (title is not null) body.AppendLine(line);
            }
            if (title is not null && body.Length > 0) result[title] = body.ToString();
            return result;
        }

        private static IEnumerable<string> Lines(string section) =>
            section.Split('\n').Select(line => line.Trim().TrimStart('-', '*', ' '))
                .Where(line => line.Length > 0);
        private static bool Identifier(string value) => value.Length > 0 &&
            (char.IsLetter(value[0]) || value[0] == '_') &&
            value.Skip(1).All(ch => char.IsLetterOrDigit(ch) || ch == '_');
        private static bool TypeLike(string value) =>
            value.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '?' or '<' or '>' or ',' or '.');
        private static DeterministicAcceptanceCriterion Criterion(
            DeterministicCriterionKind kind, string path, string expected, string source) =>
            new($"{kind}:{path}:{expected}", kind, path, expected, source);
    }

    private sealed class CSharpTokens
    {
        private readonly IReadOnlyList<Token> tokens;
        private CSharpTokens(IReadOnlyList<Token> tokens) => this.tokens = tokens;

        public bool HasProperty(string name)
        {
            for (int i = 0; i + 2 < tokens.Count; i++)
                if (tokens[i].Depth == 1 && tokens[i].Text == name && tokens[i + 1].Text == "{" &&
                    tokens.Skip(i + 2).TakeWhile(token => token.Depth > 1).Any(token => token.Text == "get"))
                    return true;
            return false;
        }

        public bool HasMethod(string name) => tokens.Zip(tokens.Skip(1))
            .Any(pair => pair.First.Depth == 1 && pair.First.Text == name && pair.Second.Text == "(");

        public bool HasStringLiteral(string value) =>
            tokens.Any(token => token.IsString && token.Text == value);

        public static bool TryParse(string source, out CSharpTokens? parsed)
        {
            var tokens = new List<Token>();
            int depth = 0;
            for (int i = 0; i < source.Length;)
            {
                char ch = source[i];
                if (char.IsWhiteSpace(ch)) { i++; continue; }
                if (ch == '/' && i + 1 < source.Length && source[i + 1] == '/')
                { i = source.IndexOf('\n', i + 2); if (i < 0) i = source.Length; continue; }
                if (ch == '/' && i + 1 < source.Length && source[i + 1] == '*')
                { int end = source.IndexOf("*/", i + 2, StringComparison.Ordinal); if (end < 0) return Fail(out parsed); i = end + 2; continue; }
                if (ch == '"')
                {
                    var value = new StringBuilder(); bool closed = false;
                    for (i++; i < source.Length; i++)
                    {
                        if (source[i] == '\\' && i + 1 < source.Length) { value.Append(source[++i]); continue; }
                        if (source[i] == '"') { i++; closed = true; break; }
                        value.Append(source[i]);
                    }
                    if (!closed) return Fail(out parsed);
                    tokens.Add(new Token(value.ToString(), depth, true)); continue;
                }
                if (char.IsLetter(ch) || ch == '_')
                {
                    int start = i++;
                    while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_')) i++;
                    tokens.Add(new Token(source[start..i], depth, false)); continue;
                }
                if (ch == '{') { tokens.Add(new Token("{", depth, false)); depth++; i++; continue; }
                if (ch == '}') { if (--depth < 0) return Fail(out parsed); tokens.Add(new Token("}", depth, false)); i++; continue; }
                if (ch is '(' or ')' or ';') tokens.Add(new Token(ch.ToString(), depth, false));
                i++;
            }
            if (depth != 0) return Fail(out parsed);
            parsed = new CSharpTokens(tokens); return true;
        }

        private static bool Fail(out CSharpTokens? parsed) { parsed = null; return false; }
        private sealed record Token(string Text, int Depth, bool IsString);
    }
}
