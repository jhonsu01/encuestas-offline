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
    public DbSet<LocationEventRow> LocationEvents => Set<LocationEventRow>();

    /// <summary>
    /// Crea las tablas añadidas después de la primera versión sin borrar datos.
    /// EnsureCreated no altera BD existentes, así que aseguramos aquí las tablas nuevas.
    /// </summary>
    public void EnsureAuxSchema()
    {
        Database.ExecuteSqlRaw(
            "CREATE TABLE IF NOT EXISTS \"LocationEvents\" (" +
            "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_LocationEvents\" PRIMARY KEY AUTOINCREMENT, " +
            "\"SurveyorId\" TEXT NOT NULL, \"DeviceId\" TEXT NOT NULL, \"Type\" TEXT NOT NULL, " +
            "\"Latitude\" REAL NULL, \"Longitude\" REAL NULL, \"Timestamp\" TEXT NOT NULL, " +
            "\"ReceivedAt\" TEXT NOT NULL);");
    }

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
