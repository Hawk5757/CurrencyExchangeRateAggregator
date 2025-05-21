using CurrencyExchangeRateAggregator.Models;

namespace CurrencyExchangeRateAggregator.Data;

public interface ICurrencyRepository
{
    Task AddOrUpdateRateAsync(CurrencyRate rate);
    Task<CurrencyRate> GetRateByDateAsync(DateTime date);
    Task<IEnumerable<CurrencyRate>> GetRatesByPeriodAsync(DateTime startDate, DateTime endDate);
    Task AddOrUpdateRatesAsync(IEnumerable<CurrencyRate> rates);
}