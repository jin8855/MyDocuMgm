using System.Data;
using Microsoft.EntityFrameworkCore;
using MyDocuMgm.Infrastructure.Data;

namespace MyDocuMgm.UnitTests;

[Collection(ProcessEnvironmentCollection.Name)]
public sealed class MyDocuMgmDesignTimeDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_RejectsMissingConnectionString()
    {
        using var environment = new ProcessEnvironmentVariableScope(
            MyDocuMgmDesignTimeDbContextFactory.ConnectionStringEnvironmentVariable,
            null);

        var error = Assert.Throws<InvalidOperationException>(
            () => new MyDocuMgmDesignTimeDbContextFactory().CreateDbContext([]));

        Assert.Contains(
            MyDocuMgmDesignTimeDbContextFactory.ConnectionStringEnvironmentVariable,
            error.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void CreateDbContext_RejectsBlankConnectionString(string value)
    {
        using var environment = new ProcessEnvironmentVariableScope(
            MyDocuMgmDesignTimeDbContextFactory.ConnectionStringEnvironmentVariable,
            value);

        Assert.Throws<InvalidOperationException>(
            () => new MyDocuMgmDesignTimeDbContextFactory().CreateDbContext([]));
    }

    [Fact]
    public void CreateDbContext_UsesExactExplicitDatabaseWithoutOpeningConnection()
    {
        var databaseName = $"MyDocuMgm_P2C_Repair_{Guid.NewGuid():N}";
        var connectionString =
            $"Server=jin\\MSSQLSERVER01;Database={databaseName};Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
        using var environment = new ProcessEnvironmentVariableScope(
            MyDocuMgmDesignTimeDbContextFactory.ConnectionStringEnvironmentVariable,
            connectionString);

        using var context = new MyDocuMgmDesignTimeDbContextFactory().CreateDbContext([]);
        var connection = context.Database.GetDbConnection();

        Assert.Equal(@"jin\MSSQLSERVER01", connection.DataSource);
        Assert.Equal(databaseName, connection.Database);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessEnvironmentCollection
{
    public const string Name = "Process environment variable tests";
}

internal sealed class ProcessEnvironmentVariableScope : IDisposable
{
    private readonly string _name;
    private readonly string? _originalValue;
    private bool _disposed;

    public ProcessEnvironmentVariableScope(string name, string? value)
    {
        _name = name;
        _originalValue = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Environment.SetEnvironmentVariable(
            _name,
            _originalValue,
            EnvironmentVariableTarget.Process);
        _disposed = true;
    }
}
