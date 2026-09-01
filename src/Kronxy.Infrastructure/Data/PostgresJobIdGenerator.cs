using System.Globalization;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Jobs;

namespace Kronxy.Infrastructure.Data;

internal sealed class PostgresJobIdGenerator : IJobIdGenerator
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public PostgresJobIdGenerator(
        ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public Guid NewId()
    {
        return Guid.NewGuid();
    }

    public string NewExternalId()
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        using var command = connection.CreateCommand();

        command.CommandText =
            "SELECT nextval('job_external_id_seq');";

        var rawValue = command.ExecuteScalar();

        if (rawValue is null ||
            !long.TryParse(
                Convert.ToString(
                    rawValue,
                    CultureInfo.InvariantCulture),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var sequenceValue) ||
            sequenceValue <= 0)
        {
            throw new InvalidOperationException(
                "Unable to generate the persistent KRONXY job identifier.");
        }

        return $"KRX-{sequenceValue:D6}";
    }
}
