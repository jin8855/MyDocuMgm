using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyDocuMgm.Application;
using MyDocuMgm.Application.UrlIntake;
using MyDocuMgm.Domain;
using MyDocuMgm.Infrastructure.Data;
using MyDocuMgm.Infrastructure.Storage;

namespace MyDocuMgm.IntegrationTests;

internal sealed class Phase2ASqlUatFactAttribute : FactAttribute
{
    internal const string ConnectionVariable = "MYDOCUMGM_PHASE2A_SQL_UAT";

    public Phase2ASqlUatFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
        {
            Skip = $"Set {ConnectionVariable} to an approved disposable SQL Server database.";
        }
    }
}

public sealed class SqlServerUrlIntakePersistenceTests
{
    private const string ConnectionVariable = Phase2ASqlUatFactAttribute.ConnectionVariable;

    [Phase2ASqlUatFact]
    public async Task DisposableSqlServer_EnforcesConcurrentDuplicateAndUnlinkPreservesMediaAndFile()
    {
        var targetConnection = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(targetConnection))
        {
            throw new InvalidOperationException($"{ConnectionVariable} was not available during SQL UAT execution.");
        }

        var target = new SqlConnectionStringBuilder(targetConnection);
        if (!string.Equals(target.DataSource, @"localhost\MSSQLSERVER01", StringComparison.OrdinalIgnoreCase) ||
            !target.IntegratedSecurity ||
            !string.IsNullOrEmpty(target.UserID) ||
            !string.IsNullOrEmpty(target.Password) ||
            !Regex.IsMatch(target.InitialCatalog, "^MyDocuMgmPhase2A_Validation_[A-Za-z0-9_]+$"))
        {
            throw new InvalidOperationException("Phase 2A disposable SQL Server target is not approved or safe.");
        }

