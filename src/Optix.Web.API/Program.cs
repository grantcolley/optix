using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Optix.Web.API.Authorization;
using Optix.Web.API.Config;
using Optix.Web.API.Constants;
using Serilog;
using Serilog.Events;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

string? domain = builder.Configuration[ConfigKeys.AUTH_DOMAIN] ?? throw new NullReferenceException(ConfigKeys.AUTH_DOMAIN);
string? audience = builder.Configuration[ConfigKeys.AUTH_AUDIENCE] ?? throw new NullReferenceException(ConfigKeys.AUTH_AUDIENCE);
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));

builder.Host.UseSerilog((ctx, lc) =>
{
    // %HOME% exists on App Service (Windows + Linux). On Windows it maps to D:\home.
    var home = Environment.GetEnvironmentVariable("HOME") ?? "";
    var logDir = Path.Combine(home, "LogFiles", "Application");
    Directory.CreateDirectory(logDir);

    lc.ReadFrom.Configuration(ctx.Configuration)
      .MinimumLevel.Is(LogEventLevel.Warning)
      .WriteTo.Console()
      .WriteTo.File(
          path: Path.Combine(logDir, "optix-.log"),
          rollingInterval: RollingInterval.Day,
          retainedFileCountLimit: 7,
          shared: true);
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = domain;
        options.Audience = audience;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = domain,
            ValidAudience = audience,
            NameClaimType = "sub",
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Claims.OPTIX_CLIENT_ID, policy =>
    {
        policy.AddRequirements(new AllowedClientRequirement());
    })
    .AddPolicy(Claims.OPTIX_USER_CLAIM, policy =>
    {
        policy.RequireAuthenticatedUser().RequireClaim("permissions", Claims.OPTIX_USER_CLAIM);
    })
    .AddPolicy(Claims.OPTIX_DEVELOPER_CLAIM, policy =>
    {
        policy.RequireAuthenticatedUser().RequireClaim("permissions", Claims.OPTIX_DEVELOPER_CLAIM);
    });

// Add services to the container.
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IAuthorizationHandler, AllowedClientHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
    .WithName("weatherforecast")
    .WithDescription("Gets a 5-day weather forecast")
    .Produces<WeatherForecast[]>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status401Unauthorized)
    .Produces(StatusCodes.Status403Forbidden)
    .Produces(StatusCodes.Status500InternalServerError)
    .RequireAuthorization(Claims.OPTIX_CLIENT_ID, Claims.OPTIX_USER_CLAIM);

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
