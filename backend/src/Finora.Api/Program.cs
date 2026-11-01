using Finora.Api.Middleware;
using Finora.Api.Services;
using Finora.Application.Common;
using Finora.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 1. Core Services & Context Accessor
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<CurrentUserService>());

// 2. Infrastructure Layer (.NET Identity, EF Core, PostgreSQL / In-Memory, JWT, Auditing)
builder.Services.AddInfrastructure(builder.Configuration);

// 3. Controllers & Web API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 4. CORS Configuration
var allowedOrigins = builder.Configuration["ALLOWED_ORIGINS"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// 5. Middleware Pipeline
app.UseCors("DefaultCorsPolicy");

app.UseAuthentication();
app.UseMiddleware<TenancyMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Version = "1.0.0",
    Timestamp = DateTimeOffset.UtcNow
}));

app.MapControllers();

app.Run();

// For WebApplicationFactory integration testing
public partial class Program { }

