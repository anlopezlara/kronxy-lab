using Kronxy.Context.Models;

namespace Kronxy.Context.Commands;

public static class CommandLineParser
{
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    private static readonly HashSet<string> ValueOptions = new(Comparer)
    {
        "--repository", "--output", "--work-item", "--from", "--to", "--ref",
        "--package", "--error-file", "--error-text"
    };

    private static readonly HashSet<string> FlagOptions = new(Comparer)
    {
        "--draft", "--final"
    };

    public const string HelpText =
        "KRONXY.Context 0.1.0\n" +
        "Uso: kronxy-context <comando> [opciones]\n" +
        "Comandos: init, baseline, handoff, issue, validate, accept\n" +
        "Opciones globales: --help, -h, --version";

    public static CommandParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return CommandParseResult.Error("Debe especificar un comando.");
        }

        if (args.Count == 1 && (Comparer.Equals(args[0], "--help") || Comparer.Equals(args[0], "-h")))
        {
            return CommandParseResult.Help();
        }

        if (args.Count == 1 && Comparer.Equals(args[0], "--version"))
        {
            return CommandParseResult.Version();
        }

        if (!TryParseCommand(args[0], out var command))
        {
            return CommandParseResult.Error($"Comando desconocido: '{args[0]}'.");
        }

        var options = new Dictionary<string, string?>(Comparer);
        for (var index = 1; index < args.Count; index++)
        {
            var token = args[index];
            var separator = token.IndexOf('=');
            var name = separator >= 0 ? token[..separator] : token;

            if (!ValueOptions.Contains(name) && !FlagOptions.Contains(name))
            {
                return CommandParseResult.Error($"Opción desconocida: '{name}'.");
            }

            if (options.ContainsKey(name))
            {
                return CommandParseResult.Error($"La opción '{name}' está repetida.");
            }

            if (FlagOptions.Contains(name))
            {
                if (separator >= 0)
                {
                    return CommandParseResult.Error($"La opción '{name}' no acepta un valor.");
                }

                options[name] = null;
                continue;
            }

            string value;
            if (separator >= 0)
            {
                value = token[(separator + 1)..];
            }
            else
            {
                if (++index >= args.Count || args[index].StartsWith('-'))
                {
                    return CommandParseResult.Error($"La opción '{name}' requiere un valor.");
                }

                value = args[index];
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return CommandParseResult.Error($"La opción '{name}' no acepta un valor vacío.");
            }

            options[name] = value;
        }

        if (options.ContainsKey("--draft") && options.ContainsKey("--final"))
        {
            return CommandParseResult.Error("Las opciones '--draft' y '--final' son mutuamente excluyentes.");
        }

        var compatibilityError = ValidateCompatibility(command, options.Keys);
        if (compatibilityError is not null)
        {
            return CommandParseResult.Error(compatibilityError);
        }

        var requirementError = ValidateRequirements(command, options);
        if (requirementError is not null)
        {
            return CommandParseResult.Error(requirementError);
        }

        PackageStability? stability = command is ContextCommand.Baseline or ContextCommand.Handoff or ContextCommand.Issue
            ? options.ContainsKey("--final") ? PackageStability.Final : PackageStability.Draft
            : null;

        return CommandParseResult.Parsed(new CommandRequest
        {
            Command = command,
            Repository = Get(options, "--repository"),
            Output = Get(options, "--output"),
            WorkItem = Get(options, "--work-item"),
            From = Get(options, "--from") ?? (command is ContextCommand.Handoff or ContextCommand.Issue ? "last" : null),
            To = Get(options, "--to") ?? (command is ContextCommand.Handoff or ContextCommand.Issue ? "HEAD" : null),
            Ref = Get(options, "--ref") ?? (command == ContextCommand.Baseline ? "HEAD" : null),
            Package = Get(options, "--package"),
            ErrorFile = Get(options, "--error-file"),
            ErrorText = Get(options, "--error-text"),
            Stability = stability
        });
    }

    private static bool TryParseCommand(string value, out ContextCommand command) =>
        Enum.TryParse(value, true, out command) && Enum.IsDefined(command);

    private static string? ValidateCompatibility(ContextCommand command, IEnumerable<string> supplied)
    {
        var allowed = command switch
        {
            ContextCommand.Init => Set("--repository", "--output"),
            ContextCommand.Baseline => Set("--repository", "--ref", "--work-item", "--output", "--draft", "--final"),
            ContextCommand.Handoff => Set("--repository", "--work-item", "--from", "--to", "--output", "--draft", "--final"),
            ContextCommand.Issue => Set("--repository", "--work-item", "--from", "--to", "--error-file", "--error-text", "--output", "--draft", "--final"),
            ContextCommand.Validate => Set("--package"),
            ContextCommand.Accept => Set("--package"),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };

        var incompatible = supplied.FirstOrDefault(option => !allowed.Contains(option));
        return incompatible is null
            ? null
            : $"La opción '{incompatible}' no es compatible con el comando '{command.ToString().ToLowerInvariant()}'.";
    }

    private static string? ValidateRequirements(ContextCommand command, IReadOnlyDictionary<string, string?> options)
    {
        if (command is ContextCommand.Init or ContextCommand.Baseline or ContextCommand.Handoff or ContextCommand.Issue)
        {
            if (!options.ContainsKey("--repository"))
            {
                return "La opción '--repository' es obligatoria.";
            }
        }

        if (command is ContextCommand.Handoff or ContextCommand.Issue && !options.ContainsKey("--work-item"))
        {
            return "La opción '--work-item' es obligatoria.";
        }

        if (command == ContextCommand.Issue &&
            !options.ContainsKey("--error-file") &&
            !options.ContainsKey("--error-text"))
        {
            return "El comando 'issue' requiere '--error-file' o '--error-text'.";
        }

        if (command is ContextCommand.Validate or ContextCommand.Accept && !options.ContainsKey("--package"))
        {
            return "La opción '--package' es obligatoria.";
        }

        return null;
    }

    private static HashSet<string> Set(params string[] values) => new(values, Comparer);

    private static string? Get(IReadOnlyDictionary<string, string?> options, string name) =>
        options.TryGetValue(name, out var value) ? value : null;
}
