# Cyclo

REST API for analysing cycling activities from GPX files – like the workout summary in Garmin Connect.
Project focused on production concerns: background processing, backpressure,
concurrency, testing and performance.

>  Work in progress.

## Features
- [x] GPX parsing (Garmin extensions: heart rate)
- [x] Metrics: distance, total / moving time (pause detection), average speed,
  elevation gain (hysteresis), heart rate, best 1 / 5 / 10 km (sliding window, O(n)) 
- [ ] Upload API with, background processing 

## Results vs Garmin Connect (same ride, 1579 GPS points)

| Metric          | Naive GPX reader | Cyclo   | Garmin  |
|-----------------|------------------|---------|---------|
| Moving time     | 4:30:46          | 2:08:59 | 2:08:44 |
| Avg speed       | 8.7 km/h         | 17.6    | 17.7    |
| Elevation gain  | 237 m            | 96 m    | 99 m    |

## Challenges
- **Pause detection** – a single 18-min recording gap explained most of the difference;
  rule: gap > 60 s = paused watch (smart recording never leaves gaps > ~20 s while riding).
- **Elevation noise** – naive sum counts ±1 m barometer flicker; hysteresis with a 1.1 m threshold.
- **Best segments** – naive O(n²) replaced with prefix sums + two pointers (O(n)); windows break at recording gaps.

## Tech stack
Now: .NET 10, xUnit” / „Planned: ASP.NET Core, EF Core, SQL Server, Testcontainers, GitHub Actions

## Project structure
```
src/Cyclo.Core     – gpx parser and metrics
src/Cyclo.Api      – Web API with controllers, EF Core, SQL Server, JWT, BackgroundService
tests/Cyclo.Core.Tests – xUnit
```

## Running tests
`LocalData` - tests on a private GPS file, not included in the repo
```bash
dotnet test --filter "Category!=LocalData"
```
