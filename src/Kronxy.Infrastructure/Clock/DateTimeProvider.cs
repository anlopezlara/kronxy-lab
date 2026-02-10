using Kronxy.Application.Abstractions.Clock;

namespace Kronxy.Infrastructure.Clock;

internal sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}