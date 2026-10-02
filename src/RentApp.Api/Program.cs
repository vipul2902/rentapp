using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using RentApp.Api.Auth;
using RentApp.Api.Configuration;
using RentApp.Api.Errors;
using RentApp.Api.Health;
using RentApp.Api.Jobs;
using RentApp.Api.Middleware;
using RentApp.Api.OpenApi;
using RentApp.Application;
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
// AddApiAuth registers the HTTP-based ICurrentUser before Infrastructure adds its anonymous fallback.
builder.Services.AddApiAuth();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiRateLimiting(builder.Configuration);

builder.Services.Configure<RentGenerationOptions>(builder.Configuration.GetSection(RentGenerationOptions.SectionName));
builder.Services.AddSingleton<RentGenerationWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RentGenerationWorker>());

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services
    // Validation error keys use JSON (camelCase) names, matching what the client sent.
    .AddControllers(o => o.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ApiErrors.InvalidModelState);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi(o =>
{
    o.AddDocumentTransformer<BearerSecurityTransformer>();
    o.AddOperationTransformer<BearerSecurityTransformer>();
});
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

if (app.Environment.IsDevelopment())
{
    // Swagger UI is middleware, not an endpoint, so it must run before the deny-by-default authorization
    // fallback (which also applies to requests that match no endpoint).
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/openapi/v1.json", "RentApp API v1");
        o.RoutePrefix = "swagger";
        o.DocumentTitle = "RentApp API";
        o.EnablePersistAuthorization();
    });
    app.MapOpenApi().AllowAnonymous();
}

app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapControllers();

if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await app.Services.ApplyMigrationsAsync();
}

app.Services.WarmUpRedis();

await app.RunAsync();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;
