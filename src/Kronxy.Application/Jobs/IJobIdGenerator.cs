using System;

namespace Kronxy.Application.Jobs;

public interface IJobIdGenerator
{
	Guid NewId();

	string NewExternalId();
}
