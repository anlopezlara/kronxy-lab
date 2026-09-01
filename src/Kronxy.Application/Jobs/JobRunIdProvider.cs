using System.Security.Cryptography;
using System.Text;

namespace Kronxy.Application.Jobs;

public interface IJobRunIdProvider
{
    Guid Create(
        Guid jobId,
        int attemptCount);
}

public sealed class DeterministicJobRunIdProvider :
    IJobRunIdProvider
{
    private const string NamespacePrefix =
        "KRONXY-JOB-RUN-V1";

    public Guid Create(
        Guid jobId,
        int attemptCount)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException(
                "Job id must not be empty.",
                nameof(jobId));
        }

        if (attemptCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attemptCount));
        }

        string canonical =
            string.Concat(
                NamespacePrefix,
                "|",
                jobId.ToString("N"),
                "|",
                attemptCount.ToString(
                    System.Globalization
                        .CultureInfo.InvariantCulture));

        byte[] hash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    canonical));

        Span<byte> guidBytes =
            stackalloc byte[16];

        hash.AsSpan(
                0,
                guidBytes.Length)
            .CopyTo(
                guidBytes);

        // RFC 4122 variant + deterministic version marker.
        guidBytes[7] =
            (byte)(
                (guidBytes[7] & 0x0F) |
                0x50);

        guidBytes[8] =
            (byte)(
                (guidBytes[8] & 0x3F) |
                0x80);

        return new Guid(
            guidBytes);
    }
}
