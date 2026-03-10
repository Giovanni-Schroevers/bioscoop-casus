using System.Text.Json;
using BioscoopCasus.Models.DataModels;

namespace BioscoopCasus.Web.Services;

public class TicketPricingService
{
    private TicketPricingConfig _pricingConfig = new();

    public async Task InitializeAsync(HttpClient httpClient)
    {
        var json = await httpClient.GetStringAsync("https://localhost:7181/api/ticketPricing");

        var options = new JsonSerializerOptions();
        options.PropertyNameCaseInsensitive = true;

        var config = JsonSerializer.Deserialize<TicketPricingConfig>(json, options);

        _pricingConfig = config ?? throw new InvalidOperationException(
            "Ticket pricing configuration could not be loaded.");
    }

    public decimal GetBasePrice(int durationMinutes)
    {
        return durationMinutes > _pricingConfig.BasePrice.LongMovieThresholdMinutes ? _pricingConfig.BasePrice.LongMovie : _pricingConfig.BasePrice.Normal;
    }

    public bool IsStudentDiscountValid(DateTime showtime)
    {
        return _pricingConfig.Rules.StudentValidDays
            .Contains((int)showtime.DayOfWeek);
    }

    public bool IsSeniorDiscountValid(DateTime showtime)
    {
        return _pricingConfig.Rules.SeniorValidDays
            .Contains((int)showtime.DayOfWeek);
    }

    public bool IsChildDiscountValid(DateTime showtime)
    {
        return showtime.Hour < _pricingConfig.Rules.ChildBeforeHour;
    }

    public decimal GetChildDiscount() => _pricingConfig.Discounts.Child;
    public decimal GetStudentDiscount() => _pricingConfig.Discounts.Student;
    public decimal GetSeniorDiscount() => _pricingConfig.Discounts.Senior;

    public decimal GetThreeDSurcharge() => _pricingConfig.Surcharges.ThreeD;

    public bool IsVoucherValid(DateTime showtime)
    {
        var day = showtime.DayOfWeek;

        var validWeekday =
            day is >= DayOfWeek.Monday and <= DayOfWeek.Thursday;

        return validWeekday;
    }
}