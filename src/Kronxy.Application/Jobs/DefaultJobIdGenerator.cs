using System;

namespace Kronxy.Application.Jobs;

public sealed class DefaultJobIdGenerator : IJobIdGenerator
{
	public Guid NewId()
	{
		return Guid.NewGuid();
	}

	public string NewExternalId()
        {
                byte[] bytes =
                        Guid.NewGuid().ToByteArray();

                ulong value =
                        BitConverter.ToUInt64(bytes, 0) %
                        1_000_000_000_000_000_000UL;

                return $"KRX-{value:D18}";
        }
}
