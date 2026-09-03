using Depicta.Api.Domain.Entities;
using Depicta.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Depicta.Api.Infrastructure;

/// <summary>
/// EF Core context. Enums are stored as strings (readable in the DB, stable across
/// reordering). Designed for PostgreSQL or SQL Server — swap the provider in Program.cs.
/// </summary>
public class DepictaDbContext : DbContext
{
    public DepictaDbContext(DbContextOptions<DepictaDbContext> options) : base(options) { }

    public DbSet<Site> Sites => Set<Site>();
    public DbSet<ScanRun> ScanRuns => Set<ScanRun>();
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<AltProposal> AltProposals => Set<AltProposal>();
    public DbSet<AltRevision> AltRevisions => Set<AltRevision>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Site>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Url).HasMaxLength(2048).IsRequired();
            e.Property(x => x.WpUsername).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.Url).IsUnique();
        });

        b.Entity<ScanRun>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasOne(x => x.Site).WithMany(s => s.ScanRuns)
             .HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.SiteId, x.StartedAt });
        });

        b.Entity<MediaItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.State).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Url).HasMaxLength(2048).IsRequired();
            e.Property(x => x.CurrentAlt).HasMaxLength(1024);
            e.HasOne(x => x.Site).WithMany(s => s.MediaItems)
             .HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);

            // One row per attachment per site — scans upsert on this key.
            e.HasIndex(x => new { x.SiteId, x.WpAttachmentId }).IsUnique();
            // Hot path: the review queue and the worker both filter by state.
            e.HasIndex(x => new { x.SiteId, x.State });
        });

        b.Entity<AltProposal>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ProposedAlt).HasMaxLength(1024).IsRequired();
            e.Property(x => x.EditedAlt).HasMaxLength(1024);
            e.Property(x => x.Provider).HasMaxLength(32).IsRequired();
            e.Property(x => x.Model).HasMaxLength(128).IsRequired();
            e.Ignore(x => x.EffectiveAlt);
            e.HasOne(x => x.MediaItem).WithMany(m => m.Proposals)
             .HasForeignKey(x => x.MediaItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.MediaItemId);
        });

        b.Entity<AltRevision>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PreviousAlt).HasMaxLength(1024);
            e.Property(x => x.NewAlt).HasMaxLength(1024).IsRequired();
            e.HasOne(x => x.MediaItem).WithMany(m => m.Revisions)
             .HasForeignKey(x => x.MediaItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.MediaItemId, x.AppliedAt });
        });

        // SQLite can't ORDER BY a DateTimeOffset. Store them as a sortable long there.
        // (No-op on PostgreSQL, which orders DateTimeOffset natively.)
        if (Database.IsSqlite())
        {
            var toTicks = new DateTimeOffsetToBinaryConverter();
            foreach (var entity in b.Model.GetEntityTypes())
                foreach (var prop in entity.GetProperties())
                    if (prop.ClrType == typeof(DateTimeOffset) || prop.ClrType == typeof(DateTimeOffset?))
                        prop.SetValueConverter(toTicks);
        }
    }
}
