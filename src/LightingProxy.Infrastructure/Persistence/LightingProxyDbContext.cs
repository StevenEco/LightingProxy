using LightingProxy.Domain.Enums;
using LightingProxy.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LightingProxy.Infrastructure.Persistence;

public class LightingProxyDbContext : DbContext
{
    public LightingProxyDbContext(DbContextOptions<LightingProxyDbContext> options)
        : base(options)
    {
    }

    public DbSet<ConfigurationRecord> ConfigurationRecords => Set<ConfigurationRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ConfigurationRecord>();

        entity.ToTable("configuration_records");
        entity.HasKey(record => record.Id);
        entity.Property(record => record.Name).HasMaxLength(128).IsRequired();
        entity.Property(record => record.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
        entity.Property(record => record.Content).IsRequired();
        entity.Property(record => record.UpdatedAt).IsRequired();
        entity.HasIndex(record => new { record.Name, record.Kind }).IsUnique();
    }
}
