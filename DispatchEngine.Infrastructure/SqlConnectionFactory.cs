using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using DispatchEngine.Core;
namespace DispatchEngine.Infrastructure;

public interface IDbConnectionFactory { DbConnection CreateConnection(); }

// One place that knows the connection string. Dapper opens and closes the connection per call;
// ADO.NET connection pooling makes that cheap.
public sealed class SqlConnectionFactory : IDbConnectionFactory {
    private readonly string _conn;
    public SqlConnectionFactory(IConfiguration config) {
        _conn = config.GetConnectionString("Default") ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
    }
    public DbConnection CreateConnection() => new SqlConnection(_conn);
}

public static class DapperExtensions {
    public const int MaxPageSize = 100;

    // For the paged procedures, which return the total count first and then the page of rows.
    public static async Task<PagedResult<T>> QueryPagedAsync<T>(this IDbConnection con, string procedure, object? parameters, int page, int pageSize) {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var p = new DynamicParameters(parameters);
        p.Add("Page", page);
        p.Add("PageSize", pageSize);
        using var grid = await con.QueryMultipleAsync(procedure, p, commandType: CommandType.StoredProcedure);
        var total = await grid.ReadSingleAsync<int>();
        var items = (await grid.ReadAsync<T>()).ToList();
        return new PagedResult<T>(items, page, pageSize, total);
    }
}
