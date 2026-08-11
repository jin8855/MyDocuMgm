using Microsoft.EntityFrameworkCore;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyDocuMgm.Application;
using MyDocuMgm.Application.Categories;
using MyDocuMgm.Application.CookingIngredients;
using MyDocuMgm.Application.Contents.WriteBlogDraft;
using MyDocuMgm.Application.Contents.EditCategory;
using MyDocuMgm.Application.Contents.EditImage;
using MyDocuMgm.Application.Contents.ReviewAnalysis;
using MyDocuMgm.Application.Contents.ReviewDetail;
using MyDocuMgm.Application.Contents.UpdateWorkflowStep;
using MyDocuMgm.Application.ExternalFetch;
using MyDocuMgm.Application.UrlIntake;
using MyDocuMgm.Infrastructure.Data;
using MyDocuMgm.Infrastructure.ExternalFetch;
using MyDocuMgm.Infrastructure.Storage;

namespace MyDocuMgm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMyDocuMgmInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MyDocuMgm")
            ?? "Server=localhost\\MSSQLSERVER01;Database=MyDocuMgm;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
        services.AddDbContext<MyDocuMgmDbContext>(options => options.UseSqlServer(connectionString));
        var externalFetchSection = configuration.GetSection(ExternalFetchOptions.SectionName);
        services.Configure<ExternalFetchOptions>(options =>
        {
            if (int.TryParse(externalFetchSection[nameof(ExternalFetchOptions.ConnectTimeoutSeconds)], out var connectTimeout))
            {
                options.ConnectTimeoutSeconds = connectTimeout;
            }
            if (int.TryParse(externalFetchSection[nameof(ExternalFetchOptions.TotalTimeoutSeconds)], out var totalTimeout))
            {
                options.TotalTimeoutSeconds = totalTimeout;
            }
            if (int.TryParse(externalFetchSection[nameof(ExternalFetchOptions.MaxRedirects)], out var maxRedirects))
            {
                options.MaxRedirects = maxRedirects;
            }
            if (int.TryParse(externalFetchSection[nameof(ExternalFetchOptions.MaxRobotsRedirects)], out var maxRobotsRedirects))
            {
                options.MaxRobotsRedirects = maxRobotsRedirects;
            }
            if (int.TryParse(externalFetchSection[nameof(ExternalFetchOptions.MaxHtmlBytes)], out var maxHtmlBytes))
            {
                options.MaxHtmlBytes = maxHtmlBytes;
            }
            if (int.TryParse(externalFetchSection[nameof(ExternalFetchOptions.MaxRobotsBytes)], out var maxRobotsBytes))
            {
                options.MaxRobotsBytes = maxRobotsBytes;
            }
        });
        var storageSection = configuration.GetSection(StorageOptions.SectionName);
        services.Configure<StorageOptions>(options =>
        {
            options.RootPath = storageSection[nameof(StorageOptions.RootPath)] ?? string.Empty;
            if (long.TryParse(storageSection[nameof(StorageOptions.MaxImageBytes)], out var maxImageBytes))
            {
                options.MaxImageBytes = maxImageBytes;
            }

            if (int.TryParse(storageSection[nameof(StorageOptions.MaxPixelWidth)], out var maxPixelWidth))
            {
                options.MaxPixelWidth = maxPixelWidth;
            }

            if (int.TryParse(storageSection[nameof(StorageOptions.MaxPixelHeight)], out var maxPixelHeight))
            {
                options.MaxPixelHeight = maxPixelHeight;
            }

            if (long.TryParse(storageSection[nameof(StorageOptions.MaxTotalPixels)], out var maxTotalPixels))
            {
                options.MaxTotalPixels = maxTotalPixels;
            }

            if (int.TryParse(storageSection[nameof(StorageOptions.ThumbnailMaxPixels)], out var thumbnailMaxPixels))
            {
                options.ThumbnailMaxPixels = thumbnailMaxPixels;
            }
        });
        services.AddScoped<IContentRepository, EfContentRepository>();
        services.AddScoped<IMediaAssetRepository, EfMediaAssetRepository>();
        services.AddScoped<ICookingIngredientRepository, EfCookingIngredientRepository>();
        services.AddScoped<ICategoryManagementRepository, EfCategoryManagementRepository>();
        services.AddScoped<IUrlIntakeRepository, EfUrlIntakeRepository>();
        services.AddScoped<IExternalFetchRepository, EfExternalFetchRepository>();
        services.AddScoped<IImageStageRepository, EfImageStageRepository>();
        services.AddScoped<IDetailStageRepository, EfDetailStageRepository>();
        services.AddScoped<IBlogDraftRepository, EfBlogDraftRepository>();
        services.AddScoped<ICleanupRepository, EfCleanupRepository>();
        services.AddScoped<LocalMediaStorage>();
        services.AddScoped<IMediaStorage>(provider => provider.GetRequiredService<LocalMediaStorage>());
        services.AddScoped<IMediaCleanupStorage>(provider => provider.GetRequiredService<LocalMediaStorage>());
        services.AddScoped<IMediaStorageReadiness>(provider => provider.GetRequiredService<LocalMediaStorage>());
        services.AddScoped<IMediaDiagnostics, MediaDiagnostics>();
        services.AddScoped<ContentService>();
        services.AddScoped<MediaService>();
        services.AddScoped<CookingIngredientService>();
        services.AddScoped<CategoryManagementService>();
        services.AddScoped<CategoryEditService>();
        services.AddScoped<ImageStageService>();
        services.AddScoped<DetailStageService>();
        services.AddScoped<BlogDraftService>();
        services.AddScoped<UpdateWorkflowStepService>();
        services.AddScoped<AnalysisReviewService>();
        services.AddScoped<UrlIntakeService>();
        services.AddScoped<CleanupService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IDnsResolver, SystemDnsResolver>();
        services.AddSingleton<SsrfSafeDestinationValidator>();
        services.AddSingleton(_ => new RobotsPolicyEvaluator(ExternalFetchOptions.RobotsProductToken));
        services.AddSingleton<IHtmlContentExtractor, HtmlContentExtractor>();
        services.AddScoped<ExternalFetchService>();
        services.AddSingleton<IExternalPageFetcher>(provider =>
            {
                var destinationValidator = provider.GetRequiredService<SsrfSafeDestinationValidator>();
                var handler = new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    UseCookies = false,
                    UseProxy = false,
                    Credentials = null,
                    AutomaticDecompression =
                        DecompressionMethods.GZip |
                        DecompressionMethods.Deflate |
                        DecompressionMethods.Brotli,
                    ConnectTimeout = TimeSpan.FromSeconds(ExternalFetchOptions.ApprovedConnectTimeoutSeconds),
                    ConnectCallback = ExternalHttpPageFetcher.ConnectPinnedAsync,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(2)
                };
                var client = new HttpClient(handler, disposeHandler: true);
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.UserAgent.ParseAdd(ExternalFetchOptions.HttpUserAgent);
                return new ExternalHttpPageFetcher(
                    client,
                    destinationValidator,
                    provider.GetRequiredService<RobotsPolicyEvaluator>(),
                    provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ExternalFetchOptions>>());
            });
        return services;
    }
}
