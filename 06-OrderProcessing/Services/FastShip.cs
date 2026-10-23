namespace Mini_Project___Order_Processing_System.Services;

public class FastShip : ShippingCarrierBase
{
    public FastShip(IEnumerable<int>? failForOrderIds = null) : base(failForOrderIds)
    {
    }

    public override string Name => "FastShip";
    protected override int MinMs => 500;
    protected override int MaxMs => 1500;
    protected override decimal Factor => 1.00m;
}
