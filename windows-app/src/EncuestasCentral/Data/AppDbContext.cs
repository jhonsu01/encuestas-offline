using Microsoft.EntityFrameworkCore;
using System.IO;

namespace EncuestasCentral.Data;

public class AppDbContext : DbContext
{
    public AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<SurveyRow> Surveys => Set<SurveyRow>();
    public DbSet<SurveyorRow> Surveyors => Set<SurveyorRow>();
    public DbSet<ResponseRow> Responses => Set<ResponseRow>();
    public DbSet<DeviceRow> Devices => Set<DeviceRow>();
    public DbSet<BatchRow> Batches => Set<BatchRow>();
    public DbSet<LogRow> Logs => Set<LogRow>();

    public static string DbPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EncuestasCentral");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "central.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite($"Data Source={DbPath()}");
        }
    }
}
