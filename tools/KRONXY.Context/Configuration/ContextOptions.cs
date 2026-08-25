namespace Kronxy.Context.Configuration;

public sealed record ContextOptions
{
    public int MaxTextFileBytes { get; init; } = 262_144;
    public int MaxLinesPerFile { get; init; } = 2_000;
    public int MaxPatchBytes { get; init; } = 1_048_576;
    public int TargetHandoffPackageBytes { get; init; } = 1_572_864;
    public int MaxHandoffPackageBytes { get; init; } = 3_145_728;
    public int MaxBaselinePackageBytes { get; init; } = 5_242_880;
    public int MaxFilesPerPackage { get; init; } = 100;
    public int RelationshipDepth { get; init; } = 1;
    public string DefaultOutputDirectory { get; init; } = ".kronxy-context/packages";
    public string RedactedValue { get; init; } = "***REDACTED***";

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        AddPositiveError(errors, MaxTextFileBytes, nameof(MaxTextFileBytes));
        AddPositiveError(errors, MaxLinesPerFile, nameof(MaxLinesPerFile));
        AddPositiveError(errors, MaxPatchBytes, nameof(MaxPatchBytes));
        AddPositiveError(errors, TargetHandoffPackageBytes, nameof(TargetHandoffPackageBytes));
        AddPositiveError(errors, MaxHandoffPackageBytes, nameof(MaxHandoffPackageBytes));
        AddPositiveError(errors, MaxBaselinePackageBytes, nameof(MaxBaselinePackageBytes));
        AddPositiveError(errors, MaxFilesPerPackage, nameof(MaxFilesPerPackage));

        if (TargetHandoffPackageBytes > MaxHandoffPackageBytes)
        {
            errors.Add("TargetHandoffPackageBytes no puede superar MaxHandoffPackageBytes.");
        }

        if (RelationshipDepth < 0)
        {
            errors.Add("RelationshipDepth no puede ser negativo.");
        }

        if (string.IsNullOrWhiteSpace(DefaultOutputDirectory))
        {
            errors.Add("DefaultOutputDirectory no puede estar vacío.");
        }

        if (string.IsNullOrWhiteSpace(RedactedValue))
        {
            errors.Add("RedactedValue no puede estar vacío.");
        }

        return errors;
    }

    private static void AddPositiveError(ICollection<string> errors, int value, string name)
    {
        if (value <= 0)
        {
            errors.Add($"{name} debe ser mayor que cero.");
        }
    }
}
