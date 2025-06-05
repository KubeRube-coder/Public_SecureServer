using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using SecureServer.Data;
using System.Diagnostics;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _dbContext;

    public DatabaseHealthCheck(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Усложнить запрос?
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            stopwatch.Stop();

            var responseTime = stopwatch.ElapsedMilliseconds; // Время в миллисекундах

            return HealthCheckResult.Healthy($"База данных доступна. Время ответа: {responseTime} мс",
                new Dictionary<string, object>
                {
                    { "ResponseTimeMs", responseTime }
                });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HealthCheckResult.Unhealthy("База данных недоступна.", ex,
                new Dictionary<string, object>
                {
                    { "ErrorMessage", ex.Message }
                });
        }
    }
}
