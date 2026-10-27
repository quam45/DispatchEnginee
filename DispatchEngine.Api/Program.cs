using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using DispatchEngine.Infrastructure;
using DispatchEngine.Api.Errors;
using DispatchEngine.Api.Hubs;
using DispatchEngine.Api.Services;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Secrets never live in appsettings.json. Locally: dotnet user-secrets; in Docker/production: the Jwt__Key environment variable.
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key is missing or shorter than 32 bytes. Run: dotnet user-secrets set \"Jwt:Key\" \"<random 64+ character string>\" --project DispatchEngine.Api");

builder.Services.AddControllers();
// Validation failures use the same error shape as every other error
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = ctx => {
    var details = ctx.ModelState.Where(e => e.Value!.Errors.Count > 0)
        .ToDictionary(e => e.Key, e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray());
    return new BadRequestObjectResult(ApiError.Body(ctx.HttpContext, "VALIDATION_FAILED", "One or more fields are invalid.", details));
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IPaystackService, PaystackService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddScoped<OrderBackgroundService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHangfire(x => x.UseSqlServerStorage(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddHangfireServer();
builder.Services.AddSwaggerGen(c => {
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", Description = "Paste the token from /api/Auth/login (without 'Bearer ')" });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } });
});
builder.Services.AddSignalR();
builder.Services.AddHealthChecks().AddCheck<SqlServerHealthCheck>("sqlserver", tags: ["ready"]);

builder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests; // the framework default is 503
    options.OnRejected = (ctx, ct) => new ValueTask(ApiError.WriteAsync(ctx.HttpContext, StatusCodes.Status429TooManyRequests, "RATE_LIMITED", "Too many requests. Slow down and try again shortly.", ct));
    // Each limit is partitioned per user (or per IP before login). A single shared window would let
    // one noisy client use up the allowance for everyone.
    static string UserOrIp(HttpContext http) => http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    // Each rider can only send 5 location updates per 10 seconds
    options.AddPolicy("rider", http => RateLimitPartition.GetFixedWindowLimiter(UserOrIp(http), _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromSeconds(10) }));
    // Login/register: slows down password guessing and account-creation spam
    options.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    // Delivery OTP: 5 guesses a minute makes a 6-digit code impractical to brute-force
    options.AddPolicy("otp", http => RateLimitPartition.GetFixedWindowLimiter(UserOrIp(http), _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => {
    o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true, ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidAudience = builder.Configuration["Jwt:Audience"], IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), ClockSkew = TimeSpan.FromMinutes(1) };
    o.Events = new JwtBearerEvents {
        OnChallenge = ctx => { ctx.HandleResponse(); return ApiError.WriteAsync(ctx.HttpContext, 401, "UNAUTHORIZED", "A valid access token is required."); },
        OnForbidden = ctx => ApiError.WriteAsync(ctx.HttpContext, 403, "FORBIDDEN", "You do not have permission to do this."),
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseExceptionHandler();
app.UseDefaultFiles(); // must run before UseStaticFiles to serve index.html for "/"
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter(); // after authentication, so limits can be partitioned per user
app.MapControllers();
app.UseHangfireDashboard("/hangfire"); // local requests only (Hangfire's default)
RecurringJob.AddOrUpdate<OrderBackgroundService>("reassign-stale-orders", s => s.ReassignStaleOrders(), Cron.Minutely);
app.MapHub<TrackingHub>("/hubs/tracking");
// Liveness: the process is up. Readiness: it can also reach SQL Server and should receive traffic.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapFallbackToFile("index.html");
app.Run();
