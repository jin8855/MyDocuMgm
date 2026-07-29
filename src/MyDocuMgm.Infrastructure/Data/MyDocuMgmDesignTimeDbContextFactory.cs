using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MyDocuMgm.Infrastructure.Data;

public sealed class MyDocuMgmDesignTimeDbContextFactory : IDesignTimeDbContextFactory<MyDocuMgmDbContext>
{
    public MyDocuMgmDbContext CreateDbContext(string[] args)
    {
        const string designTimeOnlyConnection =
            "Server=localhost\\MSSQLSERVER01;Database=MyDocuMgm;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<MyDocuMgmDbContext>()
            .UseSqlServer(designTimeOnlyConnection, sql => sql.MigrationsAssembly(typeof(MyDocuMgmDbContext).Assembly.FullName))
            .Options;
        return new MyDocuMgmDbContext(options);
    }
}
