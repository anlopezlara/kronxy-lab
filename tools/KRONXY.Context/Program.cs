using Kronxy.Context.Commands;

const string Version = "0.1.0";

try
{
    var result = CommandLineParser.Parse(args);

    if (result.HelpRequested)
    {
        Console.Out.WriteLine(CommandLineParser.HelpText);
        return ExitCodes.Success;
    }

    if (result.VersionRequested)
    {
        Console.Out.WriteLine(Version);
        return ExitCodes.Success;
    }

    if (!result.Success)
    {
        Console.Error.WriteLine($"Error: {result.ErrorMessage}");
        Console.Error.WriteLine("Use --help para consultar el uso disponible.");
        return ExitCodes.InvalidArguments;
    }

    Console.Out.WriteLine(
        $"El comando '{result.Request!.Command.ToString().ToLowerInvariant()}' fue reconocido, " +
        "pero su operación se implementará en un bloque posterior.");
    return ExitCodes.IncompleteOperation;
}
catch
{
    Console.Error.WriteLine("Ocurrió un error inesperado al procesar la solicitud.");
    return ExitCodes.UnexpectedError;
}
