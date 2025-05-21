using System.Text.Json;
using CurrencyExchangeRateAggregator.Data;
using CurrencyExchangeRateAggregator.Helpers;
using CurrencyExchangeRateAggregator.Models;
using CurrencyExchangeRateAggregator.Services.Models;

namespace CurrencyExchangeRateAggregator.Services;

public class NbuCurrencyService : ICurrencyService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private const int UsdCurrencyCode = 840;
    private readonly ILogger<NbuCurrencyService> _logger;
    private readonly ICurrencyRepository _currencyRepository;
    
    public NbuCurrencyService(HttpClient httpClient, IConfiguration configuration, ILogger<NbuCurrencyService> logger,
    ICurrencyRepository currencyRepository)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _currencyRepository = currencyRepository;

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
                _logger.LogError("NbuExchangeApiUrl не налаштований у конфігурації. Будь ласка, перевірте appsettings.json.");
                return null;
            }

            var requestUrl = $"{apiUrl}?start={startDate:yyyyMMdd}&end={endDate:yyyyMMdd}&valcode=usd&json";
            _logger.LogInformation($"NbuCurrencyService: Запит курсів за період з {startDate:yyyy-MM-dd} по {endDate:yyyy-MM-dd}. URL: {requestUrl}");

            try
            {
                var response = await _httpClient.GetAsync(requestUrl);
                response.EnsureSuccessStatusCode(); 

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug($"NbuCurrencyService: Отримано сирий JSON від API. Частина: {content.Substring(0, Math.Min(content.Length, 200))}...");

                
                var ratesResponse = JsonSerializer.Deserialize<List<NbuRateResponse>>(content, _jsonSerializerOptions);
                
                var currencyRates = ratesResponse?
                    .Where(r => r.CurrencyCode == UsdCurrencyCode)
                    .Select(r => new CurrencyRate { Date = r.ExchangeDate.Date, Rate = r.Rate })
                    .ToList();

                if (currencyRates != null && currencyRates.Any())
                {
                    _logger.LogInformation($"NbuCurrencyService: Отримано {currencyRates.Count} курсів від API. Зберігання/оновлення в базу даних пакетно.");
                    await _currencyRepository.AddOrUpdateRatesAsync(currencyRates);
                }
                else
                {
                    _logger.LogWarning($"NbuCurrencyService: Від API НБУ не отримано курсів за період з {startDate:yyyy-MM-dd} по {endDate:yyyy-MM-dd} або не знайдено USD.");
                }

                return currencyRates;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"NbuCurrencyService: Критична помилка HTTP-запиту до API НБУ після всіх спроб: {ex.Message}");
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError($"NbuCurrencyService: Помилка при парсингу JSON від API НБУ: {ex.Message}. Перевірте формат відповіді.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"NbuCurrencyService: Непередбачена помилка при отриманні курсів: {ex.Message}");
                return null;
            }
    }

    public async Task<CurrencyRate> GetCurrencyRateAsync(DateTime date)
    {
        _logger.LogInformation($"NbuCurrencyService: Запит курсу на конкретну дату: {date:yyyy-MM-dd}.");
        
        var rates = await GetCurrencyRatesAsync(date, date);
        return rates?.FirstOrDefault();
    }
}