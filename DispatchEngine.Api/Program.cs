using DispatchEngine.Infrastructure;
using DispatchEngine.Api.Hubs;
using DispatchEngine.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Hangfire;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddScoped<IPaystackService, PaystackService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHangfire(x => x.UseSqlServerStorage(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddHangfireServer();
builder.Services.AddSwaggerGen(c => {
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme { In = Microsoft.OpenApi.Models.ParameterLocation.Header, Description = "Enter JWT: Bearer {token}", Name = "Authorization", Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey, Scheme = "Bearer" });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement { { new Microsoft.OpenApi.Models.OpenApiSecurityScheme { Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } }, new string[] {} } });
});
builder.Services.AddSignalR();
builder.Services.AddSingleton<DispatchRepository>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddScoped<OrderBackgroundService>();
builder.Services.AddRateLimiter(options => {
    options.AddFixedWindowLimiter("rider", opt => {
        opt.PermitLimit = 5; // rider can only call 5 times
        opt.Window = TimeSpan.FromSeconds(10); // in 10 seconds
    });
});

var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => { o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true, ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidAudience = builder.Configuration["Jwt:Audience"], IssuerSigningKey = new SymmetricSecurityKey(key) }; });

var app = builder.Build();
app.UseStaticFiles();
app.UseDefaultFiles();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.UseHangfireDashboard("/hangfire");
RecurringJob.AddOrUpdate<OrderBackgroundService>("reassign-stale-orders", s => s.ReassignStaleOrders(), Cron.Minutely);
app.MapHub<TrackingHub>("/hubs/tracking");
app.MapFallbackToFile("index.html");
app.Run();
