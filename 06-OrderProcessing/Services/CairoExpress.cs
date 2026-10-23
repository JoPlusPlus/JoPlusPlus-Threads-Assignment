namespace Mini_Project___Order_Processing_System.Services;

public class CairoExpress : ShippingCarrierBase
{
    public CairoExpress(IEnumerable<int>? failForOrderIds = null) : base(failForOrderIds)
    {
    }

    public override string Name => "CairoExpress";
    protected override int MinMs => 300;
    protected override int MaxMs => 2000;
    protected override decimal Factor => 0.95m;

    // built-in failure: even order ids
    protected override bool FailsByItself(Order order)
    {
        return order.Id % 2 == 0;
    }
}
