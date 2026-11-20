using Listmate.Application.Sync;
using Listmate.Domain.Items;
using Listmate.Domain.Lists;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Listmate.Infrastructure.Persistence;

public sealed class ListmateDbContext(DbContextOptions<ListmateDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<SharedList> Lists => Set<SharedList>();

    public DbSet<ListItem> Items => Set<ListItem>();

    public DbSet<ProcessedMutation> ProcessedMutations => Set<ProcessedMutation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<SharedList>(ConfigureList);
        builder.Entity<Membership>(ConfigureMembership);
        builder.Entity<ListItem>(ConfigureItem);
        builder.Entity<ProcessedMutation>(ConfigureProcessed);
    }

    private static void ConfigureList(EntityTypeBuilder<SharedList> list)
    {
        list.ToTable("lists");
        list.HasKey(l => l.Id);
        list.Property(l => l.Id).ValueGeneratedNever();
        list.Property(l => l.Name).HasMaxLength(SharedList.MaxNameLength);
        list.Property(l => l.InviteCode).HasMaxLength(16);
        list.HasIndex(l => l.InviteCode).IsUnique();
        list.HasMany(l => l.Members).WithOne().HasForeignKey(m => m.ListId).OnDelete(DeleteBehavior.Cascade);
        list.Navigation(l => l.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
        AddRowVersion(list);
    }

    private static void ConfigureMembership(EntityTypeBuilder<Membership> member)
    {
        member.ToTable("list_members");
        member.HasKey(m => new { m.ListId, m.UserId });
        member.Property(m => m.Role).HasConversion<string>().HasMaxLength(16);
        member.HasIndex(m => m.UserId);
    }

    private static void ConfigureItem(EntityTypeBuilder<ListItem> item)
    {
        item.ToTable("list_items");
        item.HasKey(i => i.Id);
        item.Property(i => i.Id).ValueGeneratedNever();
        item.Property(i => i.Name).HasMaxLength(ItemText.MaxNameLength);
        item.Property(i => i.ChangedBy).HasMaxLength(64);
        item.HasOne<SharedList>().WithMany().HasForeignKey(i => i.ListId).OnDelete(DeleteBehavior.Cascade);
        // The resync query: WHERE list_id = @id AND version > @since.
        item.HasIndex(i => new { i.ListId, i.Version });
        AddRowVersion(item);
    }

    private static void ConfigureProcessed(EntityTypeBuilder<ProcessedMutation> processed)
    {
        processed.ToTable("processed_mutations");
        processed.HasKey(p => p.MutationId);
        processed.Property(p => p.MutationId).ValueGeneratedNever();
        processed.Property(p => p.UserId).HasMaxLength(64);
        processed.Property(p => p.Status).HasMaxLength(16);
        processed.HasIndex(p => p.ProcessedAt);
    }

    /// <summary>Postgres' system column xmin changes on every row update: a free optimistic-concurrency token.</summary>
    private static void AddRowVersion<T>(EntityTypeBuilder<T> entity)
        where T : class =>
        entity.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
}
