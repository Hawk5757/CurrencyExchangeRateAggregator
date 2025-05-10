using Microsoft.EntityFrameworkCore;
using CurrencyExchangeRateAggregator.Models;

namespace CurrencyExchangeRateAggregator.Data;

public class CurrencyContext : DbContext
{
    public DbSet<CurrencyRate> CurrencyRates { get; set; }
    
    public CurrencyContext(DbContextOptions<CurrencyContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CurrencyRate>()
            .HasKey(c => c.Date);

        base.OnModelCreating(modelBuilder);
    }
}