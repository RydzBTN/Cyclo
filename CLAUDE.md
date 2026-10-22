# Cyclo – kontekst projektu

## Kim jestem i po co ten projekt
Junior C#/.NET (inżynier informatyki), szukam pierwszej pracy jako backend .NET developer.
Cyclo to projekt portfolio: ma nauczyć mnie radzenia sobie z problemami systemów produkcyjnych
(asynchroniczność, współbieżność, testy, wydajność), a nie tylko CRUD.

## praca (WAŻNE)
- To projekt do NAUKI. Nie pisz za mnie całych klas ani funkcji, chyba że wprost o to poproszę.
- Gdy utknę: wyjaśnij koncepcję, wskaż kierunek, daj podpowiedź lub mały przykład obok mojego problemu.
- Rób code review mojego kodu jak senior w pracy: konkretnie, z uzasadnieniem, od najważniejszych uwag.
- Pytaj mnie, jak bym coś rozwiązał, zanim podasz rozwiązanie.
- Odpowiadaj po polsku. Kod, nazwy i commity po angielsku.
- Nie dodawaj zależności (NuGet) ani warstw abstrakcji bez wyjaśnienia, po co są.

## Co robi aplikacja
REST API do analizy telemetrii z jazdy rowerem (jak podsumowanie treningu w Garminie).
1. `POST /activities` – upload pliku GPX → walidacja, hash SHA-256, rekord `Pending`, od razu `202 Accepted` + `Location`.
2. Przetwarzanie w tle: `BoundedChannel<T>` + `BackgroundService` → parsowanie, filtrowanie szumu, metryki, zapis.
3. `GET /activities/{id}` – status (`Pending/Processing/Completed/Failed`) + wyniki.
4. `GET /activities` (paginacja), `GET /activities/{id}/splits` (podział na km).

Metryki: dystans (haversine), czas całkowity i czas ruchu (auto-pause), przewyższenie (wygładzanie + histereza),
najlepsze odcinki (1 km, 5 km, 20 min – okno przesuwne O(n)), tętno/kadencja/moc z rozszerzeń Garmina (`gpxtpx`).
Najpierw GPX (XML), FIT ewentualnie na końcu.

## Mechanizmy do stworzenia
- Backpressure: `BoundedChannel` + `TryWrite`, przy pełnej kolejce `429/503` z `Retry-After`.
- Odtwarzanie po restarcie: przy starcie ponowne wrzucenie rekordów `Pending` do kolejki.
- `SemaphoreSlim` – limit równoległego przetwarzania.
- `CancellationToken` przez całą ścieżkę + graceful shutdown.
- Deduplikacja: unikalny indeks `(UserId, FileHash)`, `DbUpdateException` → `409`.
- `RowVersion` na `Activity` (np. `POST /activities/{id}/reprocess`).
- Globalna obsługa błędów (`IExceptionHandler` / `ProblemDetails`), JWT z rolami, walidacja, DTO.

## Struktura
```
src/Cyclo.Core     – parsowanie i metryki, BEZ zależności od ASP.NET i bazy
src/Cyclo.Api      – Web API na kontrolerach, EF Core, SQL Server, JWT, BackgroundService
tests/Cyclo.Core.Tests – xUnit
(później) tests/Cyclo.Api.IntegrationTests – Testcontainers + WebApplicationFactory
(później) benchmarks/Cyclo.Benchmarks – BenchmarkDotNet
```
Zależności: Api → Core, Tests → Core. Core nie zna Api.
.NET 10, Rider, kontrolery (`[ApiController]`), nie Minimal API.
Punkty GPS nie trafiają do bazy – tylko podsumowanie, splity i surowy plik.

## Plan
| Tydzień | Zakres |
|---|---|
| 1 | Core: `TrackPoint`, `GeoMath.HaversineMeters`, `GpxParser`, `ActivityAnalyzer` + testy (bez webu) |
| 2 | API: upload/202/status, EF Core + migracje, JWT, walidacja, obsługa błędów, OpenAPI UI (Scalar/Swashbuckle) |
| 3 | Channel + BackgroundService, odtwarzanie po restarcie, semafor, cancellation |
| 4 | Testcontainers (prawdziwy SQL Server), Docker Compose, GitHub Actions (build + test) |
| 5 | Benchmark: `XDocument`+`Split`+LINQ vs `XmlReader`+`ReadOnlySpan<char>` (`[MemoryDiagnoser]`), README z diagramem Mermaid i sekcją „Wyzwania i rozwiązania” |
| 6 | Bufor, ewentualnie Azure |

Świadomie POMIJAMY na tym etapie: CQRS/MediatR, Transactional Outbox, Dapper, Redis, mikroserwisy, pełną Clean Architecture.

## Dziennik postępów

### Dzień 1 - 2026-10-19
- Referencje projektów, sprzątanie szablonu. FIT odłożony – zostajemy przy GPX.
- `Coordinates`, `GeoMath.HaversineMeters` + testy.
- `TrackPoint` (struct, pola opcjonalne nullable), `GpxParser` + testy.

### Dzień 2 - 2026-10-20
- `docs/concepts-guide.md` – przewodnik po zagadnieniach z planu.
- `ActivitySummary` + `ActivityAnalyzer` (dystans, czas całkowity) + testy. 38/38 zielonych.

### Dzień 3 - 22.10.2026
- Heart Rate - średnia i max
- metoda wykrywająca pauzy `isPause`
- prawdziwy: czas aktywności `ActivityTime`, dystans `ActivityDistance` i średnia prędkość `AvgSpeed`