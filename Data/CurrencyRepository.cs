using CurrencyExchangeRateAggregator.Models;
using Microsoft.EntityFrameworkCore;

namespace CurrencyExchangeRateAggregator.Data;

public class CurrencyRepository : ICurrencyRepository
{
    private readonly CurrencyContext _context;
    
    public CurrencyRepository(CurrencyContext context)
    {
        _context = context;
    }
    
    public async Task AddOrUpdateRateAsync(CurrencyRate rate)
    {
        var existingRate = await _context.CurrencyRates.FindAsync(rate.Date);
        if (existingRate != null)
        {
            existingRate.Rate = rate.Rate;
        }
        else
        {
            await _context.CurrencyRates.AddAsync(rate);
        }
        await _context.SaveChangesAsync();
    }
    
    public async Task AddOrUpdateRatesAsync(IEnumerable<CurrencyRate> rates)
    {
        var existingRates = await _context.CurrencyRates.Where(r => rates.Select(x => x.Date).Contains(r.Date)).ToListAsync();
        var newRates = new List<CurrencyRate>();

        foreach (var rate in rates)
        {
            var existing = existingRates.FirstOrDefault(r => r.Date == rate.Date);
            if (existing != null)
            {
                existing.Rate = rate.Rate;
            }
            else
            {
                newRates.Add(rate);
            }
        }

        if (newRates.Any())
        {
            await _context.CurrencyRates.AddRangeAsync(newRates);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<CurrencyRate> GetRateByDateAsync(DateTime date)
    {
        return await _context.CurrencyRates.FindAsync(date);
    }

    public async Task<IEnumerable<CurrencyRate>> GetRatesByPeriodAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.CurrencyRates
            .Where(r => r.Date >= startDate && r.Date <= endDate)
            .ToListAsync();
    }
}