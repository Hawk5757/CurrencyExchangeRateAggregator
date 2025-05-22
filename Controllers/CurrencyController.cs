using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using CurrencyExchangeRateAggregator.Data;
using CurrencyExchangeRateAggregator.Models;
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
    [HttpGet("date/{date}")]
    [SwaggerOperation(Summary = "Отримати курс валют UAH/USD за вказану дату", Description = "Повертає офіційний курс гривні до долара США на конкретну дату.")]
    [SwaggerResponse(200, "Успішно отримано курс", typeof(CurrencyRate))]
    [SwaggerResponse(400, "Некоректний формат дати")]
    [SwaggerResponse(404, "Курс на вказану дату не знайдено")]
    [SwaggerResponse(500, "Внутрішня помилка сервера")]
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

    [HttpGet("average")] // Використовуємо FromQuery для дат у URL
    [SwaggerOperation(Summary = "Отримати середній курс UAH/USD за період", Description = "Обчислює середньодобове значення курсу гривні до долара США за вказаний період.")]
    [SwaggerResponse(200, "Успішно отримано середній курс", typeof(decimal))]
    [SwaggerResponse(400, "Некоректний запит (формат дати, період не відповідає вимогам, початкова дата пізніша за кінцеву)")]
    [SwaggerResponse(404, "Дані для обчислення середнього курсу не знайдено")]
    [SwaggerResponse(500, "Внутрішня помилка сервера")]
    public async Task<ActionResult<decimal>> GetAverageRate(
        [FromQuery] [SwaggerParameter(Description = "Дата початку періоду у форматі YYYY-MM-DD")]
        DateTime startDate,
        [FromQuery] [SwaggerParameter(Description = "Дата закінчення періоду у форматі YYYY-MM-DD")]
        DateTime endDate)
    {
        _logger.LogInformation(
            $"CurrencyController: Отримано запит на середній курс за період з {startDate:yyyy-MM-dd} по {endDate:yyyy-MM-dd}.");

        if (startDate > endDate)
        {
            _logger.LogWarning(
                $"CurrencyController: Некоректний запит: дата початку ({startDate:yyyy-MM-dd}) пізніше за дату закінчення ({endDate:yyyy-MM-dd}).");
            return BadRequest("Дата початку періоду не може бути пізніше за дату закінчення.");
        }

        try
        {
            var averageRate = await _currencyService.GetAverageRateAsync(startDate.Date, endDate.Date);

            if (!averageRate.HasValue)
            {
                _logger.LogWarning(
                    $"CurrencyController: Дані для обчислення середнього курсу за період з {startDate:yyyy-MM-dd} по {endDate:yyyy-MM-dd} не знайдено.");
                return NotFound("Дані для обчислення середнього курсу за вказаний період не знайдено або недостатньо.");
            }

            _logger.LogInformation(
                $"CurrencyController: Середній курс за період з {startDate:yyyy-MM-dd} по {endDate:yyyy-MM-dd} успішно обчислено: {averageRate.Value:N4}.");
            return Ok(averageRate.Value);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogWarning($"CurrencyController: Помилка перевірки діапазону для середнього курсу: {ex.Message}");
            return BadRequest(ex.Message); // Повертаємо повідомлення про помилку retention
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"CurrencyController: Непередбачена помилка при отриманні середнього курсу за період з {startDate:yyyy-MM-dd} по {endDate:yyyy-MM-dd}.");
            return StatusCode(500, "Внутрішня помилка сервера при обчисленні середнього курсу.");
        }
    }
}