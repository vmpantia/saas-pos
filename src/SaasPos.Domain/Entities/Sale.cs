using SaasPos.Domain.Common;
using SaasPos.Domain.Enums;

namespace SaasPos.Domain.Entities;

public class Sale : TenantEntity
{
    public string ReceiptNumber { get; private set; } = default!;
    public Guid CashierId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public SaleStatus Status { get; private set; } = SaleStatus.Completed;
    public decimal Discount { get; private set; }
    public decimal Subtotal => _items.Sum(i => i.LineTotal);
    public decimal Total => Subtotal - Discount;
    public decimal AmountTendered { get; private set; }
    public decimal Change => Math.Max(0, AmountTendered - Total);
    public PaymentMethod PaymentMethod { get; private set; }

    private readonly List<SaleItem> _items = [];
    public IReadOnlyCollection<SaleItem> Items => _items;

    private Sale() { }

    public Sale(Guid tenantId, string receiptNumber, Guid cashierId, Guid? customerId,
        PaymentMethod method, decimal discount) : base(tenantId)
    {
        if (discount < 0) throw new DomainException("Discount cannot be negative.");
        ReceiptNumber = receiptNumber;
        CashierId = cashierId;
        CustomerId = customerId;
        PaymentMethod = method;
        Discount = discount;
    }

    /// <summary>Adds a line, deducting stock from the product. Price is snapshotted.</summary>
    public void AddItem(Product product, int quantity)
    {
        if (product.TenantId != TenantId) throw new DomainException("Product belongs to another tenant.");
        product.Sell(quantity, ReceiptNumber);
        var existing = _items.FirstOrDefault(i => i.ProductId == product.Id);
        if (existing is null)
            _items.Add(new SaleItem(TenantId, Id, product.Id, product.Name, product.Price, product.Cost, quantity));
        else
            existing.IncreaseQuantity(quantity);
    }

    public void Pay(decimal amountTendered)
    {
        if (_items.Count == 0) throw new DomainException("A sale needs at least one item.");
        if (Discount > Subtotal) throw new DomainException("Discount exceeds subtotal.");
        if (amountTendered < Total) throw new DomainException("Amount tendered is less than the total.");
        AmountTendered = amountTendered;
    }

    public void Void(IReadOnlyDictionary<Guid, Product> products)
    {
        if (Status == SaleStatus.Voided) throw new DomainException("Sale is already voided.");
        foreach (var item in _items)
            products[item.ProductId].Restock(item.Quantity, ReceiptNumber);
        Status = SaleStatus.Voided;
    }
}

public class SaleItem : TenantEntity
{
    public Guid SaleId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public decimal UnitPrice { get; private set; }
    public decimal UnitCost { get; private set; }
    public int Quantity { get; private set; }
    public decimal LineTotal => UnitPrice * Quantity;

    private SaleItem() { }

    internal SaleItem(Guid tenantId, Guid saleId, Guid productId, string name, decimal price, decimal cost, int qty)
        : base(tenantId)
    {
        SaleId = saleId;
        ProductId = productId;
        ProductName = name;
        UnitPrice = price;
        UnitCost = cost;
        Quantity = qty;
    }

    internal void IncreaseQuantity(int qty) => Quantity += qty;
}
