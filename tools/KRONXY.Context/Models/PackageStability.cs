using System.Text.Json.Serialization;

namespace Kronxy.Context.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PackageStability
{
    Draft,
    Final
}
