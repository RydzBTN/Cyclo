# Cyclo – kontekst projektu

## praca (WAŻNE)
- To projekt do NAUKI. Nie pisz za mnie całych klas ani funkcji, chyba że wprost o to poproszę.
- Gdy utknę: wyjaśnij koncepcję, wskaż kierunek, daj podpowiedź lub mały przykład obok mojego problemu.
- Rób code review mojego kodu jak senior w pracy: konkretnie, z uzasadnieniem, od najważniejszych uwag.
- Pytaj mnie, jak bym coś rozwiązał, zanim podasz rozwiązanie.
- Odpowiadaj po polsku. Kod, nazwy i commity po angielsku.
- Nie dodawaj zależności (NuGet) ani warstw abstrakcji bez wyjaśnienia, po co są.

## Co robi aplikacja
REST API do analizy telemetrii z jazdy rowerem (jak podsumowanie treningu w Garminie).
1. `POST /activities` – upload pliku GPX → walidacja, hash SHA-256 z pliku przed kompresją, rekord `Pending`, od razu `202 Accepted` + `Location`.
2. Przetwarzanie w tle: `BoundedChannel<T>` + `BackgroundService` → parsowanie, filtrowanie szumu, metryki, zapis.
3. `GET /activities/{id}` – status (`Pending/Processing/Completed/Failed`) + wyniki.
4. `GET /activities` (paginacja)

Metryki: dystans (haversine), czas całkowity i czas ruchu (auto-pause), przewyższenie (wygładzanie + histereza),
najlepsze odcinki (1 km, 5 km, 10 km – okno przesuwne O(n)), tętno/kadencja/moc z rozszerzeń Garmina (`gpxtpx`).
Najpierw GPX (XML), FIT ewentualnie na końcu.

## Mechanizmy do stworzenia
- Backpressure: `BoundedChannel` + `TryWrite`, przy pełnej kolejce `429/503` z `Retry-After`.
- Odtwarzanie po restarcie: przy starcie ponowne wrzucenie rekordów `Pending` do kolejki.
- `SemaphoreSlim` – limit równoległego przetwarzania.
- `CancellationToken` przez całą ścieżkę + graceful shutdown.
- Deduplikacja: unikalny indeks `(UserId, FileHash)`, `DbUpdateException` → `409`.
- `RowVersion` na `Activity` (np. `POST /activities/{id}/reprocess`).
- Globalna obsługa błędów (`IExceptionHandler` / `ProblemDetails`), JWT z rolami, walidacja, DTO.

## Bezpieczeństwo – minimum (bez osobnego tygodnia, przy okazji tygodnia 2)
- Parser GPX: `XmlReader` z `DtdProcessing.Prohibit` (ochrona przed XXE).
- Limit rozmiaru uploadu.
- Użytkownik widzi tylko SWOJE aktywności (cudzy id → `404`).
- Klucz JWT poza repo (user-secrets), brak stack trace’ów w odpowiedziach poza Development.

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
Punkty GPS nie trafiają do bazy – tylko podsumowanie,  i surowy plik.

## Plan
| Tydzień | Zakres |
|---|---|
| 1 | Core: `TrackPoint`, `GeoMath.HaversineMeters`, `GpxParser`, `ActivityAnalyzer` + testy (bez webu) |
| 2 | API: upload/202/status, EF Core + migracje, JWT, walidacja, obsługa błędów, OpenAPI UI (Scalar/Swashbuckle) |
| 3 | Channel + BackgroundService, odtwarzanie po restarcie, semafor, cancellation |
| 4 | Frontend w React + TS - lista treningów i wykres trasy |
| 5 | Testcontainers (prawdziwy SQL Server), Docker Compose, GitHub Actions (build + test) |
| 6 | Benchmark: `XDocument`+LINQ vs `XmlReader`+`ReadOnlySpan<char>` (`[MemoryDiagnoser]`), README z diagramem Mermaid i sekcją „Wyzwania i rozwiązania” |
| 7 | Bufor. Opcjonalnie: jeden prosty deploy na Azure (Container Apps + Azure SQL) przez Azure CLI, z budżetem i alertem kosztów; po wszystkim usunięcie grupy zasobów |

