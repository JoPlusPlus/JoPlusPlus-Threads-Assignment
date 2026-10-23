namespace Mini_Project___Order_Processing_System.Services;

public class NileCourier : ShippingCarrierBase
{
    public NileCourier(IEnumerable<int>? failForOrderIds = null) : base(failForOrderIds)
    {
    }

    public override string Name => "NileCourier";
    protected override int MinMs => 800;
    protected override int MaxMs => 1200;
    protected override decimal Factor => 1.10m;
}
