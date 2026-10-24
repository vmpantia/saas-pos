using SaasPos.Application.Abstractions;
using SaasPos.Application.Dtos;
using SaasPos.Domain.Common;
using SaasPos.Domain.Entities;
using SaasPos.Domain.Enums;

namespace SaasPos.Application.Services;

public class NotFoundException(string message) : Exception(message);
public class UnauthorizedAppException(string message) : Exception(message);

public class AuthService(ITenantRepository tenants, IUserRepository users, IPasswordHasher hasher,
    ITokenService tokens, IUnitOfWork uow)
{
    public async Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var tenant = await tenants.GetByCodeAsync(req.TenantCode.Trim().ToLowerInvariant(), ct);
        var user = tenant is { IsActive: true }
            ? await users.FindForLoginAsync(tenant.Id, req.Username.Trim().ToLowerInvariant(), ct)
            : null;
        if (tenant is null || user is null || !user.IsActive || !hasher.Verify(req.Password, user.PasswordHash))
            throw new UnauthorizedAppException("Invalid credentials.");
        return new LoginResponse(tokens.CreateToken(user, tenant), user.FullName, tenant.Name, user.Role);
    }

    public async Task<Guid> ProvisionTenantAsync(ProvisionTenantRequest req, CancellationToken ct = default)
    {
        var tenant = new Tenant(req.Name, req.Code);
        if (await tenants.GetByCodeAsync(tenant.Code, ct) is not null)
            throw new DomainException("Tenant code already exists.");
        ValidatePassword(req.AdminPassword);
        await tenants.AddAsync(tenant, ct);
        await users.AddAsync(new User(tenant.Id, req.AdminUsername, hasher.Hash(req.AdminPassword),
            req.AdminFullName, UserRole.Admin), ct);
        await uow.SaveChangesAsync(ct);
        return tenant.Id;
    }

    public async Task CreateUserAsync(ITenantContext ctx, CreateUserRequest req, CancellationToken ct = default)
    {
        ValidatePassword(req.Password);
        var username = req.Username.Trim().ToLowerInvariant();
        if (await users.FindForLoginAsync(ctx.TenantId, username, ct) is not null)
            throw new DomainException("Username already exists.");
        await users.AddAsync(new User(ctx.TenantId, username, hasher.Hash(req.Password), req.FullName, req.Role), ct);
        await uow.SaveChangesAsync(ct);
    }

    private static void ValidatePassword(string pw)
    {
        if (string.IsNullOrEmpty(pw) || pw.Length < 8) throw new DomainException("Password must be at least 8 characters.");
    }
}

public class CatalogService(ITenantContext ctx, IRepository<Category> categories, IProductRepository products,
    IRepository<Customer> customers, IUnitOfWork uow)
{
    private static ProductDto Map(Product p) => new(p.Id, p.Sku, p.Barcode, p.Name, p.CategoryId, p.Price,
        p.Cost, p.QuantityOnHand, p.ReorderLevel, p.IsActive, p.IsLowStock);

    public async Task<List<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default) =>
        (await categories.ListAsync(null, ct)).OrderBy(c => c.Name).Select(c => new CategoryDto(c.Id, c.Name)).ToList();

    public async Task<CategoryDto> CreateCategoryAsync(CategoryRequest r, CancellationToken ct = default)
    {
        var c = new Category(ctx.TenantId, r.Name);
        await categories.AddAsync(c, ct);
        await uow.SaveChangesAsync(ct);
        return new CategoryDto(c.Id, c.Name);
    }

    public async Task<List<ProductDto>> SearchProductsAsync(string? term, bool activeOnly, CancellationToken ct = default) =>
        (await products.SearchAsync(term, activeOnly, ct)).Select(Map).ToList();

    public async Task<ProductDto> GetProductAsync(Guid id, CancellationToken ct = default) =>
        Map(await products.GetByIdAsync(id, ct) ?? throw new NotFoundException("Product not found."));

    public async Task<ProductDto> CreateProductAsync(ProductRequest r, CancellationToken ct = default)
    {
        if (await products.AnyAsync(p => p.Sku == r.Sku.Trim(), ct)) throw new DomainException("SKU already exists.");
        var p = new Product(ctx.TenantId, r.Sku, r.Name, r.Price, r.Cost, r.ReorderLevel, r.Barcode, r.CategoryId);
        if (r.InitialStock > 0) p.Receive(r.InitialStock, null, "Initial stock");
        await products.AddAsync(p, ct);
        await uow.SaveChangesAsync(ct);
        return Map(p);
    }

    public async Task<ProductDto> UpdateProductAsync(Guid id, ProductRequest r, CancellationToken ct = default)
    {
        var p = await products.GetByIdAsync(id, ct) ?? throw new NotFoundException("Product not found.");
        var sku = r.Sku.Trim();
        if (await products.AnyAsync(x => x.Sku == sku && x.Id != id, ct)) throw new DomainException("SKU already exists.");
        p.Update(r.Sku, r.Name, r.Price, r.Cost, r.ReorderLevel, r.Barcode, r.CategoryId);
        await uow.SaveChangesAsync(ct);
        return Map(p);
    }

    public async Task SetProductActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var p = await products.GetByIdAsync(id, ct) ?? throw new NotFoundException("Product not found.");
        p.SetActive(active);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<List<CustomerDto>> GetCustomersAsync(CancellationToken ct = default) =>
        (await customers.ListAsync(null, ct)).OrderBy(c => c.Name).Select(c => new CustomerDto(c.Id, c.Name, c.Phone, c.Email)).ToList();

    public async Task<CustomerDto> CreateCustomerAsync(CustomerRequest r, CancellationToken ct = default)
    {
        var c = new Customer(ctx.TenantId, r.Name, r.Phone, r.Email);
        await customers.AddAsync(c, ct);
        await uow.SaveChangesAsync(ct);
        return new CustomerDto(c.Id, c.Name, c.Phone, c.Email);
    }
}

