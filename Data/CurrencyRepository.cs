using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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