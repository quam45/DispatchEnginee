using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
namespace DispatchEngine.Api.Errors;

// Every error the API returns has this shape:
// { "success": false, "error": { "code": "ORDER_NOT_AVAILABLE", "message": "..." }, "traceId": "00-..." }
public record ErrorInfo(string Code, string Message, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IDictionary<string, string[]>? Details = null);
public record ErrorBody(bool Success, ErrorInfo Error, string TraceId);

public static class ApiError {
    public static ErrorBody Body(HttpContext ctx, string code, string message, IDictionary<string, string[]>? details = null) =>
        new(false, new ErrorInfo(code, message, details), Activity.Current?.Id ?? ctx.TraceIdentifier);

    public static Task WriteAsync(HttpContext ctx, int status, string code, string message, CancellationToken ct = default) {
        ctx.Response.StatusCode = status;
        return ctx.Response.WriteAsJsonAsync(Body(ctx, code, message), ct);
    }

    public static ObjectResult Error(this ControllerBase controller, int status, string code, string message) =>
        new(Body(controller.HttpContext, code, message)) { StatusCode = status };
}
