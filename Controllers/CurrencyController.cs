using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using CurrencyExchangeRateAggregator.Data;
using CurrencyExchangeRateAggregator.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace CurrencyExchangeRateAggregator.Controllers;

[ApiController]
[Route("api/currency/uahusd")]
public class CurrencyController : ControllerBase
{
    private readonly ICurrencyService _currencyService;
    private readonly ICurrencyRepository _currencyRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CurrencyController> _logger;

    public CurrencyController(ICurrencyService currencyService, ICurrencyRepository currencyRepository,
        IConfiguration configuration, ILogger<CurrencyController> logger)
    {
        _currencyService = currencyService;
        _currencyRepository = currencyRepository;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("latest")]
    public async Task<ActionResult<decimal>> GetLatestRate()
    {
        _logger.LogInformation("Отримано запит на отримання останнього курсу валют.");

        var today = DateTime.UtcNow.Date;
        _logger.LogInformation($"Перевірка курсу на сьогоднішню дату: {today:yyyy-MM-dd} у базі даних.");

        var rate = await _currencyRepository.GetRateByDateAsync(today);
        if (rate == null)
        {
            _logger.LogInformation(
                $"Курс на {today:yyyy-MM-dd} не знайдено в базі даних. Спроба отримати з зовнішнього API.");
            rate = await _currencyService.GetCurrencyRateAsync(today);
            if (rate != null)
            {
                _logger.LogInformation(
                    $"Курс на {today:yyyy-MM-dd} успішно отримано від API: {rate.Rate}. Збереження в базу даних.");
                await _currencyRepository.AddOrUpdateRateAsync(rate);
                return Ok(rate.Rate);
            }
            else
            {
                _logger.LogWarning($"Не вдалося знайти курс на {today:yyyy-MM-dd} ні в базі, ні через API.");
                return NotFound("Курс на сьогодні не знайдено.");
            }
        }
        else
        {
            _logger.LogInformation($"Курс на {today:yyyy-MM-dd} знайдено в базі даних: {rate.Rate}.");
        }

        return Ok(rate.Rate);
    }

    [HttpGet("date/{date}")]
    public async Task<ActionResult<decimal>> GetRateByDate(
        [Required] [SwaggerParameter(Description = "Дата у форматі РРРР-ММ-ДД")]
        DateTime date)
    {
        _logger.LogInformation($"Отримано запит на отримання курсу на конкретну дату: {date:yyyy-MM-dd}.");

        var retentionMonths = _configuration.GetValue<int>("DataRetentionMonths");

        var earliestAllowedDate = DateTime.Now.Date.AddMonths(-retentionMonths).AddDays(1);

        if (date.Date < earliestAllowedDate)
        {
            _logger.LogWarning(
                $"Запит на дату {date:yyyy-MM-dd} є занадто старим. Дозволено з {earliestAllowedDate:yyyy-MM-dd}.");
            return BadRequest(
                $"Запит курсу за дату раніше ніж {retentionMonths} місяців тому не підтримується. Будь ласка, виберіть дату починаючи з {earliestAllowedDate.ToString("yyyy-MM-dd")} або пізніше.");
        }

        _logger.LogInformation($"Перевірка курсу на {date:yyyy-MM-dd} у базі даних.");
        var rate = await _currencyRepository.GetRateByDateAsync(date.Date);
        if (rate == null)
        {
            _logger.LogInformation(
                $"Курс на {date:yyyy-MM-dd} не знайдено в базі даних. Спроба отримати з зовнішнього API.");
            rate = await _currencyService.GetCurrencyRateAsync(date.Date);
            if (rate != null)
            {
                _logger.LogInformation(
                    $"Курс на {date:yyyy-MM-dd} успішно отримано від API: {rate.Rate}. Збереження в базу даних.");
                await _currencyRepository.AddOrUpdateRateAsync(rate);
                return Ok(rate.Rate);
            }
            else
            {
                _logger.LogWarning($"Не вдалося знайти курс на {date:yyyy-MM-dd} ні в базі, ні через API.");
                return NotFound($"Курс на дату {date.ToString("yyyy-MM-dd")} не знайдено.");
            }
        }
        else
        {
            _logger.LogInformation($"Курс на {date:yyyy-MM-dd} знайдено в базі даних: {rate.Rate}.");
        }

        return Ok(rate.Rate);
    }

    [HttpGet("average")]
    public async Task<ActionResult<decimal>> GetAverageRate(
        [FromQuery] [SwaggerParameter(Description = "Дата початку періоду у форматі YYYY-MM-DD")]
        DateTime startDate,
        [FromQuery] [SwaggerParameter(Description = "Дата закінчення періоду у форматі YYYY-MM-DD")]
        DateTime endDate)
    {
        _logger.LogInformation($"Отримано запит на середній курс за період з {startDate} по {endDate}.");

        if (startDate > endDate)
        {
            _logger.LogError($"Некоректний запит: дата початку ({startDate}) пізніше за дату закінчення ({endDate}).");
            return BadRequest("Дата початку періоду не може бути пізніше за дату закінчення.");
        }

        var retentionMonths = _configuration.GetValue<int>("DataRetentionMonths");
        var earliestAllowedDate = DateTime.UtcNow.AddMonths(-retentionMonths).Date.AddDays(1);

        if (startDate < earliestAllowedDate || endDate < earliestAllowedDate)
        {
            _logger.LogWarning(
                $"Запит за межами дозволеного періоду. Запит з {startDate} по {endDate}, дозволено з {earliestAllowedDate}.");
            return BadRequest(
                $"Вказаний період містить дані, старші за {retentionMonths} місяців. Будь ласка, виберіть період починаючи з {earliestAllowedDate.ToString("yyyy-MM-dd")} або пізніше.");
        }

        var ratesFromDb = await _currencyRepository.GetRatesByPeriodAsync(startDate.Date, endDate.Date);
        var periodLength = (endDate.Date - startDate.Date).Days + 1;

        if (ratesFromDb.Count() < periodLength)
        {
            _logger.LogInformation($"Недостатньо даних у базі за період з {startDate} по {endDate}. Запит до API.");
            var ratesFromApi = await _currencyService.GetCurrencyRatesAsync(startDate.Date, endDate.Date);
            if (ratesFromApi != null)
            {
                _logger.LogInformation($"Отримано {ratesFromApi.Count()} курсів від API. Збереження в базу даних.");
                foreach (var rate in ratesFromApi)
                {
                    await _currencyRepository.AddOrUpdateRateAsync(rate);
                }

                ratesFromDb = await _currencyRepository.GetRatesByPeriodAsync(startDate.Date, endDate.Date);
            }
            else
            {
                _logger.LogError($"Не вдалося отримати дані від API за період з {startDate} по {endDate}.");
            }
        }

        if (!ratesFromDb.Any(r => r.Date >= startDate.Date && r.Date <= endDate.Date))
        {
            _logger.LogWarning(
                $"Курси за період з {startDate.ToString("yyyy-MM-dd")} по {endDate.ToString("yyyy-MM-dd")} не знайдено.");
            return NotFound(
                $"Курси за період з {startDate.ToShortDateString()} по {endDate.ToShortDateString()} не знайдено.");
        }

        var averageRate = ratesFromDb
            .Where(r => r.Date >= startDate.Date && r.Date <= endDate.Date)
            .Average(r => r.Rate);

        _logger.LogInformation($"Середній курс за період з {startDate} по {endDate}: {averageRate}.");
        return Ok(averageRate);
    }
}