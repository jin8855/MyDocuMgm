using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MyDocuMgm.Infrastructure.Data;

public sealed class MyDocuMgmDesignTimeDbContextFactory : IDesignTimeDbContextFactory<MyDocuMgmDbContext>
{
    public const string ConnectionStringEnvironmentVariable = "ConnectionStrings__MyDocuMgm";

    public MyDocuMgmDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set the {ConnectionStringEnvironmentVariable} process environment variable to an explicit design-time SQL Server connection before using EF tooling.");
        }

        var options = new DbContextOptionsBuilder<MyDocuMgmDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(MyDocuMgmDbContext).Assembly.FullName))
            .Options;
        return new MyDocuMgmDbContext(options);
    }
}
