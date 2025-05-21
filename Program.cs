using Microsoft.EntityFrameworkCore;
using CurrencyExchangeRateAggregator.Data;
using CurrencyExchangeRateAggregator.Services;
using Microsoft.OpenApi.Models;
using NLog.Web;
using Polly;

var builder = WebApplication.CreateBuilder(args);

// Configure NLog
builder.Logging.ClearProviders();
builder.Host.UseNLog();

// Додаємо HttpClient для ICurrencyService/NbuCurrencyService
builder.Services.AddHttpClient<ICurrencyService, NbuCurrencyService>()
    .AddTransientHttpErrorPolicy(policyBuilder =>
        policyBuilder.WaitAndRetryAsync(
            3, // Кількість повторних спроб
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            (exception, timespan, retryAttempt, context) =>
            {
                Console.WriteLine($"Помилка HTTP-запиту (спроба {retryAttempt}). Очікування {timespan.TotalSeconds:N1}с. Помилка: {exception.Exception?.Message ?? exception.Result?.StatusCode.ToString()}");
            }
        ));

// Add services to the container.
builder.Services.AddDbContext<CurrencyContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<ICurrencyService, NbuCurrencyService>();
builder.Services.AddScoped<ICurrencyRepository, CurrencyRepository>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1.0", new OpenApiInfo
    {
        Title = "Currency Rate Aggregator API",
        Version = "v1.0",
        Description = "API для отримання курсів валют UAH/USD"
    });
    c.EnableAnnotations();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("../swagger/v1.0/swagger.json", "Currency Rate Aggregator API v1.0");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();