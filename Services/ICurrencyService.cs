using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CurrencyExchangeRateAggregator.Models;

namespace CurrencyExchangeRateAggregator.Services;

public interface ICurrencyService
{
    Task<CurrencyRate> GetCurrencyRateAsync(DateTime date);
    Task<IEnumerable<CurrencyRate>> GetCurrencyRatesAsync(DateTime startDate, DateTime endDate);
}