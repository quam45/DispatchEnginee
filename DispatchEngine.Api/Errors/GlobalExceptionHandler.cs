using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
namespace DispatchEngine.Api.Errors;

// Stored procedures report business-rule failures with THROW (50000 + HTTP status), 'CODE: message'.
// Those become normal 4xx responses. Anything else is a bug or an outage: it is logged in full,
// and the client gets a generic 500 with no stack trace or SQL details.
public sealed class GlobalExceptionHandler : IExceptionHandler {
    private readonly ILogger<GlobalExceptionHandler> _logger;
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) { _logger = logger; }

    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct) {
        if (ex is SqlException { Number: >= 50400 and < 50600 } sql) {
            var raw = sql.Errors.Count > 0 ? sql.Errors[0].Message : sql.Message;
            var sep = raw.IndexOf(": ", StringComparison.Ordinal);
            var (code, message) = sep > 0 ? (raw[..sep], raw[(sep + 2)..]) : ("REQUEST_REJECTED", raw);
            _logger.LogInformation("Request rejected by business rule {Code} on {Path}", code, ctx.Request.Path);
            await ApiError.WriteAsync(ctx, sql.Number - 50000, code, message, ct);
            return true;
        }

        _logger.LogError(ex, "Unhandled exception on {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
        await ApiError.WriteAsync(ctx, StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "Something went wrong. Please try again.", ct);
        return true;
    }
}
