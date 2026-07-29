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
            Detail = exception is DomainRuleException or InvalidDataException or ConcurrencyConflictException
                ? exception.Message
                : null,
            Extensions = { ["code"] = code }
        });
    });
});
app.UseCors("MyDocuMgmWeb");
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", (IConfiguration configuration) =>
{
    var connectionConfigured = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("MyDocuMgm"));
    var storage = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>();
    var storageConfigured = storage is { RootPath.Length: > 0 };
    var storageExists = storageConfigured && Directory.Exists(storage!.RootPath);
    var body = new
    {
        status = connectionConfigured && storageExists ? "ready" : "not-ready",
        database = connectionConfigured ? "configured-not-probed" : "not-configured",
        storage = !storageConfigured ? "not-configured" : storageExists ? "available" : "root-missing"
    };
    return connectionConfigured && storageExists ? Results.Ok(body) : Results.Json(body, statusCode: 503);
});

app.Run();

public partial class Program;
