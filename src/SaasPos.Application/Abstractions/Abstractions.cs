using System.Linq.Expressions;
using SaasPos.Domain.Common;
using SaasPos.Domain.Entities;

namespace SaasPos.Application.Abstractions;

public interface ITenantContext
{
    Guid TenantId { get; }
    Guid UserId { get; }
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IRepository<T> where T : TenantEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Remove(T entity);
}

public interface IProductRepository : IRepository<Product>
{
    Task<List<Product>> SearchAsync(string? term, bool activeOnly, CancellationToken ct = default);
    Task<List<Product>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<List<StockMovement>> GetMovementsAsync(Guid productId, CancellationToken ct = default);
}

public interface ISaleRepository : IRepository<Sale>
{
    Task<Sale?> GetWithItemsAsync(Guid id, CancellationToken ct = default);
    Task<List<Sale>> ListWithItemsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

public interface ITenantRepository
{
    Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task AddAsync(Tenant tenant, CancellationToken ct = default);
}

public interface IUserRepository
{
    /// <summary>Bypasses tenant filtering; used only for sign-in and provisioning.</summary>
    Task<User?> FindForLoginAsync(Guid tenantId, string username, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    string CreateToken(User user, Tenant tenant);
}
