using System.Text.Json.Serialization;

namespace Kronxy.Context.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PackageType
{
    Baseline,
    Handoff,
    Issue
}
