using System.Text.Json.Serialization;
using RentApp.Api.Configuration;
using RentApp.Api.Errors;
using RentApp.Api.Health;
using RentApp.Api.Middleware;
using RentApp.Infrastructure;
using RentApp.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration -------------------------------------------------------------------------------
// Local development reads the repo-root .env (shared with docker compose). Other environments get
// real environment variables / secret stores; .env is never loaded there.
if (builder.Environment.IsDevelopment())
{
    DotEnvFile.LoadFromNearest(builder.Environment.ContentRootPath);
}

builder.Configuration.AddFlatEnvironmentVariables();

// ---- Logging -------------------------------------------------------------------------------------
builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddSimpleConsole(o =>
    {
        o.IncludeScopes = true;
        o.SingleLine = true;
        o.TimestampFormat = "HH:mm:ss ";
    });
}
else
{
    builder.Logging.AddJsonConsole(o =>
    {
        o.IncludeScopes = true;
        o.UseUtcTimestamp = true;
        o.TimestampFormat = "O";
    });
}

// ---- Services ------------------------------------------------------------------------------------
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ApiErrors.InvalidModelState);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddApiCors(builder.Configuration);

var app = builder.Build();

// ---- Pipeline ------------------------------------------------------------------------------------
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages(ApiErrors.WriteStatusCodeBody);

var isLocal = app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing");
if (!isLocal)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/openapi/v1.json", "RentApp API v1");
        o.RoutePrefix = "swagger";
        o.DocumentTitle = "RentApp API";
    });
}

app.MapHealthEndpoints();
app.MapControllers();

if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await app.Services.ApplyMigrationsAsync();
}

await app.RunAsync();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;
