using SaasPos.Domain.Common;
using SaasPos.Domain.Enums;

namespace SaasPos.Domain.Entities;

public class Category : TenantEntity
{
    public string Name { get; private set; } = default!;

    private Category() { }
    public Category(Guid tenantId, string name) : base(tenantId) => Rename(name);

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Category name is required.");
        Name = name.Trim();
    }
}

public class Product : TenantEntity
{
    public string Sku { get; private set; } = default!;
    public string? Barcode { get; private set; }
    public string Name { get; private set; } = default!;
    public Guid? CategoryId { get; private set; }
    public decimal Price { get; private set; }
    public decimal Cost { get; private set; }
    public int QuantityOnHand { get; private set; }
    public int ReorderLevel { get; private set; }
    public bool IsActive { get; private set; } = true;

    private readonly List<StockMovement> _movements = [];
    public IReadOnlyCollection<StockMovement> Movements => _movements;

    private Product() { }

    public Product(Guid tenantId, string sku, string name, decimal price, decimal cost,
        int reorderLevel = 0, string? barcode = null, Guid? categoryId = null) : base(tenantId)
    {
        Update(sku, name, price, cost, reorderLevel, barcode, categoryId);
    }

    public void Update(string sku, string name, decimal price, decimal cost,
        int reorderLevel, string? barcode, Guid? categoryId)
    {
        if (string.IsNullOrWhiteSpace(sku)) throw new DomainException("SKU is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Product name is required.");
        if (price < 0 || cost < 0) throw new DomainException("Price and cost cannot be negative.");
        if (reorderLevel < 0) throw new DomainException("Reorder level cannot be negative.");
        Sku = sku.Trim();
        Name = name.Trim();
        Price = price;
        Cost = cost;
        ReorderLevel = reorderLevel;
        Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
        CategoryId = categoryId;
    }

    public void SetActive(bool active) => IsActive = active;

    public bool IsLowStock => QuantityOnHand <= ReorderLevel;

    public StockMovement Receive(int quantity, string? reference = null, string? note = null)
    {
        if (quantity <= 0) throw new DomainException("Quantity must be positive.");
        return Record(StockMovementType.Purchase, quantity, reference, note);
    }

    public StockMovement Sell(int quantity, string? reference = null)
    {
        if (quantity <= 0) throw new DomainException("Quantity must be positive.");
        if (!IsActive) throw new DomainException($"Product '{Name}' is inactive.");
        if (QuantityOnHand < quantity)
            throw new DomainException($"Insufficient stock for '{Name}'. Available: {QuantityOnHand}.");
        return Record(StockMovementType.Sale, -quantity, reference, null);
    }

    public StockMovement Restock(int quantity, string? reference = null) =>
        Record(StockMovementType.Return, quantity, reference, "Returned from voided sale");

    /// <summary>Sets stock to an absolute counted value (stock take).</summary>
    public StockMovement Adjust(int newQuantity, string? note = null)
    {
        if (newQuantity < 0) throw new DomainException("Stock cannot be negative.");
        return Record(StockMovementType.Adjustment, newQuantity - QuantityOnHand, null, note);
    }

    private StockMovement Record(StockMovementType type, int delta, string? reference, string? note)
    {
        QuantityOnHand += delta;
        var movement = new StockMovement(TenantId, Id, type, delta, QuantityOnHand, reference, note);
        _movements.Add(movement);
        return movement;
    }
}

public class StockMovement : TenantEntity
{
    public Guid ProductId { get; private set; }
    public StockMovementType Type { get; private set; }
    public int QuantityChange { get; private set; }
    public int BalanceAfter { get; private set; }
    public string? Reference { get; private set; }
    public string? Note { get; private set; }

    private StockMovement() { }

    internal StockMovement(Guid tenantId, Guid productId, StockMovementType type, int change,
        int balanceAfter, string? reference, string? note) : base(tenantId)
    {
        ProductId = productId;
        Type = type;
        QuantityChange = change;
        BalanceAfter = balanceAfter;
        Reference = reference;
        Note = note;
    }
}

public class Customer : TenantEntity
{
    public string Name { get; private set; } = default!;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }

    private Customer() { }

    public Customer(Guid tenantId, string name, string? phone, string? email) : base(tenantId)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Customer name is required.");
        Name = name.Trim();
        Phone = phone;
        Email = email;
    }
}
