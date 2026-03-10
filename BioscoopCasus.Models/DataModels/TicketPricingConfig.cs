namespace BioscoopCasus.Models.DataModels;

public class TicketPricingConfig
{
    public BasePriceConfig BasePrice { get; set; } = new();
    public DiscountConfig Discounts { get; set; } = new();
    public SurchargeConfig Surcharges { get; set; } = new();
    public RuleConfig Rules { get; set; } = new();
}

public class BasePriceConfig
{
    public decimal Normal { get; set; } = 8.50m;
    public decimal LongMovie { get; set; } = 9.00m;
    public int LongMovieThresholdMinutes { get; set; } = 120;
}

public class DiscountConfig
{
    public decimal Child { get; set; } = 1.50m;
    public decimal Student { get; set; } = 1.50m;
    public decimal Senior { get; set; } = 1.50m;
}

public class SurchargeConfig
{
    public decimal ThreeD { get; set; } = 2.50m;
}

public class RuleConfig
{
    public int ChildMaxAge { get; set; } = 11;
    public int ChildBeforeHour { get; set; } = 18;

    public List<int> StudentValidDays { get; set; } = new() { 1, 2, 3, 4 };
    public List<int> SeniorValidDays { get; set; } = new() { 1, 2, 3, 4 };
    public List<int> VoucherValidDays { get; set; } = new() { 1, 2, 3, 4 };
}