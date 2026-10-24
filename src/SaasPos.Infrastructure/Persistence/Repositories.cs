using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SaasPos.Application.Abstractions;
using SaasPos.Domain.Common;
using SaasPos.Domain.Entities;

namespace SaasPos.Infrastructure.Persistence;

public class Repository<T>(AppDbContext db) : IRepository<T> where T : TenantEntity
{
    protected readonly AppDbContext Db = db;

    public Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Db.Set<T>().FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default) =>
        (predicate is null ? Db.Set<T>() : Db.Set<T>().Where(predicate)).ToListAsync(ct);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Db.Set<T>().AnyAsync(predicate, ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) => await Db.Set<T>().AddAsync(entity, ct);
    public void Remove(T entity) => Db.Set<T>().Remove(entity);
}

public class ProductRepository(AppDbContext db) : Repository<Product>(db), IProductRepository
{
    public Task<List<Product>> SearchAsync(string? term, bool activeOnly, CancellationToken ct = default)
    {
        var q = Db.Products.AsQueryable();
        if (activeOnly) q = q.Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim();
            q = q.Where(p => p.Name.Contains(t) || p.Sku.Contains(t) || p.Barcode == t);
        }
        return q.OrderBy(p => p.Name).ToListAsync(ct);
    }

    public Task<List<Product>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        Db.Products.Where(p => ids.Contains(p.Id)).ToListAsync(ct);

    public async Task<List<StockMovement>> GetMovementsAsync(Guid productId, CancellationToken ct = default) =>
        (await Db.StockMovements.Where(m => m.ProductId == productId).ToListAsync(ct))
        .OrderByDescending(m => m.CreatedAtUtc).ToList();
}

public class SaleRepository(AppDbContext db) : Repository<Sale>(db), ISaleRepository
{
    public Task<Sale?> GetWithItemsAsync(Guid id, CancellationToken ct = default) =>
        Db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<List<Sale>> ListWithItemsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        (await Db.Sales.Include(s => s.Items)
            .Where(s => s.CreatedAtUtc >= fromUtc && s.CreatedAtUtc < toUtc).ToListAsync(ct))
        .OrderByDescending(s => s.CreatedAtUtc).ToList();
}

public class TenantRepository(AppDbContext db) : ITenantRepository
{
    public Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        db.Tenants.FirstOrDefaultAsync(t => t.Code == code, ct);

    public async Task AddAsync(Tenant tenant, CancellationToken ct = default) => await db.Tenants.AddAsync(tenant, ct);
}

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> FindForLoginAsync(Guid tenantId, string username, CancellationToken ct = default) =>
        db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Username == username, ct);

    public async Task AddAsync(User user, CancellationToken ct = default) => await db.Users.AddAsync(user, ct);
}
