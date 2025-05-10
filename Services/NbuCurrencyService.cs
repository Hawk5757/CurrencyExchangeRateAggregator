using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CurrencyExchangeRateAggregator.Helpers;
using CurrencyExchangeRateAggregator.Models;
using Microsoft.Extensions.Configuration;
using CurrencyExchangeRateAggregator.Services.Models;

namespace CurrencyExchangeRateAggregator.Services;

public class NbuCurrencyService : ICurrencyService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private const int UsdCurrencyCode = 840;
    
    public NbuCurrencyService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new DateTimeConverter("dd.MM.yyyy") }
        };
    }
    
    public async Task<IEnumerable<CurrencyRate>> GetCurrencyRatesAsync(DateTime startDate, DateTime endDate)
    {
        var apiUrl = _configuration["NbuExchangeApiUrl"];
        if (string.IsNullOrEmpty(apiUrl))
        {
            return null;
        }

        var requestUrl = $"{apiUrl}?start={startDate:yyyyMMdd}&end={endDate:yyyyMMdd}&valcode=usd&json";

        try
        {
            var response = await _httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var ratesResponse = JsonSerializer.Deserialize<List<NbuRateResponse>>(content, _jsonSerializerOptions);

            return ratesResponse?
                .Where(r => r.CurrencyCode == UsdCurrencyCode)
                .Select(r => new CurrencyRate { Date = r.ExchangeDate.Date, Rate = r.Rate })
                .ToList();
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Помилка при запиті до API НБУ: {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Помилка при парсингу JSON від API НБУ: {ex.Message}");
            return null;
        }
    }

    public async Task<CurrencyRate> GetCurrencyRateAsync(DateTime date)
    {
        var rates = await GetCurrencyRatesAsync(date, date);
        return rates?.FirstOrDefault();
    }
}