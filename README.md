# Cyclo

REST API for analysing cycling activities from GPX files – like the workout summary in Garmin Connect.
Project focused on production concerns: background processing, backpressure,
concurrency, testing and performance.

>  Work in progress.

## Features
- [x] GPX parsing (Garmin extensions: heart rate, temperature)
- [x] Metrics: distance, total / moving time (pause detection), average speed,
  elevation gain (hysteresis), heart rate, best 1 / 5 / 10 km (O(n) vs O(n²)) 
- [ ] Upload API with, background processing 

## Tech stack
.NET 10, ASP.NET Core (controllers), EF Core + SQL Server, xUnit

## Project structure
```
src/Cyclo.Core     – parsowanie i metryki, BEZ zależności od ASP.NET i bazy
src/Cyclo.Api      – Web API na kontrolerach, EF Core, SQL Server, JWT, BackgroundService
tests/Cyclo.Core.Tests – xUnit
```

## Running tests
```bash
dotnet test --filter "Category!=LocalData"
```
