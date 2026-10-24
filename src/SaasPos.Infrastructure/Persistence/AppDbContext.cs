using Microsoft.EntityFrameworkCore;
using SaasPos.Application.Abstractions;
using SaasPos.Domain.Common;
using SaasPos.Domain.Entities;

namespace SaasPos.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();

    // Evaluated per query, so each request filters by its own tenant.
    private Guid CurrentTenantId => tenant.TenantId;

    protected override void OnModelCreating(ModelBuilder b)
    {
        foreach (var t in b.Model.GetEntityTypes().Select(e => e.ClrType).Where(t => typeof(Entity).IsAssignableFrom(t)))
            b.Entity(t).Property(nameof(Entity.Id)).ValueGeneratedNever();

        b.Entity<Tenant>().HasIndex(t => t.Code).IsUnique();

        b.Entity<User>(e =>
        {
            e.HasIndex(u => new { u.TenantId, u.Username }).IsUnique();
            e.HasQueryFilter(u => u.TenantId == CurrentTenantId);
        });

        b.Entity<Category>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(100);
            e.HasQueryFilter(c => c.TenantId == CurrentTenantId);
        });

        b.Entity<Product>(e =>
        {
            e.Property(p => p.Sku).HasMaxLength(50);
            e.Property(p => p.Name).HasMaxLength(200);
            e.Property(p => p.Price).HasPrecision(18, 2);
            e.Property(p => p.Cost).HasPrecision(18, 2);
            e.Ignore(p => p.IsLowStock);
            e.HasIndex(p => new { p.TenantId, p.Sku }).IsUnique();
            e.HasIndex(p => new { p.TenantId, p.Barcode });
            e.HasMany(p => p.Movements).WithOne().HasForeignKey(m => m.ProductId);
            e.Navigation(p => p.Movements).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.HasQueryFilter(p => p.TenantId == CurrentTenantId);
        });

        b.Entity<StockMovement>().HasQueryFilter(m => m.TenantId == CurrentTenantId);
        b.Entity<Customer>().HasQueryFilter(c => c.TenantId == CurrentTenantId);

        b.Entity<Sale>(e =>
        {
            e.Property(s => s.Discount).HasPrecision(18, 2);
            e.Property(s => s.AmountTendered).HasPrecision(18, 2);
            e.Ignore(s => s.Subtotal);
            e.Ignore(s => s.Total);
            e.Ignore(s => s.Change);
            e.HasIndex(s => new { s.TenantId, s.ReceiptNumber }).IsUnique();
            e.HasIndex(s => new { s.TenantId, s.CreatedAtUtc });
            e.HasMany(s => s.Items).WithOne().HasForeignKey(i => i.SaleId);
            e.Navigation(s => s.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.HasQueryFilter(s => s.TenantId == CurrentTenantId);
        });

        b.Entity<SaleItem>(e =>
        {
            e.Property(i => i.UnitPrice).HasPrecision(18, 2);
            e.Property(i => i.UnitCost).HasPrecision(18, 2);
            e.Ignore(i => i.LineTotal);
            e.HasQueryFilter(i => i.TenantId == CurrentTenantId);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Defense in depth: an authenticated request may never write another tenant's data.
        if (CurrentTenantId != Guid.Empty)
        {
            var foreign = ChangeTracker.Entries<TenantEntity>()
                .Any(e => e.State != EntityState.Unchanged && e.Entity.TenantId != CurrentTenantId);
            if (foreign) throw new DomainException("Cross-tenant write blocked.");
        }
        return base.SaveChangesAsync(ct);
    }
}
