using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaasPos.Application.Abstractions;
using SaasPos.Application.Dtos;
using SaasPos.Application.Services;

namespace SaasPos.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth, ITenantContext ctx, IConfiguration config) : ControllerBase
{
    [HttpPost("login")]
    public async Task<LoginResponse> Login(LoginRequest r, CancellationToken ct) => await auth.LoginAsync(r, ct);

    /// <summary>Onboards a new client. Guarded by a platform-level key, not a tenant login.</summary>
    [HttpPost("/api/platform/tenants")]
    public async Task<IActionResult> Provision([FromHeader(Name = "X-Provisioning-Key")] string? key,
        ProvisionTenantRequest r, CancellationToken ct)
    {
        var expected = config["Provisioning:Key"];
        if (string.IsNullOrEmpty(expected) || key != expected) return Unauthorized();
        return Ok(new { tenantId = await auth.ProvisionTenantAsync(r, ct) });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest r, CancellationToken ct)
    {
        await auth.CreateUserAsync(ctx, r, ct);
        return NoContent();
    }
}

[ApiController, Authorize, Route("api")]
public class CatalogController(CatalogService catalog) : ControllerBase
{
    [HttpGet("categories")]
    public Task<List<CategoryDto>> Categories(CancellationToken ct) => catalog.GetCategoriesAsync(ct);

    [Authorize(Roles = "Admin,Manager"), HttpPost("categories")]
    public Task<CategoryDto> CreateCategory(CategoryRequest r, CancellationToken ct) => catalog.CreateCategoryAsync(r, ct);

    [HttpGet("products")]
    public Task<List<ProductDto>> Products([FromQuery] string? q, [FromQuery] bool activeOnly = false, CancellationToken ct = default) =>
        catalog.SearchProductsAsync(q, activeOnly, ct);

    [HttpGet("products/{id:guid}")]
    public Task<ProductDto> Product(Guid id, CancellationToken ct) => catalog.GetProductAsync(id, ct);

    [Authorize(Roles = "Admin,Manager"), HttpPost("products")]
    public Task<ProductDto> Create(ProductRequest r, CancellationToken ct) => catalog.CreateProductAsync(r, ct);

    [Authorize(Roles = "Admin,Manager"), HttpPut("products/{id:guid}")]
    public Task<ProductDto> Update(Guid id, ProductRequest r, CancellationToken ct) => catalog.UpdateProductAsync(id, r, ct);

    [Authorize(Roles = "Admin,Manager"), HttpPut("products/{id:guid}/active/{active:bool}")]
    public async Task<IActionResult> SetActive(Guid id, bool active, CancellationToken ct)
    {
        await catalog.SetProductActiveAsync(id, active, ct);
        return NoContent();
    }

    [HttpGet("customers")]
    public Task<List<CustomerDto>> Customers(CancellationToken ct) => catalog.GetCustomersAsync(ct);

    [HttpPost("customers")]
    public Task<CustomerDto> CreateCustomer(CustomerRequest r, CancellationToken ct) => catalog.CreateCustomerAsync(r, ct);
}

[ApiController, Authorize(Roles = "Admin,Manager"), Route("api/inventory")]
public class InventoryController(InventoryService inventory) : ControllerBase
{
    [HttpGet("low-stock")]
    public Task<List<ProductDto>> LowStock(CancellationToken ct) => inventory.GetLowStockAsync(ct);

    [HttpGet("{productId:guid}/movements")]
    public Task<List<StockMovementDto>> Movements(Guid productId, CancellationToken ct) => inventory.GetMovementsAsync(productId, ct);

    [HttpPost("{productId:guid}/receive")]
    public Task<ProductDto> Receive(Guid productId, ReceiveStockRequest r, CancellationToken ct) => inventory.ReceiveAsync(productId, r, ct);

    [HttpPost("{productId:guid}/adjust")]
    public Task<ProductDto> Adjust(Guid productId, AdjustStockRequest r, CancellationToken ct) => inventory.AdjustAsync(productId, r, ct);
}

[ApiController, Authorize, Route("api/sales")]
public class SalesController(SaleService sales) : ControllerBase
{
    [HttpPost("checkout")]
    public Task<SaleDto> Checkout(CheckoutRequest r, CancellationToken ct) => sales.CheckoutAsync(r, ct);

    [HttpGet("{id:guid}")]
    public Task<SaleDto> Get(Guid id, CancellationToken ct) => sales.GetAsync(id, ct);

    [HttpGet]
    public Task<List<SaleDto>> List([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var end = to ?? DateTime.UtcNow.AddDays(1);
        return sales.ListAsync(from ?? end.AddDays(-1), end, ct);
    }

    [Authorize(Roles = "Admin,Manager"), HttpPost("{id:guid}/void")]
    public Task<SaleDto> Void(Guid id, CancellationToken ct) => sales.VoidAsync(id, ct);
}

[ApiController, Authorize(Roles = "Admin,Manager"), Route("api/reports")]
public class ReportsController(ReportService reports) : ControllerBase
{
    [HttpGet("summary")]
    public Task<SalesSummaryDto> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var end = to ?? DateTime.UtcNow.AddDays(1);
        return reports.SummaryAsync(from ?? end.Date.AddDays(-30), end, ct);
    }
}
