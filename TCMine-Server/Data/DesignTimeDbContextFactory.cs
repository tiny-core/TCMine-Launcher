using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TCMine_Server.Data;

/// <summary>
///     Usado apenas pelas ferramentas EF (<c>dotnet ef migrations</c>) para criar o
///     contexto em tempo de design, sem arrancar a aplicação web.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=tcmine.db")
            .Options;
        return new AppDbContext(options);
    }
}