using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyDocuMgm.Application;
using MyDocuMgm.Application.Categories;
using MyDocuMgm.Application.CookingIngredients;
using MyDocuMgm.Application.Contents.UpdateWorkflowStep;
using MyDocuMgm.Application.UrlIntake;
using MyDocuMgm.Infrastructure.Data;
using MyDocuMgm.Infrastructure.Storage;

namespace MyDocuMgm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMyDocuMgmInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MyDocuMgm")
            ?? "Server=localhost\\MSSQLSERVER01;Database=MyDocuMgm;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
        services.AddDbContext<MyDocuMgmDbContext>(options => options.UseSqlServer(connectionString));
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
        services.AddScoped<LocalMediaStorage>();
        services.AddScoped<IMediaStorage>(provider => provider.GetRequiredService<LocalMediaStorage>());
        services.AddScoped<IMediaStorageReadiness>(provider => provider.GetRequiredService<LocalMediaStorage>());
        services.AddScoped<IMediaDiagnostics, MediaDiagnostics>();
        services.AddScoped<ContentService>();
        services.AddScoped<MediaService>();
        services.AddScoped<CookingIngredientService>();
        services.AddScoped<CategoryManagementService>();
        services.AddScoped<UpdateWorkflowStepService>();
        services.AddScoped<UrlIntakeService>();
        return services;
    }
}
