namespace Kronxy.Context.Processes;

public sealed record ProcessRequest
{
    public const int DefaultOutputLimitBytes = 1_048_576;
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaximumTimeout = TimeSpan.FromMilliseconds(uint.MaxValue - 1d);

    public required string FileName { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = [];
    public required string WorkingDirectory { get; init; }
    public TimeSpan Timeout { get; init; } = DefaultTimeout;
    public int StandardOutputLimitBytes { get; init; } = DefaultOutputLimitBytes;
    public int StandardErrorLimitBytes { get; init; } = DefaultOutputLimitBytes;
    public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; init; } =
        new Dictionary<string, string?>();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FileName) || FileName.Contains('\0'))
        {
            throw new ArgumentException("El ejecutable no puede estar vacío ni contener NUL.", nameof(FileName));
        }

        if (string.IsNullOrWhiteSpace(WorkingDirectory) || WorkingDirectory.Contains('\0'))
        {
            throw new ArgumentException("El directorio de trabajo no es válido.", nameof(WorkingDirectory));
        }

        if (!Directory.Exists(WorkingDirectory))
        {
            throw new DirectoryNotFoundException("El directorio de trabajo no existe.");
        }

        if (Arguments is null || Arguments.Any(argument => argument is null || argument.Contains('\0')))
        {
            throw new ArgumentException("Los argumentos no pueden ser nulos ni contener NUL.", nameof(Arguments));
        }

        if (Timeout <= TimeSpan.Zero || Timeout > MaximumTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(Timeout), "El timeout debe ser positivo y compatible con .NET.");
        }

        if (StandardOutputLimitBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(StandardOutputLimitBytes));
        }

        if (StandardErrorLimitBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(StandardErrorLimitBytes));
        }

        if (EnvironmentVariables is null || EnvironmentVariables.Any(item =>
                string.IsNullOrWhiteSpace(item.Key) || item.Key.Contains('\0') || item.Key.Contains('=') ||
                item.Value?.Contains('\0') == true))
        {
            throw new ArgumentException("Las variables de entorno adicionales no son válidas.", nameof(EnvironmentVariables));
        }
    }
}
