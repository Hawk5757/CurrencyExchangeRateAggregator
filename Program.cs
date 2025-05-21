using Microsoft.EntityFrameworkCore;
using CurrencyExchangeRateAggregator.Data;
using CurrencyExchangeRateAggregator.Services;
using Microsoft.OpenApi.Models;
using NLog.Web;
using Polly;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Налаштування логування NLog ---
builder.Logging.ClearProviders(); 
builder.Host.UseNLog();           

// --- 2. Реєстрація сервісів ---

// Реєстрація DbContext для SQLite
builder.Services.AddDbContext<CurrencyContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Реєстрація IMemoryCache (для кешування в пам'яті застосунку)
builder.Services.AddMemoryCache();

// Реєстрація HttpClient для ICurrencyService з політикою повторних спроб (Polly)
// Метод AddHttpClient<TClient, TImplementation>() автоматично реєструє
// NbuCurrencyService як реалізацію ICurrencyService у DI-контейнері
// та надає сконфігурований HttpClient.
builder.Services.AddHttpClient<ICurrencyService, NbuCurrencyService>()
    .AddTransientHttpErrorPolicy(policyBuilder =>
        policyBuilder.WaitAndRetryAsync(
            3, // Кількість повторних спроб
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Експоненціальна затримка: 2с, 4с, 8с
            (exception, timespan, retryAttempt, context) =>
            {
                // Логування повторних спроб можна зробити тут.
                // В умовах реального застосунку краще використовувати ILogger,
                // але для простоти прикладу Console.WriteLine теж підійде.
                Console.WriteLine($"[Polly] Помилка HTTP-запиту (спроба {retryAttempt}). Очікування {timespan.TotalSeconds:N1}с. " +
                                  $"Помилка: {exception.Exception?.Message ?? (exception.Result != null ? $"HTTP {exception.Result.StatusCode}" : "Невідома")}");
            }
        ));

// Реєстрація репозиторію
builder.Services.AddScoped<ICurrencyRepository, CurrencyRepository>();

// Додавання контролерів
builder.Services.AddControllers();

// --- 3. Налаштування Swagger/OpenAPI ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1.0", new OpenApiInfo
    {
        Title = "Currency Rate Aggregator API",
        Version = "v1.0",
        Description = "API для отримання курсів валют UAH/USD"
    });
    c.EnableAnnotations(); // Дозволяє використання Swagger/Swashbuckle анотацій
});

// --- 4. Побудова та налаштування HTTP Request Pipeline ---
var app = builder.Build();

// Конфігурація HTTP request pipeline для середовища розробки
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(); // Вмикає middleware для генерації JSON-документа Swagger
    app.UseSwaggerUI(c => // Вмикає middleware для Swagger UI
    {
        c.SwaggerEndpoint("../swagger/v1.0/swagger.json", "Currency Rate Aggregator API v1.0");
    });
}

// Перенаправлення HTTP-запитів на HTTPS
app.UseHttpsRedirection();

// Додає middleware для авторизації (якщо потрібно)
app.UseAuthorization();

// Мапує атрибути маршрутизації контролерів
app.MapControllers();

// Запускає застосунок
app.Run();