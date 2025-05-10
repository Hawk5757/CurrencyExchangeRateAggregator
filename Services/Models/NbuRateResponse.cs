using System;
using System.Text.Json.Serialization;

namespace CurrencyExchangeRateAggregator.Services.Models;

public class NbuRateResponse
{
    [JsonPropertyName("exchangedate")]
    public DateTime ExchangeDate { get; set; }

    [JsonPropertyName("r030")]
    public int CurrencyCode { get; set; }

    [JsonPropertyName("cc")]
    public string Currency { get; set; }

    [JsonPropertyName("txt")]
    public string Description { get; set; }

    [JsonPropertyName("enname")]
    public string EnglishName { get; set; }

    [JsonPropertyName("rate")]
    public decimal Rate { get; set; }

    [JsonPropertyName("units")]
    public int Units { get; set; }

    [JsonPropertyName("rate_per_unit")]
    public decimal RatePerUnit { get; set; }

    [JsonPropertyName("group")]
    public string Group { get; set; }

    [JsonPropertyName("calcdate")]
    public DateTime CalculationDate { get; set; }
}