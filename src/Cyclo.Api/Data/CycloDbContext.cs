using Cyclo.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cyclo.Api.Data;

public class CycloDbContext : DbContext
{
    public CycloDbContext(DbContextOptions<CycloDbContext> options) : base(options) {}
    
    public DbSet<ActivityEntity> Activities => Set<ActivityEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActivityEntity>(activity =>
        {
            activity.ToTable("Activities");
            activity.HasKey(x => x.Id);
            activity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            activity.Property(x => x.FileName).HasMaxLength(255);
            activity.Property(x => x.FileHash).IsUnicode(false).HasMaxLength(64).IsFixedLength();

            activity.OwnsOne(x => x.Summary, summary =>
            {
                summary.OwnsOne(x => x.Best1Km);
                summary.OwnsOne(x => x.Best5Km);
                summary.OwnsOne(x => x.Best10Km);
            });
            
            activity.HasIndex(x => new { x.UserId, x.FileHash }).IsUnique(); // deduplication
            activity.HasIndex(x => new { x.UserId, x.UploadedAt });
        });
    }
}