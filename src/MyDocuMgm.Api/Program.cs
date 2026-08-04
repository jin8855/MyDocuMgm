using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyDocuMgm.Application;
using MyDocuMgm.Domain;
using MyDocuMgm.Infrastructure;
using MyDocuMgm.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);
var localSettingsPath = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".local", "appsettings.Local.json"));
builder.Configuration.AddJsonFile(localSettingsPath, optional: true, reloadOnChange: true);

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
    options.AddPolicy("MyDocuMgmWeb", policy =>
        policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddMyDocuMgmInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var (status, title, code) = exception switch
        {
            MediaOperationException media => (MediaStatus(media.Code), media.Message, media.Code),
            NotFoundException => (StatusCodes.Status404NotFound, "대상을 찾을 수 없습니다.", "NOT_FOUND"),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "동시 수정 충돌", "CONCURRENCY_CONFLICT"),
            DomainRuleException domain => (StatusCodes.Status400BadRequest, domain.Message, domain.Code),
            InvalidDataException invalid => (StatusCodes.Status400BadRequest, invalid.Message, "INVALID_MEDIA"),
            _ => (StatusCodes.Status500InternalServerError, "요청을 처리하지 못했습니다.", "UNEXPECTED_ERROR")
        };
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception is MediaOperationException or DomainRuleException or InvalidDataException or ConcurrencyConflictException
                ? exception.Message
                : null,
            Extensions = { ["code"] = code }
        });
    });
});
app.UseCors("MyDocuMgmWeb");
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (
    IConfiguration configuration,
    IMediaStorageReadiness storageReadiness,
    CancellationToken cancellationToken) =>
{
    var connectionConfigured = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("MyDocuMgm"));
    var storage = await storageReadiness.CheckReadinessAsync(cancellationToken);
    var body = new
    {
        status = connectionConfigured && storage.IsReady ? "ready" : "not-ready",
        database = connectionConfigured ? "configured-not-probed" : "not-configured",
        storage = storage.Code
    };
    return connectionConfigured && storage.IsReady ? Results.Ok(body) : Results.Json(body, statusCode: 503);
});

app.Run();

static int MediaStatus(string code) => code switch
{
    "MEDIA_CONTENT_NOT_FOUND" or "MEDIA_NOT_FOUND" => StatusCodes.Status404NotFound,
    "MEDIA_FILE_TOO_LARGE" => StatusCodes.Status413PayloadTooLarge,
    "MEDIA_EXTENSION_NOT_ALLOWED" or
        "MEDIA_MIME_MISMATCH" or
        "MEDIA_SIGNATURE_INVALID" or
        "MEDIA_DECODE_FAILED" => StatusCodes.Status415UnsupportedMediaType,
    "MEDIA_CONCURRENCY_CONFLICT" or
        "MEDIA_RESTORE_BLOCKED" or
        "MEDIA_NOT_DELETED" or
        "MEDIA_FILE_MISSING" or
        "MEDIA_INTEGRITY_SIZE_MISMATCH" or
        "MEDIA_INTEGRITY_HASH_MISMATCH" => StatusCodes.Status409Conflict,
    "MEDIA_STORAGE_NOT_READY" or
        "MEDIA_STORAGE_ACCESS_DENIED" or
        "MEDIA_STORAGE_WRITE_FAILED" or
        "MEDIA_STORAGE_READ_FAILED" or
        "MEDIA_STORAGE_DELETE_FAILED" or
        "MEDIA_PROMOTION_FAILED" or
        "MEDIA_THUMBNAIL_GENERATION_FAILED" or
        "MEDIA_PERSISTENCE_FAILED" => StatusCodes.Status503ServiceUnavailable,
    _ => StatusCodes.Status400BadRequest
};

public partial class Program;
