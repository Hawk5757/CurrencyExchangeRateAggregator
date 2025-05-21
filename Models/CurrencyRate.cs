
using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CurrencyExchangeRateAggregator.Models;

[Index(nameof(Date), IsUnique = true)]
public class CurrencyRate
{
    [Key]
    public DateTime Date { get; set; }
    public decimal Rate { get; set; }
}