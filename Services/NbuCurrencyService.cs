using System.Text.Json;
using CurrencyExchangeRateAggregator.Data;
using CurrencyExchangeRateAggregator.Helpers;
using CurrencyExchangeRateAggregator.Models;
using CurrencyExchangeRateAggregator.Services.Models;
using Microsoft.Extensions.Caching.Memory;

namespace CurrencyExchangeRateAggregator.Services;

public class NbuCurrencyService : ICurrencyService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private const int UsdCurrencyCode = 840;
    private readonly ILogger<NbuCurrencyService> _logger;
    private readonly ICurrencyRepository _currencyRepository;
    private readonly IMemoryCache _cache;

    public NbuCurrencyService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<NbuCurrencyService> logger,
        ICurrencyRepository currencyRepository,
        IMemoryCache cache)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _currencyRepository = currencyRepository;
        _cache = cache;

        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new DateTimeConverter("dd.MM.yyyy") }
        };
    }

    public async Task<IEnumerable<CurrencyRate>> GetCurrencyRatesAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation($"NbuCurrencyService: Запит курсів за діапазон з {startDate:yyyy-MM-dd} по {endDate:yyyy-MM-dd}.");

        var ratesFromService = new List<CurrencyRate>();
        var datesToFetchFromApi = new List<DateTime>();

        for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
        {
            string cacheKey = $"CurrencyRate_{date:yyyyMMdd}";
            if (_cache.TryGetValue(cacheKey, out CurrencyRate cachedRate))
            {
                _logger.LogDebug($"Курс на {date:yyyy-MM-dd} отримано з кешу для діапазону.");
                ratesFromService.Add(cachedRate);
            }
            else
            {
                datesToFetchFromApi.Add(date);
            }
        }

        if (datesToFetchFromApi.Any())
        {
            _logger.LogInformation($"Деякі дати в діапазоні відсутні в кеші. Буде виконано запит до бази/API для {datesToFetchFromApi.Count} дат.");

            var ratesFromDb = await _currencyRepository.GetRatesByPeriodAsync(datesToFetchFromApi.Min(), datesToFetchFromApi.Max());
            var datesStillToFetchFromApi = new List<DateTime>();

            foreach (var dateToFetch in datesToFetchFromApi)
            {
                var rateFromDb = ratesFromDb?.FirstOrDefault(r => r.Date == dateToFetch);
                if (rateFromDb != null)
                {
                    ratesFromService.Add(rateFromDb);
                    string cacheKey = $"CurrencyRate_{dateToFetch:yyyyMMdd}";
                    _cache.Set(cacheKey, rateFromDb, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) // Кешуємо на 24 години
                    });
                    _logger.LogDebug($"Курс на {dateToFetch:yyyy-MM-dd} отримано з БД і додано в кеш.");
                }
                else
                {
                    datesStillToFetchFromApi.Add(dateToFetch);
                }
            }
            
            if (datesStillToFetchFromApi.Any())
            {
                _logger.LogInformation($"Є {datesStillToFetchFromApi.Count} дат, які відсутні в кеші та БД. Запит до зовнішнього API.");
                
                var minApiDate = datesStillToFetchFromApi.Min();
                var maxApiDate = datesStillToFetchFromApi.Max();

                var apiUrl = _configuration["NbuExchangeApiUrl"];
                if (string.IsNullOrEmpty(apiUrl))
                {
                    _logger.LogError("NbuExchangeApiUrl не налаштований у конфігурації.");
                    return null;
                }

                var requestUrl = $"{apiUrl}?start={minApiDate:yyyyMMdd}&end={maxApiDate:yyyyMMdd}&valcode=usd&json";
                _logger.LogInformation($"NbuCurrencyService: Виконання HTTP-запиту до API НБУ. URL: {requestUrl}");

                try
                {
                    var response = await _httpClient.GetAsync(requestUrl);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug($"NbuCurrencyService: Отримано сирий JSON від API. Частина: {content.Substring(0, Math.Min(content.Length, 200))}...");

                    var ratesResponse = JsonSerializer.Deserialize<List<NbuRateResponse>>(content, _jsonSerializerOptions);

                    var currencyRatesFromApi = ratesResponse?
                        .Where(r => r.CurrencyCode == UsdCurrencyCode)
                        .Select(r => new CurrencyRate { Date = r.ExchangeDate.Date, Rate = r.Rate })
                        .ToList();

                    if (currencyRatesFromApi != null && currencyRatesFromApi.Any())
                    {
                        _logger.LogInformation($"NbuCurrencyService: Отримано {currencyRatesFromApi.Count} курсів від API. Зберігання/оновлення в базу даних пакетно.");
                        await _currencyRepository.AddOrUpdateRatesAsync(currencyRatesFromApi); // Зберігаємо в БД
                        
                        foreach (var rate in currencyRatesFromApi)
                        {
                            ratesFromService.Add(rate);
                            string cacheKey = $"CurrencyRate_{rate.Date:yyyyMMdd}";
                            _cache.Set(cacheKey, rate, new MemoryCacheEntryOptions
                            {
                                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                            });
                            _logger.LogDebug($"Курс на {rate.Date:yyyy-MM-dd} отримано від API і додано в кеш.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"NbuCurrencyService: Від API НБУ не отримано курсів за період з {minApiDate:yyyy-MM-dd} по {maxApiDate:yyyy-MM-dd} або не знайдено USD.");
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError($"NbuCurrencyService: Критична помилка HTTP-запиту до API НБУ після всіх спроб: {ex.Message}");
                }
                catch (JsonException ex)
                {
                    _logger.LogError($"NbuCurrencyService: Помилка при парсингу JSON від API НБУ: {ex.Message}.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"NbuCurrencyService: Непередбачена помилка при отриманні курсів з API: {ex.Message}");
                }
            }
        }
        
        return ratesFromService.OrderBy(r => r.Date);
    }

    public async Task<CurrencyRate> GetCurrencyRateAsync(DateTime date)
    {
        _logger.LogInformation($"NbuCurrencyService: Запит курсу на конкретну дату: {date:yyyy-MM-dd}.");
        
        var rates = await GetCurrencyRatesAsync(date, date);
        return rates?.FirstOrDefault();
    }
}