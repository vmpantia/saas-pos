using SaasPos.Domain.Enums;

namespace SaasPos.Application.Dtos;

public record LoginRequest(string TenantCode, string Username, string Password);
public record LoginResponse(string Token, string FullName, string TenantName, UserRole Role);
public record ProvisionTenantRequest(string Name, string Code, string AdminUsername, string AdminPassword, string AdminFullName);
public record CreateUserRequest(string Username, string Password, string FullName, UserRole Role);

public record CategoryDto(Guid Id, string Name);
public record CategoryRequest(string Name);

public record ProductDto(Guid Id, string Sku, string? Barcode, string Name, Guid? CategoryId,
    decimal Price, decimal Cost, int QuantityOnHand, int ReorderLevel, bool IsActive, bool IsLowStock);
public record ProductRequest(string Sku, string Name, decimal Price, decimal Cost,
    int ReorderLevel, string? Barcode, Guid? CategoryId, int InitialStock = 0);

public record ReceiveStockRequest(int Quantity, string? Reference, string? Note);
public record AdjustStockRequest(int NewQuantity, string? Note);
public record StockMovementDto(Guid Id, StockMovementType Type, int QuantityChange, int BalanceAfter,
    string? Reference, string? Note, DateTime CreatedAtUtc);

public record CustomerDto(Guid Id, string Name, string? Phone, string? Email);
public record CustomerRequest(string Name, string? Phone, string? Email);

public record SaleLineRequest(Guid ProductId, int Quantity);
public record CheckoutRequest(List<SaleLineRequest> Items, PaymentMethod PaymentMethod,
    decimal AmountTendered, decimal Discount = 0, Guid? CustomerId = null);
public record SaleItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);
public record SaleDto(Guid Id, string ReceiptNumber, DateTime CreatedAtUtc, SaleStatus Status,
    PaymentMethod PaymentMethod, decimal Subtotal, decimal Discount, decimal Total,
    decimal AmountTendered, decimal Change, List<SaleItemDto> Items);

public record TopProductDto(string ProductName, int QuantitySold, decimal Revenue);
public record SalesSummaryDto(DateTime FromUtc, DateTime ToUtc, int SaleCount, decimal Revenue,
    decimal Profit, List<TopProductDto> TopProducts);