Świadomie POMIJAMY na tym etapie: CQRS/MediatR, Transactional Outbox, Dapper, Redis, mikroserwisy, pełną Clean Architecture.

## Dziennik postępów

### Dzień 1
- Referencje projektów, sprzątanie szablonu. FIT odłożony – zostajemy przy GPX.
- `Coordinates`, `GeoMath.HaversineMeters` + testy.
- `TrackPoint` (struct, pola opcjonalne nullable), `GpxParser` + testy.

### Dzień 2
- `docs/concepts-guide.md` – przewodnik po zagadnieniach z planu.
- `ActivitySummary` + `ActivityAnalyzer` (dystans, czas całkowity) + testy. 38/38 zielonych.

### Dzień 3
- Heart Rate - średnia i max
- metoda wykrywająca pauzy `isPause`
- prawdziwy: czas aktywności `ActivityTime`, dystans `ActivityDistance` i średnia prędkość `AvgSpeed`

### Dzień 4
- Elewacja
- Best Segment dla 1/5/10 Km - liczy najszybszy odcinek 
- GetBestSegmentFast zlozoność (O(n) zamiast O(n²) w GetBestSegment)
- Skończone założenia Cyclo.Core

### Dzień 5
- Start API: pierwszy kontroler `ActivityController`
- Encje bazy: `ActivityEntity`, `ActivitySummaryEntity`, `BestSegmentEntity`, `ProcessStatus` (oddzielone od modeli z Core).
- SQL Server w Dockerze.
- Connection string w user secrets
- `CycloDbContext`: podsumowanie i najlepsze odcinki jako owned types (jedna tabela), status jako tekst
- Indeksy: unikalny `(UserId, FileHash)` – blokuje duplikaty plików, zwykły `(UserId, UploadedAt)` – pod listę aktywności.
- Pierwsza migracja `InitialCreate`, baza `Cyclo` z tabelą `Activities`.
- encja dla plików .gpx z relacją 1:1 do `ActivityEntity`.

### Dzień 6
- endpoint POST activities:
  - hasowanie pliku sha256
  - kompresja GZip | mode: Optimal| 317 KB| stopień  8,0x | kompresja 13,5 ms |  dekompresja 4,1 ms
  - zapis do bazy aktywności i osobno pliku
  - zwraca id i status
  
### Dzień 7
- DTO dla odpowiedzi aktywności
  - activity, summary, bestSegment
- endpoint `GET activities/{id}`
  - mapowanie przez projekcje
  - Testy:
    - Nieistniejący guid - application/problem+json (api controller automatycznie opakował błąd)
    - Wywołanie z id - routing nie znalazł endpointu i zwrócił pustą treść - Content-Length: 0
    - wywołanie z innym userid - to samo co nieistniejące guid
    - do zrobienia wspolny format błędu

### Dzień 8
- Globalna obsługa błędów z pomocą `builder.Services.AddProblemDetails()`, `app.UseExceptionHandler()`, `app.UseStatusCodePages()`.
- stack trace tylko w Dev build, w Prod TraceId.

### Dzień 9
- `ActivityCursor.cs` kodowanie i dekodowanie kursora - zawiera date, id i jest kodowany do stringa.
- endpint `GetPage` zwracający liste aktywności i kursor do pobrania następnej strony
  - gdy dwie aktywności mają taką samą date to decyduje GUID (`CompareTo`)
  - gdy kursor jest null to znaczy że nie ma więcej aktywności
- Dodanie opisu Rest API za pomocą openAPI
- Dodanie interfejsu graficznego Scalar
- Ustawienie zwracanych typów `ProducesResponseType` dla dokumentacji OpenApi

### Dzień 10
- użytkownicy
- register/login

### Dzień 11
- JWT: `TokenService` tworzenie tokena (`JsonWebTokenHandler`, HS256, claimy `sub` + `role`), login zwraca `AccessToken`
- Options pattern: `JwtOptions` + `ValidateOnStart`, klucz w user-secrets
- `[Authorize]` na `ActivityController`, `User.GetUserId()` (metoda rozszerzająca)
- Testy `JwtTests.http`














