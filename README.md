# Cyclo

REST API for analysing cycling activities from GPX files – like the workout summary in Garmin Connect.
Project focused on production concerns: background processing, backpressure,
concurrency, testing and performance.

>  Work in progress.

## Features
- [x] GPX parsing (Garmin extensions: heart rate)
- [x] Metrics: distance, total / moving time (pause detection), average speed, elevation gain (hysteresis), heart rate, best 1 / 5 / 10 km (sliding window, O(n))
- [x] `POST /activities` – upload, SHA-256 deduplication, GZip-compressed file storage
- [x] `GET /activities/{id}` – status + summary
- [ ] Background processing (Channel + BackgroundService)
- [ ] Activity list with pagination
- [ ] JWT auth

## Results vs Garmin Connect (same ride, 1579 GPS points)

| Metric          | Naive GPX reader | Cyclo   | Garmin  |
|-----------------|------------------|---------|---------|
| Moving time     | 4:30:46          | 2:08:59 | 2:08:44 |
| Avg speed       | 8.7 km/h         | 17.6    | 17.7    |
| Elevation gain  | 237 m            | 96 m    | 99 m    |

## Compression: GZip vs Brotli
2526 KB gpx file

| Algorithm | Level        | Size After [KB] |  Ratio | Compress [ms] | Decompress [ms]|
|-----------|--------------|-----------------|--------|---------------|----------------|
| GZip      | Fastest      | 463             |   5.5x |           3.1 |             3.6|
| GZip      | Optimal      | 317             |   8.0x |          13.3 |             3.0|
| GZip      | SmallestSize | 306             |   8.3x |          33.9 |             4.0|
| Brotli    | Fastest      | 326             |   7.8x |           4.0 |             5.4|
| Brotli    | Optimal      | 323             |   7.8x |          14.2 |             3.7|
| Brotli    | SmallestSize | 162             |  15.6x |        2094.5 |             5.0|

median of 10 runs, .NET 10, `System.IO.Compression`

## Challenges
- **Pause detection** - a single 18-min recording gap explained most of the difference; rule: gap > 60 s = paused watch (smart recording never leaves gaps > ~20 s while riding).
- **Elevation noise** - naive sum counts ±1 m barometer flicker; hysteresis with a 1.1 m threshold.
- **Best segments** - naive O(n²) replaced with prefix sums + two pointers (O(n)); windows break at recording gaps.
- **Compression** - Tested GZip vs Brotli - finally I chose Gzip (optimal) because wider compatibility (`SQL DDECOMPRESS()`) and best compression-to-time ratio
- **Deduplication** - unique index (UserId, FileHash)

## Tech stack
Now: .NET 10, xUnit, ASP.NET Core, EF Core, SQL Server (Docker).

Planned: Testcontainers, JWT, GitHub Actions

## Project structure
```
src/Cyclo.Core     – gpx parser and metrics
src/Cyclo.Api      – Web API with controllers, EF Core, SQL Server
tests/Cyclo.Core.Tests – xUnit
```

## Running tests
`LocalData` - tests on a private GPS file, not included in the repo
```bash
dotnet test --filter "Category!=LocalData"
```
