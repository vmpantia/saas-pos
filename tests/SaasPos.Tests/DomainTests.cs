using SaasPos.Domain.Common;
using SaasPos.Domain.Entities;
using SaasPos.Domain.Enums;

namespace SaasPos.Tests;

public class DomainTests
{
    private static readonly Guid T = Guid.NewGuid();

    private static Product NewProduct(int stock = 10)
    {
        var p = new Product(T, "SKU1", "Coffee", 50m, 20m, reorderLevel: 3);
        if (stock > 0) p.Receive(stock);
        return p;
    }

    [Fact]
    public void Receive_increases_stock_and_records_movement()
    {
        var p = NewProduct(10);
        Assert.Equal(10, p.QuantityOnHand);
        Assert.Single(p.Movements);
    }

    [Fact]
    public void Sell_beyond_stock_throws()
    {
        var p = NewProduct(2);
        Assert.Throws<DomainException>(() => p.Sell(3));
    }

    [Fact]
    public void Sale_deducts_stock_and_computes_totals()
    {
        var p = NewProduct(10);
        var sale = new Sale(T, "R1", Guid.NewGuid(), null, PaymentMethod.Cash, discount: 10m);
        sale.AddItem(p, 2);
        sale.AddItem(p, 1);
        sale.Pay(200m);
        Assert.Equal(7, p.QuantityOnHand);
        Assert.Equal(150m, sale.Subtotal);
        Assert.Equal(140m, sale.Total);
        Assert.Equal(60m, sale.Change);
        Assert.Single(sale.Items);
    }

    [Fact]
    public void Underpayment_throws()
    {
        var p = NewProduct();
        var sale = new Sale(T, "R1", Guid.NewGuid(), null, PaymentMethod.Cash, 0);
        sale.AddItem(p, 1);
        Assert.Throws<DomainException>(() => sale.Pay(10m));
    }

    [Fact]
    public void Void_restores_stock_once()
    {
        var p = NewProduct(10);
        var sale = new Sale(T, "R1", Guid.NewGuid(), null, PaymentMethod.Cash, 0);
        sale.AddItem(p, 4);
        sale.Pay(500m);
        var map = new Dictionary<Guid, Product> { [p.Id] = p };
        sale.Void(map);
        Assert.Equal(10, p.QuantityOnHand);
        Assert.Throws<DomainException>(() => sale.Void(map));
    }

    [Fact]
    public void Sale_rejects_other_tenant_product()
    {
        var p = new Product(Guid.NewGuid(), "X", "X", 1, 1);
        var sale = new Sale(T, "R1", Guid.NewGuid(), null, PaymentMethod.Cash, 0);
        Assert.Throws<DomainException>(() => sale.AddItem(p, 1));
    }
}