public class InventoryService(IProductRepository products, IUnitOfWork uow)
{
    public async Task<ProductDto> ReceiveAsync(Guid productId, ReceiveStockRequest r, CancellationToken ct = default)
    {
        var p = await products.GetByIdAsync(productId, ct) ?? throw new NotFoundException("Product not found.");
        p.Receive(r.Quantity, r.Reference, r.Note);
        await uow.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<ProductDto> AdjustAsync(Guid productId, AdjustStockRequest r, CancellationToken ct = default)
    {
        var p = await products.GetByIdAsync(productId, ct) ?? throw new NotFoundException("Product not found.");
        p.Adjust(r.NewQuantity, r.Note);
        await uow.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<List<StockMovementDto>> GetMovementsAsync(Guid productId, CancellationToken ct = default) =>
        (await products.GetMovementsAsync(productId, ct))
        .Select(m => new StockMovementDto(m.Id, m.Type, m.QuantityChange, m.BalanceAfter, m.Reference, m.Note, m.CreatedAtUtc))
        .ToList();

    public async Task<List<ProductDto>> GetLowStockAsync(CancellationToken ct = default) =>
        (await products.SearchAsync(null, true, ct)).Where(p => p.IsLowStock).Select(ToDto).ToList();

    private static ProductDto ToDto(Product p) => new(p.Id, p.Sku, p.Barcode, p.Name, p.CategoryId, p.Price,
        p.Cost, p.QuantityOnHand, p.ReorderLevel, p.IsActive, p.IsLowStock);
}

public class SaleService(ITenantContext ctx, ISaleRepository sales, IProductRepository products, IUnitOfWork uow)
{
    internal static SaleDto Map(Sale s) => new(s.Id, s.ReceiptNumber, s.CreatedAtUtc, s.Status, s.PaymentMethod,
        s.Subtotal, s.Discount, s.Total, s.AmountTendered, s.Change,
        s.Items.Select(i => new SaleItemDto(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.LineTotal)).ToList());

    public async Task<SaleDto> CheckoutAsync(CheckoutRequest r, CancellationToken ct = default)
    {
        if (r.Items is not { Count: > 0 }) throw new DomainException("Cart is empty.");
        var ids = r.Items.Select(i => i.ProductId).Distinct().ToList();
        var found = (await products.GetByIdsAsync(ids, ct)).ToDictionary(p => p.Id);
        if (found.Count != ids.Count) throw new NotFoundException("One or more products were not found.");

        var receipt = $"{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var sale = new Sale(ctx.TenantId, receipt, ctx.UserId, r.CustomerId, r.PaymentMethod, r.Discount);
        foreach (var line in r.Items) sale.AddItem(found[line.ProductId], line.Quantity);
        sale.Pay(r.AmountTendered);

        await sales.AddAsync(sale, ct);
        await uow.SaveChangesAsync(ct);
        return Map(sale);
    }

    public async Task<SaleDto> GetAsync(Guid id, CancellationToken ct = default) =>
        Map(await sales.GetWithItemsAsync(id, ct) ?? throw new NotFoundException("Sale not found."));

    public async Task<List<SaleDto>> ListAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        (await sales.ListWithItemsAsync(fromUtc, toUtc, ct)).Select(Map).ToList();

    public async Task<SaleDto> VoidAsync(Guid id, CancellationToken ct = default)
    {
        var sale = await sales.GetWithItemsAsync(id, ct) ?? throw new NotFoundException("Sale not found.");
        var map = (await products.GetByIdsAsync(sale.Items.Select(i => i.ProductId).ToList(), ct)).ToDictionary(p => p.Id);
        sale.Void(map);
        await uow.SaveChangesAsync(ct);
        return Map(sale);
    }
}

public class ReportService(ISaleRepository sales)
{
    public async Task<SalesSummaryDto> SummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var list = (await sales.ListWithItemsAsync(fromUtc, toUtc, ct)).Where(s => s.Status == SaleStatus.Completed).ToList();
        var items = list.SelectMany(s => s.Items).ToList();
        var top = items.GroupBy(i => i.ProductName)
            .Select(g => new TopProductDto(g.Key, g.Sum(i => i.Quantity), g.Sum(i => i.LineTotal)))
            .OrderByDescending(t => t.Revenue).Take(10).ToList();
        var revenue = list.Sum(s => s.Total);
        var cost = items.Sum(i => i.UnitCost * i.Quantity);
        return new SalesSummaryDto(fromUtc, toUtc, list.Count, revenue, revenue - cost, top);
    }
}
