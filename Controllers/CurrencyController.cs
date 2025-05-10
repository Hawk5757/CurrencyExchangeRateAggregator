using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using CurrencyExchangeRateAggregator.Data;
using CurrencyExchangeRateAggregator.Services;
using Microsoft.Extensions.Configuration;
using Swashbuckle.AspNetCore.Annotations;

namespace CurrencyExchangeRateAggregator.Controllers;

[ApiController]
[Route("api/currency/uahusd")]
public class CurrencyController : ControllerBase
{
    private readonly ICurrencyService _currencyService;
    private readonly ICurrencyRepository _currencyRepository;
    private readonly IConfiguration _configuration;

    public CurrencyController(ICurrencyService currencyService, ICurrencyRepository currencyRepository,
        IConfiguration configuration)
    {
        _currencyService = currencyService;
        _currencyRepository = currencyRepository;
        _configuration = configuration;
    }

    [HttpGet("latest")]
    public async Task<ActionResult<decimal>> GetLatestRate()
    {
        var today = DateTime.UtcNow.Date;
        var rate = await _currencyRepository.GetRateByDateAsync(today);
        if (rate == null)
        {
            rate = await _currencyService.GetCurrencyRateAsync(today);
            if (rate != null)
            {
                await _currencyRepository.AddOrUpdateRateAsync(rate);
                return Ok(rate.Rate);
            }

            return NotFound("Курс на сьогодні не знайдено.");
        }

        return Ok(rate.Rate);
    }

    [HttpGet("date/{date}")]
    public async Task<ActionResult<decimal>> GetRateByDate(
        [Required] [SwaggerParameter(Description = "Дата у форматі YYYY-MM-DD")] DateTime date)
    {
        var retentionMonths = _configuration.GetValue<int>("DataRetentionMonths");
        var earliestAllowedDate = DateTime.UtcNow.AddMonths(-retentionMonths).Date.AddDays(1);

        if (date.Date < earliestAllowedDate)
        {
            return BadRequest(
                $"Запит курсу за дату раніше ніж {retentionMonths} місяців тому не підтримується. Будь ласка, виберіть дату починаючи з {earliestAllowedDate.ToString("yyyy-MM-dd")} або пізніше.");
        }

        var rate = await _currencyRepository.GetRateByDateAsync(date.Date);
        if (rate == null)
        {
            rate = await _currencyService.GetCurrencyRateAsync(date.Date);
            if (rate != null)
            {
                await _currencyRepository.AddOrUpdateRateAsync(rate);
                return Ok(rate.Rate);
            }

            return NotFound($"Курс на дату {date.ToShortDateString()} не знайдено.");
        }

        return Ok(rate.Rate);
    }

    [HttpGet("average")]
    public async Task<ActionResult<decimal>> GetAverageRate(
        [FromQuery] [SwaggerParameter(Description = "Дата початку періоду у форматі YYYY-MM-DD")] DateTime startDate,
        [FromQuery] [SwaggerParameter(Description = "Дата закінчення періоду у форматі YYYY-MM-DD")] DateTime endDate)
    {
        if (startDate > endDate)
        {
            return BadRequest("Дата початку періоду не може бути пізніше за дату закінчення.");
        }

        var retentionMonths = _configuration.GetValue<int>("DataRetentionMonths");
        var earliestAllowedDate = DateTime.UtcNow.AddMonths(-retentionMonths).Date.AddDays(1);

        if (startDate < earliestAllowedDate || endDate < earliestAllowedDate)
        {
            return BadRequest(
                $"Вказаний період містить дані, старші за {retentionMonths} місяців. Будь ласка, виберіть період починаючи з {earliestAllowedDate.ToString("yyyy-MM-dd")} або пізніше.");
        }

        var ratesFromDb = await _currencyRepository.GetRatesByPeriodAsync(startDate.Date, endDate.Date);
        var periodLength = (endDate.Date - startDate.Date).Days + 1;

        if (ratesFromDb.Count() < periodLength)
        {
            var ratesFromApi = await _currencyService.GetCurrencyRatesAsync(startDate.Date, endDate.Date);
            if (ratesFromApi != null)
            {
                foreach (var rate in ratesFromApi)
                {
                    await _currencyRepository.AddOrUpdateRateAsync(rate);
                }

                ratesFromDb = await _currencyRepository.GetRatesByPeriodAsync(startDate.Date, endDate.Date);
            }
        }

        if (!ratesFromDb.Any(r => r.Date >= startDate.Date && r.Date <= endDate.Date))
        {
            return NotFound(
                $"Курси за період з {startDate.ToShortDateString()} по {endDate.ToShortDateString()} не знайдено.");
        }

        var averageRate = ratesFromDb
            .Where(r => r.Date >= startDate.Date && r.Date <= endDate.Date)
            .Average(r => r.Rate);

        return Ok(averageRate);
    }
}