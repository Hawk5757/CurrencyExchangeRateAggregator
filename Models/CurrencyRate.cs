using System.ComponentModel.DataAnnotations;

namespace CurrencyExchangeRateAggregator.Models;

public class CurrencyRate
{
    [Key]
    public DateTime Date { get; set; }
    public decimal Rate { get; set; }
}