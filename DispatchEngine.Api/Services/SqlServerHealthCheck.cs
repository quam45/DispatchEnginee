using Dapper;
using DispatchEngine.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
namespace DispatchEngine.Api.Services;

// Backs GET /health/ready: the API is only "ready" for traffic if it can reach SQL Server.
public sealed class SqlServerHealthCheck : IHealthCheck {
    private readonly IDbConnectionFactory _db;
    public SqlServerHealthCheck(IDbConnectionFactory db) { _db = db; }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) {
        try {
            using var con = _db.CreateConnection();
            await con.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1", cancellationToken: ct));
            return HealthCheckResult.Healthy();
        } catch (Exception ex) {
            return HealthCheckResult.Unhealthy("SQL Server is unreachable", ex);
        }
    }
}
