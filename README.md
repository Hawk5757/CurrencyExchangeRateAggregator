# Currency Exchange Rate Aggregator

## Initial Technical Specification (TS)

### User Story:
"As a user, I want to analyze the Hryvnia to US Dollar exchange rate, and also be able to view the average rate over a certain period, to make decisions about buying or selling currency."

### Description:
Implement a service that obtains the Hryvnia to US Dollar exchange rate from an open source and provides a REST interface for accessing the current and average rate for a selected period.
The goal is to assess the candidate's technical competence, ability to work with APIs, organize application structure, and implement business logic considering data storage and aggregation.

### Acceptance Criteria:
1. Ability to retrieve UAH/USD exchange rate data for a specific date.
2. Ability to retrieve the daily average rate for a period.

### Notes:
* Data source: NBU API or another public API with exchange rates (only official UAH/USD rate).
* Database: SQLite, InMemory, or another, at the candidate's discretion.
* The service should be easily integrable with another service or web application in the future.
* For the test task, it is sufficient to collect data no older than 3 months.

---

## General Solution Overview

This project is an ASP.NET Core Web API designed to aggregate and provide historical and current exchange rates (currently UAH/USD) from the National Bank of Ukraine (NBU). It efficiently retrieves, stores, and serves currency rate data, ensuring resilience against temporary external API issues and optimal database performance.

Key functionalities:
* Retrieving currency rates for a specific date or period.
* Retrieving the latest available rate.
* Storing retrieved data in a local SQLite database for quick access and reducing external API requests.
* Supporting data retention limits for storage optimization.

## Key Technologies and Libraries

* **ASP.NET Core**: Framework for building web APIs.
* **Entity Framework Core**: ORM for interacting with the SQLite database.
* **NLog**: Flexible logging library for application events.
* **Polly**: Library for implementing resilience policies (such as retries) for HTTP requests.
* **Swagger/OpenAPI**: For automatic API documentation generation and easy testing.

## Improvements and Optimizations

During the development and enhancement of the solution, the following significant improvements have been implemented:

1.  **Logging (NLog)**:
    * Centralized logging using NLog has been introduced, allowing monitoring application operation, identifying errors and warnings. Logs are written to both the console and daily files.
2.  **Retry Mechanism for HTTP Requests (Polly)**:
    * A retry policy has been added to HTTP requests to the external NBU API using the Polly library. This increases the application's resilience to temporary network issues or external service overload, automatically retrying requests with exponential backoff.
3.  **Database Operations Optimization**:
    * **Batch Add/Update Records**: Instead of single database write operations, a mechanism for batch adding and updating rates (`AddOrUpdateRatesAsync` method in `CurrencyRepository`) has been implemented. This significantly reduces the number of database accesses and improves performance when synchronizing large amounts of data.
    * **Database Indexes**: A unique index has been added to the `Date` field in the `CurrencyRates` table. This significantly speeds up data retrieval, filtering, and sorting by date, optimizing database queries.
4.  **Business Logic Encapsulation**:
    * Business logic, such as average rate calculation (`GetAverageRate`), has been moved from the controller to a separate service layer (`ICurrencyService`), improving modularity, testability, and code readability.
5.  **`using` Directive Optimization**:
    * Unnecessary `using` directives have been removed from the code, especially those automatically imported by the **Implicit Usings** feature in modern .NET versions. This enhances code readability and cleanliness.
6.  **In-Memory Caching (IMemoryCache)**:
    * In-memory caching of currency rate data has been implemented using `IMemoryCache`. This significantly speeds up repeated requests for already retrieved dates, reducing the load on the database and external APIs, and providing lightning-fast responses for frequently requested data.

## Getting Started

### Prerequisites

* .NET SDK (recommended .NET 8.0 or higher)
* IDE: Rider or Visual Studio

### Cloning the Repository

```bash
git clone [https://github.com/Hawk5757/CurrencyExchangeRateAggregator.git](https://github.com/Hawk5757/CurrencyExchangeRateAggregator.git)
cd CurrencyExchangeRateAggregator
