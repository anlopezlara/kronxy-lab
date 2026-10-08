using Kronxy.Application.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Kronxy.Infrastructure;

internal sealed class OperatorHealthService : IOperatorHealthService
{
    private readonly ApplicationDbContext dbContext;
    public OperatorHealthService(ApplicationDbContext dbContext) => this.dbContext = dbContext;

    public async Task<OperatorHealth> CheckAsync(CancellationToken cancellationToken = default)
    {
        try { return new(await dbContext.Database.CanConnectAsync(cancellationToken)); }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { return new(false); }
    }
}