        var databaseName = target.InitialCatalog;
        var master = new SqlConnectionStringBuilder(targetConnection) { InitialCatalog = "master" };
        var mediaRoot = Path.Combine(Path.GetTempPath(), $"MyDocuMgm-phase2a-sql-media-{Guid.NewGuid():N}");
        await EnsureDatabaseMissingAsync(master.ConnectionString, databaseName);
        try
        {
            await CreateDatabaseAsync(master.ConnectionString, databaseName);
            var options = new DbContextOptionsBuilder<MyDocuMgmDbContext>()
                .UseSqlServer(target.ConnectionString)
                .Options;
            await using (var migrationContext = new MyDocuMgmDbContext(options))
            {
                await migrationContext.Database.MigrateAsync();
            }

            var requests = Enumerable.Range(0, 20)
                .Select(index => IntakeAsync(
                    options,
                    $"https://WWW.INSTAGRAM.com/reel/SqlBoundary_42/?share={index}#fragment"))
                .ToArray();
            var results = await Task.WhenAll(requests);

            Assert.Single(results.Select(result => result.Id).Distinct());
            Assert.Single(results, result => result.IsDuplicate is false);
            Assert.Equal(19, results.Count(result => result.IsDuplicate));
            await using (var verificationContext = new MyDocuMgmDbContext(options))
            {
                Assert.Equal(1, await verificationContext.Contents.CountAsync(
                    content => content.NormalizedUrlHash != null));
            }

            var mediaId = Guid.NewGuid();
            var bytes = "synthetic Phase 2A media boundary"u8.ToArray();
            var owner = new Content
            {
                CategoryId = CategoryCatalog.All.Single(category => category.Code == "OTHER").Id,
                Title = "Synthetic media owner"
            };
            var relativePath = $"media/{owner.Id:N}/{mediaId:N}/original/{mediaId:N}.png";
            var fullPath = Path.Combine(mediaRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllBytesAsync(fullPath, bytes);
            var expectedHash = Convert.ToHexString(SHA256.HashData(bytes));

            await using (var seedContext = new MyDocuMgmDbContext(options))
            {
                var media = new MediaAsset
                {
                    Id = mediaId,
                    ContentId = owner.Id,
                    OriginalFileName = "synthetic.png",
                    StoredFileName = $"{mediaId:N}.png",
                    RelativePath = relativePath,
                    MimeType = "image/png",
                    SizeBytes = bytes.Length,
                    Sha256 = expectedHash,
                    Width = 1,
                    Height = 1,
                    StorageStatus = MediaStorageStatus.READY
                };
                seedContext.Add(owner);
                seedContext.Add(media);
                await seedContext.SaveChangesAsync();
            }

            await using (var relationContext = new MyDocuMgmDbContext(options))
            {
                var repository = new EfUrlIntakeRepository(relationContext);
                var targetContent = await repository.FindAsync(results[0].Id, default);
                Assert.NotNull(targetContent);
                await repository.ReplaceLinkedMediaAsync(targetContent!, [mediaId], default);
                await repository.ReplaceLinkedMediaAsync(targetContent!, [], default);
            }

            await using (var finalContext = new MyDocuMgmDbContext(options))
            {
                var preservedMedia = await finalContext.MediaAssets.SingleAsync(media => media.Id == mediaId);
                Assert.Equal(relativePath, preservedMedia.RelativePath);
                Assert.False(await finalContext.ContentMediaLinks.AnyAsync(link => link.MediaAssetId == mediaId));
            }
            Assert.True(File.Exists(fullPath));
            Assert.Equal(bytes.Length, new FileInfo(fullPath).Length);
            Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(fullPath))));
        }
        finally
        {
            var cleanupErrors = new List<Exception>();
            if (Directory.Exists(mediaRoot))
            {
                try
                {
                    Directory.Delete(mediaRoot, recursive: true);
                }
                catch (Exception exception)
                {
                    cleanupErrors.Add(exception);
                }
            }
            SqlConnection.ClearAllPools();
            try
            {
                await DropDatabaseIfExistsAsync(master.ConnectionString, databaseName);
            }
            catch (Exception exception)
            {
                cleanupErrors.Add(exception);
            }
            if (cleanupErrors.Count > 0)
            {
                throw new AggregateException("Phase 2A disposable test cleanup failed.", cleanupErrors);
            }
        }
    }

    [Phase2ASqlUatFact]
    public async Task DisposableSqlServer_DeletesOrphanMediaBeforeSoftDeletedOwner()
    {
        var targetConnection = RequireApprovedTarget();
        var target = new SqlConnectionStringBuilder(targetConnection);
        var databaseName = target.InitialCatalog;
        var master = new SqlConnectionStringBuilder(targetConnection) { InitialCatalog = "master" };
        var mediaRoot = Path.Combine(Path.GetTempPath(), $"MyDocuMgm-cleanup-sql-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(mediaRoot);
        try
        {
            await EnsureDatabaseMissingAsync(master.ConnectionString, databaseName);
            await CreateDatabaseAsync(master.ConnectionString, databaseName);
            var options = new DbContextOptionsBuilder<MyDocuMgmDbContext>()
                .UseSqlServer(target.ConnectionString)
                .Options;
            await using (var migrationContext = new MyDocuMgmDbContext(options))
            {
                await migrationContext.Database.MigrateAsync();
            }

            var content = new Content
            {
                CategoryId = CategoryCatalog.All.Single(category => category.Code == "OTHER").Id,
                Title = "Synthetic cleanup owner"
            };
            content.SoftDelete();
            var mediaId = Guid.NewGuid();
            var storedFileName = $"{mediaId:N}.png";
            var relativePath = $"media/{content.Id:N}/{mediaId:N}/original/{storedFileName}";
            var fullPath = Path.Combine(mediaRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllBytesAsync(fullPath, [1, 2, 3, 4]);
            await using (var seedContext = new MyDocuMgmDbContext(options))
            {
                seedContext.Add(content);
                seedContext.Add(new MediaAsset
                {
                    Id = mediaId,
                    ContentId = content.Id,
                    OriginalFileName = "synthetic.png",
                    StoredFileName = storedFileName,
                    RelativePath = relativePath,
                    MimeType = "image/png",
                    SizeBytes = 4,
                    Sha256 = new string('A', 64),
                    Width = 1,
                    Height = 1,
                    StorageStatus = MediaStorageStatus.READY
                });
                await seedContext.SaveChangesAsync();
            }

            var storage = new LocalMediaStorage(Options.Create(new StorageOptions { RootPath = mediaRoot }));
            await using (var mediaContext = new MyDocuMgmDbContext(options))
            {
                var service = new CleanupService(
                    new EfCleanupRepository(mediaContext),
                    storage,
                    new NoopDiagnostics());
                await service.PermanentlyDeleteOrphanMediaAsync(mediaId, default);
            }

            Assert.False(File.Exists(fullPath));
            await using (var contentContext = new MyDocuMgmDbContext(options))
            {
                Assert.Equal(0, await contentContext.MediaAssets.IgnoreQueryFilters().CountAsync());
                Assert.Equal(1, await contentContext.Contents.IgnoreQueryFilters().CountAsync());
                var service = new CleanupService(
                    new EfCleanupRepository(contentContext),
                    storage,
                    new NoopDiagnostics());
                await service.PermanentlyDeleteContentAsync(content.Id, default);
            }

            await using (var finalContext = new MyDocuMgmDbContext(options))
            {
                Assert.Equal(0, await finalContext.MediaAssets.IgnoreQueryFilters().CountAsync());
                Assert.Equal(0, await finalContext.Contents.IgnoreQueryFilters().CountAsync());
                Assert.Equal(0, await finalContext.ContentMediaLinks.CountAsync());
            }
        }
        finally
        {
            if (Directory.Exists(mediaRoot)) Directory.Delete(mediaRoot, recursive: true);
            SqlConnection.ClearAllPools();
            await DropDatabaseIfExistsAsync(master.ConnectionString, databaseName);
        }
    }

    private static string RequireApprovedTarget()
    {
        var targetConnection = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(targetConnection))
        {
            throw new InvalidOperationException($"{ConnectionVariable} was not available during SQL UAT execution.");
        }

        var target = new SqlConnectionStringBuilder(targetConnection);
        if (!string.Equals(target.DataSource, @"localhost\MSSQLSERVER01", StringComparison.OrdinalIgnoreCase) ||
            !target.IntegratedSecurity ||
            !string.IsNullOrEmpty(target.UserID) ||
            !string.IsNullOrEmpty(target.Password) ||
            !Regex.IsMatch(target.InitialCatalog, "^MyDocuMgmPhase2A_Validation_[A-Za-z0-9_]+$"))
        {
            throw new InvalidOperationException("Phase 2A disposable SQL Server target is not approved or safe.");
        }

        return targetConnection;
    }

    private sealed class NoopDiagnostics : IMediaDiagnostics
    {
        public void Record(string code, Guid? contentId, Guid? mediaId = null) { }
    }

    private static async Task<UrlIntakeDto> IntakeAsync(
        DbContextOptions<MyDocuMgmDbContext> options,
        string url)
    {
        await using var context = new MyDocuMgmDbContext(options);
        var service = new UrlIntakeService(new EfUrlIntakeRepository(context));
        return await service.IntakeAsync(new CreateUrlIntakeRequest(url), default);
    }

    private static async Task EnsureDatabaseMissingAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT DB_ID(@databaseName)";
        check.Parameters.AddWithValue("@databaseName", databaseName);
        if (await check.ExecuteScalarAsync() is not DBNull)
        {
            throw new InvalidOperationException("Phase 2A disposable database already exists.");
        }
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseIfExistsAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END";
        await command.ExecuteNonQueryAsync();
    }
}
