using Mini_Project___Order_Processing_System.Data;
using Mini_Project___Order_Processing_System.MyExceptions;
using Mini_Project___Order_Processing_System.Repositories;

namespace Mini_Project___Order_Processing_System.Services;

public class SyncPricingService
{
    private const string Name = "SyncPricingService";
    private readonly JsonFileStore _store = new();
    private readonly SyncCustomerRepository _customers = new();
    private readonly SyncCouponRepository _coupons = new();

    // same rules as PricingService, but everything runs one after another
    public decimal CalculateTotal(Order order)
    {
        // 1 query: SubTotal
        order.SubTotal = Simulate.DbQuery<decimal>(Name, $"SubTotal of Order #{order.Id}", () =>
        {
            decimal subTotal = 0;
            var products = _store.ReadAll<Product>(DataFiles.Products);
            foreach (var item in order.Items)
            {
                var product = products.First(x => x.Id == item.ProductId);
                item.UnitPrice = product.Price;
                subTotal += item.UnitPrice * item.Quantity;
            }
            return Math.Round(subTotal, 2, MidpointRounding.AwayFromZero);
        });

        // discount API
        Simulate.ExternalApi(Name, $"discount for Order #{order.Id}");
        decimal percent = 0;
        var customer = _customers.GetById(order.CustomerId);
        if (customer != null)
        {
            if (customer.LoyaltyTier == LoyaltyTier.Silver) percent += 5;
            if (customer.LoyaltyTier == LoyaltyTier.Gold) percent += 10;
        }
        if (order.CouponCode != null)
        {
            var coupon = _coupons.GetByCode(order.CouponCode);
            if (coupon == null) throw new InvalidCouponException($"Coupon '{order.CouponCode}' does not exist");
            if (coupon.ExpiryDate < DateTime.Today) throw new InvalidCouponException($"Coupon '{order.CouponCode}' has expired");
            percent += coupon.DiscountPercent;
        }
        if (percent > 30) percent = 30;
        order.Discount = Math.Round(order.SubTotal * percent / 100, 2, MidpointRounding.AwayFromZero);

        // shipping API
        Simulate.ExternalApi(Name, $"shipping for Order #{order.Id}");
        var allProducts = _store.ReadAll<Product>(DataFiles.Products);
        decimal shipping = 0;
        if (order.SubTotal < 1000)
        {
            int totalQuantity = 0;
            decimal totalWeight = 0;
            foreach (var item in order.Items)
            {
                var product = allProducts.First(x => x.Id == item.ProductId);
                totalQuantity += item.Quantity;
                totalWeight += product.WeightKg * item.Quantity;
            }
            shipping = 5m + 1.5m * totalQuantity + 2m * totalWeight;
        }
        order.ShippingCost = Math.Round(shipping, 2, MidpointRounding.AwayFromZero);

        order.TotalAmount = Math.Round(order.SubTotal - order.Discount + order.ShippingCost, 2, MidpointRounding.AwayFromZero);
        Log.Success(Name, $"Order #{order.Id} Total = {order.TotalAmount}");
        return order.TotalAmount;
    }
}
