namespace Mini_Project___Order_Processing_System.Services;

// the shipping formula of Task 8.1
public static class ShippingPrice
{
    public static decimal Calculate(Order order, List<Product> products)
    {
        decimal subTotal = 0;
        int totalQuantity = 0;
        decimal totalWeight = 0;

        foreach (var item in order.Items)
        {
            var product = products.First(p => p.Id == item.ProductId);
            subTotal += product.Price * item.Quantity;
            totalQuantity += item.Quantity;
            totalWeight += product.WeightKg * item.Quantity;
        }

        // free shipping
        if (subTotal >= 1000) return 0;

        decimal shipping = 5m + 1.5m * totalQuantity + 2m * totalWeight;
        return Math.Round(shipping, 2, MidpointRounding.AwayFromZero);
    }
}
