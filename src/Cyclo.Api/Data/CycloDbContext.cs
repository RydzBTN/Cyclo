using Cyclo.Api.Data.Entities.Activity;
using Cyclo.Api.Data.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace Cyclo.Api.Data;

public class CycloDbContext : DbContext
{
    public CycloDbContext(DbContextOptions<CycloDbContext> options) : base(options) {}
    
    public DbSet<ActivityEntity> Activities => Set<ActivityEntity>();
    public DbSet<ActivityFileEntity> ActivityFiles => Set<ActivityFileEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActivityEntity>(activity =>
        {
            activity.ToTable("Activities");
            activity.HasKey(x => x.Id);
            activity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            activity.Property(x => x.FileName).HasMaxLength(255);
            activity.Property(x => x.FileHash).IsUnicode(false).HasMaxLength(64).IsFixedLength();
            activity.Property(x => x.FailureReason).HasMaxLength(255);

            activity.OwnsOne(x => x.Summary, summary =>
            {
                summary.OwnsOne(x => x.Best1Km);
                summary.OwnsOne(x => x.Best5Km);
                summary.OwnsOne(x => x.Best10Km);
            });
            
            activity.HasIndex(x => new { x.UserId, x.FileHash }).IsUnique(); // deduplication
            activity.HasIndex(x => new { x.UserId, x.UploadedAt });

            activity.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActivityFileEntity>(file =>
        {
            file.ToTable("ActivityFiles");
            file.HasKey(x => x.ActivityId);
            
            file.HasOne<ActivityEntity>()
                .WithOne()
                .HasForeignKey<ActivityFileEntity>(x => x.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserEntity>(users =>
        {
            users.ToTable("Users");
            users.HasKey(x => x.Id);
            users.Property(x => x.Email).HasMaxLength(255);
            users.Property(x => x.PasswordHash).HasMaxLength(255).IsUnicode(false);
            users.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);

            users.HasIndex(x => x.Email).IsUnique();
        });
    }
}