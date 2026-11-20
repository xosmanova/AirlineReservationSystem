# AirlineReservationSystem: Konsolidə edilmiş plan (Gün 9-40)

> Bu fayl `docs/PLAN.md` kimi saxlanılır və **tək həqiqət mənbəyidir**. Agent hər gün `AGENT_RULES.md` ilə birlikdə bunu oxuyur.
> Əvvəlki gün bölgüləri (Redis, Docker, CI/CD, 3D Secure, parol sıfırlama, RBAC və s.) **etibarsızdır**.
> Layihə: Code Academy Final Layihəsi, Project 19 (Speech-Enabled Airline Reservation System). Hibrid model: Admin = ASP.NET Core MVC + `HttpClient` (Variant B), istifadəçi saytı = JS + Fetch (Variant A), ikisi də eyni ASP.NET Core API ilə danışır.

---

## 1. Cari vəziyyət (Gün 1-8 və hardening: TAMAMLANDI)

- Solution `AirlineReservationSystem.slnx`, layihələr: `Domain`, `Application`, `Infrastructure`, `API`, `Admin` (MVC), `Client` (statik HTML/CSS/JS). Admin heç bir daxili qata istinad etmir.
- Application: CQRS (**MediatR 12.5.0**, Apache 2.0), `ApiResponse<T>` zərfi, FluentValidation + `ValidationBehavior`, **Riok.Mapperly**, xüsusi exception-lar, `GlobalExceptionHandlerMiddleware`, `TimeProvider`.
- Infrastructure: EF Core 10 + SQL Server (`.\SQLEXPRESS`), qlobal soft delete filtri, filtrli unikal indekslər, tool-generated migration, `Generic Repository` + `UnitOfWork`, seed.
- Seed (demo data): 7 aeroport, 4 aviaşirkət, 8 təyyarə (DEMO-xxx), 1488 oturacaq, 60 günlük reyslər (hub əsaslı, üst-üstə düşmə yoxdur), ikitərəfli marşrut əhatəsi yoxlanılıb.
- Gün 8: uçuş axtarışı və reys detalı (CQRS handler, `FlightsController`).
- **Gün 9 (TAMAMLANDI):** axtarış SQL-də yenidən yazıldı (proyeksiya, alt sorğu ilə boş yer sayı, `passengersCount` filtri `OFFSET`-dən əvvəl, 2 komanda: data + `COUNT`, `Scheduled`/`Delayed` satılabilir, `PageSize` ≤ 50), fail-fast saat qurşağı, real SQL Server inteqrasiya test bazası (`AirlineReservationDb_Test`), Scalar + XML şərhləri. Commit-lər: `cad120d`, `4f1bf77`, `a3c7ce3`, `5fafc5d`. Testlər: 62 (58 unit + 4 inteqrasiya).
- **Gün 10-14 (TAMAMLANDI):** aeroport/aviaşirkət API və alias-lar; Identity (`AppUser`, rollar, admin `user-secrets`-dən, `Booking.UserId` məcburi); JWT və `AuthController` (`AddIdentityCore`, 401); oturacaq xəritəsi JSON müqaviləsi, `PricingService`, `quote`; `SeatHold` (bazada), `seatmap/status`, UTC çeviricisi, `UnitOfWork`-da unikal pozuntusu tərcüməsi, hold təmizlik xidməti.
- **Gün 15 (TAMAMLANDI):** `PnrGenerator` (32 simvollu əlifba), `CheckoutCommand` + validator, kart datasının maskalanması. Testlər: 298 (233 unit + 65 inteqrasiya).
- **Gün 16-18 (TAMAMLANDI, düzəlişlər gözləyir):** checkout mühərriki, ödəniş simulyasiyası, PNR axtarışı, "my trips" və ləğv. **Məlum qüsur (Gün 19-un əvvəlində düzəlir):** hold ödənişdən **əvvəl** claim olunmur (eyni istifadəçinin ikiqat göndərməsi kartdan iki dəfə pul çıxara bilər), `ReverseAsync` kompensasiyası yoxdur, `UniqueConstraintViolationException` indeks adını daşımır, hold və sərnişin yerlərinin bərabərliyi yoxlanmır. Gün 17-18 hələ yoxlama kapısından keçməyib. Testlər: 387 (306 unit + 81 inteqrasiya).
- Köhnə plana görə **erkən yazılıb** (Gün 9-da yoxlanılır, Gün 13-də təmizlənir): oturacaq xəritəsi `GET /api/v1/flights/{id}/seatmap` (strukturlu JSON) və server tərəfli SVG köməkçi endpoint-i.

## 2. Dəyişməz qərarlar

| Mövzu | Qərar |
|---|---|
| Oturacaq statusu | `Occupied` = həmin uçuş üçün **aktiv bilet** (`Ticket.IsCancelled = false`, `IsDeleted = false`, `Booking.Status` Cancelled deyil). `Held` = vaxtı bitməmiş `SeatHold`. `Blocked` = admin bloku (Gün 31). `FlightSeat` **yoxdur**. Qoruma: `Tickets (FlightId, SeatId)` filtrli unikal indeks |
| Ləğv | `Ticket.IsCancelled`, `Booking.Status = Cancelled`. Biznes ləğvi üçün `IsDeleted` işlədilmir |
| Hold (10 dəq) | **Bazada `SeatHold` cədvəli:** `(FlightId, SeatId, UserId, ExpiresAtUtc)`, `(FlightId, SeatId)` üzərində unikal indeks, fiziki silinir (soft delete yoxdur). Yerlər "Davam et" düyməsində **atomik** (hamısı və ya heç biri) kilidlənir, ≤ 9, uzatma yoxdur. Yaddaş (`ConcurrentDictionary`) kilidi **istifadə olunmur** |
| **Hold qaydası** | Oturacağı kilidləməzdən və ya satmazdan əvvəl, **eyni tranzaksiyada**, həmin oturacağın vaxtı keçmiş hold-larını sil. Checkout hold-un **sahibini və vaxtını** yoxlayır, tranzaksiyada hold-ları silib biletləri yazır. Fon xidməti yalnız təmizlikdir, təhlükəsizlik mexanizmi deyil |
| Checkout | **Tək addım:** `POST /api/v1/bookings/checkout` (hold + sərnişinlər + kart). Tranzaksiya: hold yoxla → ödəniş simulyasiyası → `Booking (Confirmed)` + biletlər + `Payment`. Ödəniş rədd olsa rezervasiya yaranmır, hold qalır. `Booking.ExpiresAtUtc` və `BookingStatus.Pending/Expired` bu axında **istifadə olunmur** (Gün 36-da silinir) |
| Rezervasiya | Yalnız **giriş etmiş** istifadəçi (`Booking.UserId` məcburi `Guid`). Qonaq bron edə bilməz, qeydiyyata yönləndirilir. PNR + soyad yalnız **baxmaq** üçündür |
| İstifadəçi | `AppUser : IdentityUser<Guid>`, rollar: `Admin`, `Customer` (yalnız ikisi) |
| Admin parolu | Kodda yoxdur, `user-secrets` və ya mühit dəyişəni |
| Vaxt | Bazada UTC. `IBusinessTime` + `Business:TimeZone = Asia/Baku`, başlanğıcda fail-fast. `DateTime.UtcNow/Now` qadağandır |
| Cavab formatı | Hər endpoint `ApiResponse<T>` qaytarır |
| Marşrutlar | Ayrıca `Route` entity-si **yoxdur**. Marşrut = (çıxış, təyinat) cütü, admin-də uçuşlardan qruplanmış görünür |
| Aviaşirkət adı | "Azerbaijan Airlines (AZAL)", kod `J2` |
| Dil | UI azərbaycanca; kod, API, commit mesajları ingiliscə |
| Pul | `decimal(18,2)`, AZN. Qiyməti yalnız server hesablayır |
| Kart datası | Heç vaxt saxlanılmır və loglanmır |

## 3. Əhatə

**Daxildir:** F1-F8 (tam), səs tanıma və səsləndirmə, interaktiv SVG oturacaq xəritəsi, PDF boarding pass + QR, admin KPI qrafikləri, ləğv və qaytarma siyasəti, hold, testlər, Scalar, README, slaydlar, müdafiə hazırlığı.

**Daxil deyil (vaxt itkisi):** parol sıfırlama, email təsdiqi, saxlanmış sənədlər, 3D Secure, faktura, loyallıq xalları, 3 səviyyəli RBAC (SuperAdmin/Operator), Redis, DDoS/IP limit infrastrukturu (yalnız login üçün daxili rate limiter), audit logging, APM monitorinq, Docker, deployment, real email göndərmə, "Guest checkout".

## 4. Funksiya və yaradıcılıq xəritəsi

| Kod | Feature | Gün (backend / frontend) |
|---|---|---|
| F1 | Axtarış (çıxış, təyinat, tarix, sərnişin sayı) | 8-10 / 21 |
| F2 | Səsli giriş (Web Speech API) | 25-26 |
| F3 | Uçuş siyahısı + filtr (aviaşirkət, qiymət, dayanacaq, müddət) | 8-9 / 21 |
| F4 | Interaktiv oturacaq xəritəsi | 13-14 / 22 |
| F5 | Rezervasiya (sərnişin və əlaqə məlumatı) | 15-17 / 23 |
| F6 | PNR təsdiqi | 15-17 / 23 |
| F7 | My Trips (gələcək və keçmiş), ləğv | 18 / 24 |
| F8 | Admin: uçuşlar, marşrutlar, cədvəl, oturacaq inventarı | 28-33 |
| Yaradıcılıq | Oturacaq kilidi (hold + geri sayım), TTS, PDF + QR, KPI qrafikləri, qaytarma siyasəti, SVG xəritə + canlı qiymət, (seçim) canlı izləmə | 14, 22 / 26, 33, 34, 35 |

---

## 5. Gündəlik plan

Format: **Branch**, **Tapşırıqlar**, **Commit-lər**, **Bitmə meyarı**. Hər gün `plan → təsdiq → addım-addım`, hər addımdan sonra `dotnet build` və `dotnet test`. Hər məntiqi işdən sonra atomik commit.

### MƏRHƏLƏ A: Backend-in tamamlanması (Gün 9-19)

### Gün 9: Axtarış sorğusunun düzəldilməsi, inteqrasiya test bazası və Scalar (TAMAMLANDI)
**Branch:** `fix/flight-search-polish`
**Nəticə:** commit-lər `cad120d`, `4f1bf77`, `a3c7ce3`, `5fafc5d`; 62 test yaşıl. Qalan 2 kiçik iş Gün 10-a keçdi.
**Yoxlama nəticəsi (görülüb):** `8d6882a` `develop`-dadır; Bakı günü sərhədi düzgündür (`startUtc` 20:00 UTC); tək SQL komandası; pasport `Trim()` və `static readonly Regex` düzgündür; `AirlineId` və `MaxStops` filtrləri var.
**Düzəldilməli (SQL logundan görünən):**
- [x] **Sərnişin sayı filtri SQL-də olmalıdır.** İndi səhifələmədən sonra yaddaşda süzülür (səhifələr və say səhvdir). Boş yer sayı SQL-də hesablanır (`Aircraft.TotalSeats` − aktiv biletlər) və `>= passengersCount` şərti `OFFSET`-dən **əvvəl** tətbiq olunur
- [x] **Sorğu ağırdır:** hər reysin bütün bilet və rezervasiyaları yüklənir (`Include`). `Select` proyeksiyası + `AsNoTracking`, boş yer sayı alt sorğu ilə, yalnız DTO üçün lazım olan sütunlar
- [x] **Satılabilən statuslar:** `Scheduled` və `Delayed` (indi yalnız `Scheduled`, gecikmiş reyslər çıxır). `Cancelled`, `Departed`, `InAir`, `Landed` çıxarılır
- [x] **`TotalCount`:** `PagedResult` üçün ayrıca `CountAsync` (eyni filtrlərlə), yaddaşdakı səhifədən hesablama yoxdur
- [x] **Fail-fast həqiqətən işləməlidir:** `ResolveTimeZone` tapılmayanda səssiz UTC qaytarırdı. Başlanğıc üçün `GetRequiredTimeZone` (tapılmasa `InvalidOperationException`). Səhv ad (`Asia/Bakuu`) ilə sınaqdan keçir, tətbiq dayanmalıdır
- [x] `PageSize` maksimumu 50
- [x] Gün saatı (səhər/günorta/axşam) filtri **planda yoxdur** (F3 tələb etmir). Çeşidləmə: qiymət, çıxış vaxtı, müddət
- [x] **İnteqrasiya test bazası:** `tests/AirlineReservation.IntegrationTests` (real SQL Server, `AirlineReservationDb_Test`, migration ilə yaradılır, testlər öz datasını yaradır və təmizləyir). İlk testlər: 1 boş yeri qalan reys 2 sərnişin üçün çıxmır; səhifə ölçüsü və `TotalCount` düzgündür; `Delayed` reys görünür, `Cancelled` görünmür. Bu infrastruktur Gün 14 və 16-da da istifadə olunacaq
- [x] **Scalar:** daxili OpenAPI + `Scalar.AspNetCore`, XML şərhləri, mövcud endpoint-lərdə (flights, seatmap) `ProducesResponseType<ApiResponse<T>>`
**Commit-lər:** `perf(flights): count available seats in sql and project search results`, `fix(flights): apply passenger count filter before paging`, `fix(flights): keep delayed flights searchable`, `fix(flights): return total count with paged results`, `fix(api): fail fast on unknown business timezone`, `test(flights): add sql server integration tests for search`, `feat(api): add openapi and scalar documentation`
**Bitmə meyarı:** axtarış bir proyeksiya sorğusu və bir `COUNT` ilə işləyir, səhifələmə və say düzgündür, səhv zona adı ilə tətbiq başlamır, Scalar `/scalar`-da açılır.

### Gün 10: Aeroport və aviaşirkət API, alias, reseed (TAMAMLANDI)
**Branch:** `feat/airports-airlines-api`
- [x] **Gün 9-dan qalan:** hər sıralamaya unikal tie-break (`ThenBy(f => f.Id)`: çıxış vaxtı, qiymət, müddət). "Aktiv bilet" qaydası **tək bir ifadədir** (silinməyib, `IsCancelled` deyil, rezervasiya `Cancelled` deyil), axtarış və oturacaq xəritəsi eyni ifadəni istifadə edir. Test: eyni reys üçün ikisinin boş yer sayı eynidir
- [x] `Airport.Aliases` (vergüllə ayrılmış) + migration (alətlə). Seed alias-ları ASCII variantlarla: GYD `Baku,Bakı,Baki,Баку`, IST `Istanbul,İstanbul,Istanbul Airport,Стамбул`, SAW `Istanbul,İstanbul,Sabiha Gökçen,Sabiha Gokcen,Стамбул`, DXB `Dubai,Dubay,Дубай`, JFK `New York,Nyu-York,Нью-Йорк`, LHR `London,Londan,Лондон`, FRA `Frankfurt,Франкфурт`. Kollasiyaya güvənmə, `ə` və `ı` alias-larda açıq yazılır
- [x] Aviaşirkət adları: "Azerbaijan Airlines (AZAL)", "Turkish Airlines", "Emirates", "Lufthansa". Dev bazasını sıfırla (`dotnet ef database drop -f`) və yenidən seed et
- [x] `GET /api/v1/airports?search=` (IATA, ad, şəhər, alias; hərf registrinə həssas deyil; max 10; boş axtarış şəhər üzrə ilk 10). CQRS query, validator (uzunluq ≤ 50), `ApiResponse<List<AirportDto>>`. `GET /api/v1/airlines`. XML şərhləri və `ProducesResponseType`
- [x] Testlər: handler və validator unit testləri; inteqrasiya testləri (`Bakı`, `baku`, `Баку`, `GYD`, `ist`, `Sabiha` düzgün aeroportu tapır, tanımayan mətn boş siyahı qaytarır, xəta yox)
**Commit-lər:** `fix(flights): add deterministic tie-break to flight ordering`, `refactor(flights): share active ticket rule between search and seat map`, `feat(airports): add airport aliases with migration and seed`, `feat(airports): add airport search endpoint`, `feat(airlines): add airlines endpoint`, `test(airports): cover alias search`
**Bitmə meyarı:** autocomplete `Bakı`, `Баку`, `ist` üçün düzgün aeroportları qaytarır, axtarış və xəritə eyni boş yer sayını göstərir.

### Gün 11: ASP.NET Core Identity (TAMAMLANDI)
**Branch:** `feat/identity-setup`
- [x] `AppUser : IdentityUser<Guid>`, `AppRole`, rollar `Admin` və `Customer` (Infrastructure, Domain Identity-ni tanımır)
- [x] Admin hesabı seed: **parol yalnız `user-secrets`/mühit dəyişənindən** (`Seed:AdminPassword`), `appsettings`-də yoxdur; parol yoxdursa xəbərdarlıq və atlama (default parol yoxdur). E-poçt qeyri-məxfi konfiqurasiyadan, ayrılmış test domeni (`admin@airline.test`), `EmailConfirmed = true`. README-də "yalnız demo" və parol nümunəsi yoxdur
- [x] İnteqrasiya test fabriki admin e-poçtunu və test parolunu **özü konfiqurasiya override ilə verir** (`user-secrets`-ə güvənmir, test təkrarlanandır)
- [x] `Booking.UserId` **məcburi `Guid`** (nullable deyil, qonaq rezervasiyası yoxdur) + Infrastructure konfiqurasiyasında `AspNetUsers`-ə FK (`Restrict`) və indeks. Əgər əvvəl nullable yazılıbsa, ayrıca yeni migration ilə düzəlt (köhnə migration-a toxunma)
- [x] Migration (alətlə), `has-pending-model-changes` exit 0
**Commit-lər:** `feat(infra): add identity user and roles`, `feat(infra): seed admin from user secrets`, `feat(infra): link booking user to identity`
**Bitmə meyarı:** Identity cədvəlləri bazadadır, admin hesabı secret-dən yaranır.

### Gün 12: JWT və `AuthController` (TAMAMLANDI)
**Branch:** `feat/jwt-auth`
- [x] Identity `AddIdentityCore<AppUser>().AddRoles<AppRole>().AddEntityFrameworkStores<AppDbContext>()` ilə (**cookie autentifikasiya sxemi yoxdur**). Default sxem JWT Bearer, tokensiz sorğu **401** qaytarır (302 yönləndirmə yox). Test ilə sübut et
- [x] `ITokenService` / `JwtTokenService` (claim-lər: Id, email, rol), açar `user-secrets`-də
- [x] `register` (rol `Customer`), `login`, `me`; parol siyasəti; validatorlar
- [x] `[Authorize]` və `[Authorize(Roles = "Admin")]`; Scalar-da Bearer "Authorize"
- [x] Yalnız login üçün daxili rate limiter (`AddRateLimiter`)
- [x] Testlər: token claim-ləri, səhv parol, rol yoxlaması. **Saat:** token verilməsi və yoxlanması **eyni `TimeProvider`-i** istifadə edir (dondurulmuş test saatı ilə real saat qarışmır), vaxtı bitmiş token 401 verir. Testlər real saata və sabit təqvim tarixinə bağlı olmamalıdır
**Commit-lər:** `feat(auth): add jwt token service`, `feat(api): add register login and me endpoints`, `feat(api): add login rate limiter`, `test(auth): cover token and role checks`
**Bitmə meyarı:** Scalar-da qeydiyyat, giriş, token ilə `me` işləyir, qorunan endpoint tokensiz 401 verir.

### Gün 13: Oturacaq xəritəsi müqaviləsinin təmizlənməsi, qiymət və quote (TAMAMLANDI)
**Branch:** `feat/seatmap-contract-and-pricing`
`GET /api/v1/flights/{id}/seatmap` artıq var (köhnə Gün 9). Bu gün onu **kilid və blokla uyğun** edirik.
- [x] `SeatMapDto` müqaviləsi: `bool IsOccupied` → `Status` (`Available`, `Occupied`, `Held`, `Blocked`, JSON-da mətn kimi, `JsonStringEnumConverter`) və `heldByMe` (token varsa). **`SvgMap` əsas cavabdan çıxarılır** (hər sorğuda yüzlərlə oturacağın SVG sətri göndərmək lazımsızdır; server tərəfli SVG ayrıca endpoint kimi qala bilər, Client xəritəni JSON-dan çəkir). `FlightSeatDto` adı `SeatDto` olur (`FlightSeat` entity-si yoxdur)
- [x] Doluluq sorğusu yalnız aktiv biletləri sayır (ləğv, silinmiş, `Cancelled` rezervasiya çıxarılır, bu hissə var). Bütün uçuşu bilet və rezervasiyalarla yükləmək əvəzinə **`Select` proyeksiyası** ilə tək sorğu
- [x] `PricingService`: `BasePrice × Seat.PriceMultiplier` (business, exit row), pəncərə/keçid əlavə haqqı (konfiqurasiya), son 3 gündə tələbat çarpanı (default ×1.2). Xəritədəki `Price` **yalnız bu servisdən** gəlir (iki ayrı qiymət məntiqi olmasın)
- [x] `POST /api/v1/bookings/quote` (flightId, seatIds) → sətir-sətir döküm və yekun
- [x] Testlər: xəritə statusları, table-driven qiymət testləri (economy, business, exit row, pəncərə, tələbat, yuvarlaqlaşdırma)
**Commit-lər:** `refactor(seats): replace occupied flag with seat status and drop svg from main dto`, `perf(seats): project seat map in a single query`, `feat(pricing): add pricing service with configurable fees`, `feat(bookings): add quote endpoint`, `test(pricing): add table driven pricing tests`
**Bitmə meyarı:** 777 üçün 3-4-3 xəritə `Status` ilə qayıdır, quote düzgün rəqəmlər verir.

### Gün 14: Oturacaq kilidi (`SeatHold`, bazada) (TAMAMLANDI)
**Branch:** `feat/seat-hold`
- [x] `SeatHold` entity (`FlightId`, `SeatId`, `UserId`, `ExpiresAtUtc`, `CreatedAtUtc`), `BaseEntity`-dən miras almır, **soft delete yoxdur** (fiziki silinir; qlobal soft-delete filtri dövrü onu keçir). `(FlightId, SeatId)` üzərində **filtrsiz unikal indeks**, `(FlightId, UserId)` kompozit və `ExpiresAtUtc` indeksləri. FK-lar `Flight`, `Seat`, `AspNetUsers` (hamısı `Restrict`). Migration (alətlə), `has-pending-model-changes` exit 0
- [x] **UTC işarəsi:** EF-də bütün `DateTime` sahələri üçün UTC `ValueConverter` (`ConfigureConventions`), JSON-da vaxtlar `Z` ilə bitir (indi `Unspecified` olduğu üçün `Z` ola bilmir və brauzer yerli vaxt kimi oxuyur). Test: xam JSON-da `departureTimeUtc` və `expiresAtUtc` `Z` ilə bitir
- [x] **Unikal pozuntusunun tərcüməsi:** `UnitOfWork` `SqlException` 2601/2627-ni Application-da təyin olunmuş `UniqueConstraintViolationException`-a çevirir (Application `SqlClient`-i tanımır), handler mənalı `ConflictException` atır. Hər `DbUpdateException` 409 olmur
- [x] `POST /api/v1/flights/{id}/holds` `[Authorize]`, body `{ seatIds: [] }`, **atomik** (hamısı və ya heç biri), ≤ 9 yer. Tranzaksiyada: (1) bu istifadəçinin həmin reysdəki köhnə hold-larını burax, (2) seçilmiş yerlərin **vaxtı keçmiş hold-larını sil** (Hold qaydası), (3) reysin satılabilən olduğunu (`Scheduled`/`Delayed`, qalxış gələcəkdə) və yerlərin həmin təyyarəyə aid olduğunu, aktiv biletlə dolu və ya bloklu olmadığını yoxla, (4) yaz. **Bir istifadəçinin eyni anda yalnız bir reysdə hold-u olur** (yeni reysdə hold alanda köhnə reysin hold-ları buraxılır). Konfliktdə tranzaksiya geri çəkilir, istifadəçinin köhnə hold-ları qalır Unikal indeks pozuntusu → `ConflictException` (409). `ExpiresAtUtc = TimeProvider + Hold:Minutes` (default 10), **uzatma yoxdur**
- [x] `GET /api/v1/flights/{id}/holds/mine` (`expiresAtUtc` və **`remainingSeconds`**, serverin saatı ilə), `DELETE /api/v1/flights/{id}/holds` (yalnız **öz** hold-larını burax, hold olmayanda da uğurlu)
- [x] Xəritə: başqasının aktiv hold-u `Held`, öz hold-u `Held` + `heldByMe = true`
- [x] **Polling üçün yüngül endpoint:** `GET /api/v1/flights/{id}/seatmap/status` yalnız `Held`/`Occupied`/`Blocked` yerlərin `seatId` və statusunu qaytarır. Client tam xəritəni **bir dəfə** yükləyir, sonra yalnız statusu yeniləyir (66 KB-lıq tam cavab hər 8 saniyədən bir getməsin)
- [x] `HoldCleanupService` (`BackgroundService`, hər 30 san, `ExecuteDeleteAsync`, `TimeProvider`, hər dövr üçün scope, **hər iterasiya `try/catch`** ki, istisna tətbiqi dayandırmasın, interval və `Hold:CleanupEnabled` konfiqurasiyada, test fabrikində söndürülür)
- [x] **Status qaydası:** prioritet `Blocked` > `Occupied` > `Held` > `Available`. Tam xəritə və `seatmap/status` **eyni sorğu köməkçisini** istifadə edir. Status endpoint-i yalnız `Available` olmayanları qaytarır və `generatedAtUtc` əlavə edir. Etibarsız/vaxtı keçmiş tokenlə ictimai `/seatmap` 401 yox, **200** qaytarır (`heldByMe = false`). Axtarışdakı boş yer sayı = xəritədə `Available + Held`
- [x] Testlər (hər addımla birlikdə): iki istifadəçi eyni yeri kilidləyir (biri 409), vaxtı bitmiş hold götürülür (**dəyişdirilə bilən** saat, JWT-nin istifadə etdiyi saatla eyni), 9-dan çox yer rədd, konfliktdə köhnə hold-lar qalır (rollback), başqa təyyarənin yeri 400, aktiv biletli yer 409, öz hold-unu yenidən seçmək əvəz edir, `DELETE` başqasının hold-una toxunmur, xəritə və status endpoint-i eyni statusları göstərir. **Paralel inteqrasiya testi** (real SQL Server test bazası): başlanğıc darvazası ilə eyni anda, 20 dəfə təkrar, sonda bazada həmin yer üçün dəqiq bir sətir. `ClearTestDataAsync`-ə `SeatHolds` əlavə olunur
- [x] Scalar: 4 endpoint üçün XML təsvirləri, `401/409` cavab tipləri
**Commit-lər:** `feat(domain): add seat hold entity`, `feat(infra): add seat hold configuration and migration`, `feat(seats): add atomic multi seat hold command`, `feat(seats): add release and my holds endpoints`, `feat(seats): add hold cleanup background service`, `test(seats): cover hold conflicts expiry and parallel access`
**Bitmə meyarı:** iki hesabla eyni yeri kilidləməyə çalışanda biri 409 alır, vaxt bitəndə yer azad olur, restart kilidləri silmir.

### Gün 15: PNR və checkout validatoru (TAMAMLANDI)
**Branch:** `feat/pnr-and-checkout-validation`
- [x] `IPnrGenerator` + `PnrGenerator`: `RandomNumberGenerator` (ədalətli seçim, `RandomNumberGenerator.GetItems`, modulo qərəzi yoxdur), 6 simvol, 32 simvollu əlifba: hərflər `I` və `O` olmadan, rəqəmlər `2-9` (`0` və `1` yoxdur). Generator saf funksiyadır. **Unikal pozuntusunda təkrar cəhd Gün 16-dadır** (checkout handler-i, `UniqueConstraintViolationException`)
- [x] `CheckoutCommand` forması: `FlightId`, `Passengers[]` (hər birində `SeatId`), əlaqə (`email`, `phone`), `Card` (`number`, `expiry` MM/YY, `cvv`). `UserId` sorğu gövdəsində **yoxdur**, tokendən gəlir
- [x] `CheckoutCommandValidator`: sərnişin sayı 1-9, hər sərnişin `PassengerValidator`-dan keçir (Azərbaycan adları, pasport), `SeatId`-lər təkrarlanmır, əlaqə formatları, kart formatı (nömrə uzunluğu və rəqəmlər, `expiry` formatı və bu aydan əvvəl deyil (`IBusinessTime`), `cvv` 3-4 rəqəm). Luhn Gün 17-dədir. Hold və reys yoxlamaları (verilənlər bazası) handler-dədir (Gün 16)
- [x] **Kart datası təhlükəsizliyi:** kart modelinin `ToString()` və log çıxışı maskalanır; validasiya xəta mesajları daxil edilən dəyəri **təkrarlamır**; global exception handler sorğu gövdəsini loglamır. Test: `command.ToString()` kart nömrəsini və CVV-ni ehtiva etmir
- [x] Testlər (addımla birlikdə): PNR formatı (uzunluq, icazəli simvollar, qadağan simvollar yoxdur, 100 000 nümunə), validator halları (Azərbaycan adları daxil)
**Commit-lər:** `feat(bookings): add pnr generator`, `feat(bookings): add checkout command with validator`, `test(bookings): cover pnr and checkout validation`
**Bitmə meyarı:** testlər yaşıldır, kart datası `ToString()`-də görünmür.

### Gün 16: Checkout mühərriki (ən kritik gün) (TAMAMLANDI, düzəlişlər Gün 19-da)
**Branch:** `feat/checkout-engine`
- [ ] `IPaymentGateway` (`ChargeAsync`, `ReverseAsync`) + müvəqqəti həmişə-uğurlu `StubPaymentGateway` (real simulyasiya Gün 17-dədir). `PaymentProcessRequest`-in `ToString()` və log çıxışı kart datasını maskalayır
- [ ] `CheckoutCommand` handler axını, **hamısı bir `IUnitOfWork` tranzaksiyasında**: (1) reys satılabilən olmalıdır (404/409); (2) istifadəçinin bu reysdəki **bütün** hold-larını yüklə (vaxtı bitmişlər də, düzgün xəta mesajı üçün); sərnişinlərin `SeatId` çoxluğu hold çoxluğuna **bərabər olmalıdır** (əks halda 409); vaxtı bitmiş hold üçün ayrıca mesaj; (3) **hold-ları atomik "claim" et:** hold `Id`-ləri üzrə `ExecuteDeleteAsync`, silinən sətir sayı hold sayına bərabər olmalıdır (əks halda başqa sorğu artıq götürüb, 409, ödəniş çağırılmır). Eyni istifadəçinin iki dəfə basması beləcə ikiqat ödənişə yol vermir; (4) yerlərin aktiv biletlə dolu olmadığını **`TicketSpecifications.IsActive`** ilə yoxla (öz `IsCancelled` yoxlamanı yazma); (5) qiyməti `PricingService`-dən hesabla (müştərinin göndərdiyinə etibar yox); (6) PNR: `IPnrGenerator` ilə, `AnyAsync` ilə yoxlayaraq ən çox 5 cəhd (**ödənişdən əvvəl**); (7) `ChargeAsync`, rədd olarsa tranzaksiya geri çəkilir (claim də geri çəkilir, hold-lar qayıdır); (8) `Booking (Confirmed)`, `Passenger`, `Ticket`, `Payment` yaz, commit
- [ ] **Kompensasiya:** ödəniş uğurlu olub sonrakı addım (yazma, commit) uğursuz olarsa `ReverseAsync` çağırılır (best effort, loglanır, kart datası yoxdur). `UniqueConstraintViolationException` indeksin **adını** daşıyır: PNR indeksi → 409 "yenidən cəhd et", bilet indeksi → 409 "oturacaq tutulub" (ikisində də kompensasiya). Deadlock (1205) bir dəfə təkrar cəhd olunur
- [ ] `TicketNumber` generasiyası: `{pnr}-{sıra}` (PNR unikal olduğu üçün unikaldır)
- [ ] `CheckoutCommand`-a isteğe bağlı `ExpectedTotalAmount`: göndərilibsə və serverin hesabladığından fərqlidirsə **409 `PriceChanged`** (yeni yekunla), ödəniş çağırılmır. İstifadəçi görmədiyi məbləği ödəmir
- [ ] `CheckoutResultDto` (pnr, rezervasiya id, yekun, biletlər, reys xülasəsi; kart datası yoxdur). `POST /api/v1/bookings/checkout` `[Authorize]`, uğurlu cavab **201**. `UserId` tokendən gəlir (`ClaimTypes.NameIdentifier`). XML + `ProducesResponseType` (201, 400, 401, 409)
- [ ] **400 cavabı daxil edilən dəyərləri təkrarlamır** (kart nömrəsi, CVV, `ValidationFailure.AttemptedValue`). Bütün mətn sahələrinin maksimum uzunluğu var (ad, soyad, pasport, email, telefon, kart sahibi)
- [ ] **İnteqrasiya testləri** (real SQL Server test bazası): hold yoxdur → 409; vaxtı bitmiş hold → 409; başqasının hold-u → 409; başqa reysdəki hold → 409; sərnişin yerləri ≠ hold yerləri → 409; uğurlu checkout (biletlər və PNR yaranır, hold-lar silinir, xəritədə `Occupied`, axtarışda boş yer azalır, bilet qiymətləri xəritə qiymətinə bərabərdir); **eyni istifadəçinin eyni anda iki dəfə göndərməsi** → bir rezervasiya və **bir ödəniş**; iki istifadəçi eyni yer (başlanğıc darvazası, **20 dövr**) → hər dövrdə bir 201 və bir 409; ödəniş rədd edən test gateway → heç nə yazılmır, hold-lar qalır; yazma uğursuz olanda `ReverseAsync` çağırılır; PNR toqquşması (saxta generator əvvəl mövcud PNR-i qaytarır) təkrar cəhdlə uğurlu olur; `ExpectedTotalAmount` uyğunsuzluğu; tokensiz 401; kart nömrəsi və CVV cavabda və log çıxışında yoxdur
**Commit-lər:** `feat(payments): add payment gateway abstraction and stub`, `feat(bookings): add checkout command consuming seat holds`, `fix(bookings): claim holds atomically before charging`, `feat(bookings): reverse payment when booking write fails`, `feat(api): add checkout endpoint`, `test(bookings): add checkout integration and concurrency tests`
**Bitmə meyarı:** Scalar-da hold → checkout → PNR işləyir, eyni istifadəçinin ikiqat göndərməsi yalnız bir ödəniş yaradır, paralel test 20 dövrdə keçir.

### Gün 17: Ödəniş simulyasiyası (TAMAMLANDI)
**Branch:** `feat/payment-simulation`
- [x] `MockPaymentGateway` (`ChargeAsync`, `ReverseAsync`; interfeys Gün 16-da yaranıb): kart nömrəsi (Luhn), bitmə tarixi (keçməyib), CVV formatı. Test kartları: `4111111111111111` uğurlu, `4000000000000002` rədd
- [x] Rədd: rezervasiya yaranmır, hold qalır, aydın xəta mesajı (`ApiResponse`). Uğurlu: `Payment` (məbləğ, status `Success`, saxta istinad kodu)
- [ ] **Kart datası saxlanılmır və loglanmır:** test logger provider ilə bütün log çıxışında kart nömrəsi və CVV axtarılır (tapılmamalıdır), 400/402 cavab gövdəsində də yoxdur, `Payment` cədvəlində kart sahəsi yoxdur
- [x] README qeydi: real sistemdə ödəniş xarici çağırışdır və tranzaksiyadan kənarda idarə olunur, burada simulyasiya olduğu üçün sadələşdirilib
- [x] Testlər: uğurlu, rədd, vaxtı keçmiş kart, hold bitdikdən sonra ödəniş cəhdi
**Commit-lər:** `feat(payments): add mock payment gateway with luhn check`, `feat(bookings): use payment gateway in checkout`, `test(payments): cover payment outcomes and card data safety`
**Bitmə meyarı:** rədd edilən kartla rezervasiya yaranmır, uğurlu kartla PNR alınır.

### Gün 18: Rezervasiya sorğuları və ləğv (TAMAMLANDI)
**Branch:** `feat/bookings-queries-cancel`
- [x] `GET /api/v1/bookings/pnr/{pnr}?lastName=` `[AllowAnonymous]` (yalnız baxış), `GET /api/v1/bookings/my?status=upcoming|past|cancelled` (səhifəli)
- [x] `CancellationPolicy`: uçuşdan >24 saat qalıb 100%, ≤24 saat 50%, uçuş başlayıbsa ləğv yoxdur. `Booking.RefundAmount`, `CancelledAtUtc` + migration
- [x] `GET .../cancel-preview`, `POST .../cancel` (sahiblik yoxlaması: yalnız öz rezervasiyası; admin hamısı). Ləğv: `Booking = Cancelled`, biletlər `IsCancelled`, `Payment = Refunded`
- [x] Testlər: siyasət sərhədləri (tam 24 saat), başqasının rezervasiyasını ləğv cəhdi (403)
**Commit-lər:** `feat(bookings): add pnr lookup and my trips queries`, `feat(bookings): add cancellation policy with refund amount`, `feat(bookings): add cancel and cancel preview endpoints`, `test(bookings): cover cancellation boundaries and ownership`
**Bitmə meyarı:** ləğvdən sonra oturacaq xəritədə boşdur, qaytarma məbləği düzgündür.

### Gün 19: Gün 16-18 düzəlişləri, backend inteqrasiyası (bufer) və `v0.1-backend` (TAMAMLANDI)
**Branch:** əvvəl `fix/checkout-atomic-claim`, sonra `chore/backend-integration`
**A. Məcburi düzəlişlər (Gün 16-nın Bölməsinə bax, testdən başla):**
- [x] **Qırmızı test əvvəl:** eyni istifadəçi eyni anda iki dəfə checkout göndərir, test gateway `ChargeAsync` çağırışlarını sayır. Hazırkı kodda **2 çağırış** olmalıdır (qüsurun sübutu), sonra düzəliş edilir və test **1** göstərir
- [x] Hold-ları ödənişdən əvvəl **atomik claim et** (`ExecuteDeleteAsync`, silinən say hold sayına bərabər olmalıdır, yoxsa 409 və ödəniş çağırılmır). Bütün hold-lar (vaxtı bitmişlər də) yüklənir, sərnişin yerləri hold yerlərinə bərabər olmalıdır
- [x] `TicketSpecifications.IsActive` ilə dolu yer yoxlaması, PNR ödənişdən əvvəl (`AnyAsync`, ≤ 5 cəhd)
- [x] `UniqueConstraintViolationException.IndexName` (Infrastructure `SqlException` mesajından çıxarır): PNR indeksi və bilet indeksi ayrı-ayrı işlənir
- [x] `IPaymentGateway.ReverseAsync` + kompensasiya (ödəniş uğurlu, sonrakı addım uğursuz), `PaymentProcessRequest.ToString()` maskalı
- [x] İsteğe bağlı `ExpectedTotalAmount` (409 `PriceChanged`), `TicketNumber = {pnr}-{sıra}`, uğurlu cavab 201
- [x] Testlər: ikiqat göndərmə (bir rezervasiya, **bir** ödəniş), paralel iki istifadəçi **20 dövr**, rədd edən gateway hold-ları saxlayır, yazma uğursuz olanda `ReverseAsync`, PNR toqquşması (saxta generator), sərnişin yerləri ≠ hold yerləri, başqa reysdəki hold
**B. Gün 17-18 yoxlama kapısı (oxu rejimi, sonra düzəliş):**
- [x] Ləğv **atomikdir və təkrarlanmır:** iki eyni vaxtlı ləğv sorğusu iki dəfə qaytarma yaratmır (status keçidi şərtli yeniləmə ilə, silinən/yenilənən sətir sayı yoxlanır). Test: paralel iki ləğv, bir qaytarma
- [x] PNR axtarışı (`AllowAnonymous`): rate limit və yanlış PNR ilə yanlış soyad üçün **eyni** 404 mesajı (məlumat sızmır)
- [x] `MockPaymentGateway`: hansı kartlar uğurlu/rədd, `Payment` cədvəlində kart sahəsi yoxdur, log çıxışında kart datası yoxdur (test)
- [x] Ləğv siyasəti sərhədləri (tam 24 saat, qalxmış reys), başqasının rezervasiyası 403/404
**C. İnteqrasiya (bufer):**
- [x] Scalar-da tam ssenari: qeydiyyat → axtarış → xəritə → quote → hold → checkout → my → ləğv
- [x] Bug-lar, çatışmayan testlər, `has-pending-model-changes` exit 0, CORS (`AllowClient` Client origin-i ilə)
- [x] README: backend işə salma (SQL Server, migration, user-secrets, seed)
- [x] `AGENT_RULES.md` və bu plan real vəziyyətə uyğunlaşdırılır (Hold qaydası, tək addımlı checkout, claim)
- [x] `develop` → `main` merge, tag `v0.1-backend` (**yalnız A və B bitəndən sonra**)
**Bitmə meyarı:** ikiqat göndərmə yalnız bir ödəniş yaradır, ikiqat ləğv yalnız bir qaytarma yaradır, bütün backend axını Scalar-da sınaqdan keçib.

### MƏRHƏLƏ B: İstifadəçi saytı (Gün 20-27)

### Gün 20: Client skeleti və dizayn dili
**Branch:** `feat/client-foundation` | **Skill:** taste-skill (yalnız Client)
- [ ] Client fixed portla qalxır (Live Server `5500` və ya oxşar), CORS origin eynidir
- [ ] **Dizayn dili:** brief ver (booking saytı, mobil birinci, Azərbaycan hərfləri, əlçatanlıq, yalnız HTML/CSS/JS), nəticə `docs/DESIGN.md` və `css/tokens.css`. Agent-ə: "React/Tailwind/GSAP/yeni kitabxana yoxdur, davranışı dəyişmə"
- [ ] `js/api/http.js` (`ApiResponse` zərfini açır, Bearer, 401 → login, xətaları göstərir), `auth.js`, `navbar`, `toast`, `modal`
- [ ] `login.html`, `register.html` (client validasiya)
**Commit-lər:** `docs(design): add design language`, `feat(client): add http client with api response handling`, `feat(client): add login and register pages`
**Bitmə meyarı:** qeydiyyat və giriş Client-dən işləyir.

### Gün 21: Axtarış və nəticələr (F1, F3)
**Branch:** `feat/client-search-results`
- [ ] `index.html`: aeroport autocomplete (debounce), tarix (min = bu gün), sərnişin stepper (1-9), "yerini dəyiş"
- [ ] `flights.html`: URL parametrlərindən sorğu, reys kartları (aviaşirkət, Bakı vaxtı ilə saatlar, müddət, dayanacaq, qiymət, boş yer), **server tərəfli** filtr (qiymət aralığı, dayanacaq, aviaşirkət) və çeşidləmə, səhifələmə
- [ ] Yüklənir, boş, xəta halları
**Commit-lər:** `feat(client): add search form with autocomplete`, `feat(client): add flight results with server side filters`, `feat(client): add sorting and paging`
**Bitmə meyarı:** Bakı → İstanbul axtarışı filtr və çeşidləmə ilə işləyir.

### Gün 22: Oturacaq xəritəsi və kilid (F4)
**Branch:** `feat/client-seat-map`
- [ ] SVG/CSS Grid xəritə JSON-dan çəkilir: sinif bölmələri, cərgə nömrələri, keçid, legend; statuslar (`Available`, `Occupied`, `Held`, `Blocked`) rəngə tək bağlı deyil (ikon/naxış)
- [ ] Seçim limiti = sərnişin sayı; canlı qiymət (`quote`), sticky xülasə
- [ ] **"Davam et"** düyməsi `POST .../holds` çağırır (bütün seçilmiş yerlər atomik). 409 olsa "bu yer artıq tutulub" göstərir və xəritəni yeniləyir. Uğurlu olanda **10:00 geri sayım** serverin qaytardığı **`remainingSeconds`**-dan başlayır (müştəri saatından asılı deyil)
- [ ] Xəritə 8 saniyədən bir yenilənir (başqalarının hold-ları görünür), səkmə gizli olanda dayanır
- [ ] "Seçimi dəyiş" → `DELETE .../holds`
- [ ] Klaviatura və `aria` dəstəyi, mobil üfüqi sürüşdürmə
**Commit-lər:** `feat(client): render interactive seat map from json`, `feat(client): add seat selection with live price`, `feat(client): add seat hold with countdown`, `feat(client): poll seat map for held seats`, `feat(client): improve seat map accessibility`
**Bitmə meyarı:** iki brauzerdə eyni yeri seçəndə ikincisi 409 alır və xəritə yenilənir.

### Gün 23: Checkout, ödəniş və PNR təsdiqi (F5, F6)
**Branch:** `feat/client-checkout`
- [ ] Sərnişin forması (ad `\p{L}` qaydası ilə, pasport, doğum tarixi, əlaqə), `validators.js`
- [ ] Geri sayım serverin `remainingSeconds` dəyərindən davam edir (səhifə yenilənəndə `holds/mine` ilə yenidən alınır); bitəndə xəbərdarlıq və xəritəyə qayıdış
- [ ] Ödəniş (kart forması, Luhn), **tək `checkout` sorğusu**. Rədd: xəta göstərilir, hold qalır, yenidən cəhd olar. 409 "kilidin vaxtı bitib": xəritəyə qaytar
- [ ] Qiymət zamana bağlıdır (72 saat sərhədi). Checkout `ExpectedTotalAmount` göndərir. Server 409 `PriceChanged` qaytararsa yeni yekun göstərilir və istifadəçidən yenidən təsdiq istənir
- [ ] `confirmation.html`: böyük PNR, detallar, çap
- [ ] Giriş etməmiş istifadəçi login-ə yönləndirilir və geri qayıdır (`guard.js`)
**Commit-lər:** `feat(client): add passenger form with validation`, `feat(client): add checkout with payment form`, `feat(client): handle expired hold and declined payment`, `feat(client): add pnr confirmation page`, `feat(client): add auth guard with return url`
**Bitmə meyarı:** axtarışdan PNR-ə qədər tam axın işləyir.

### Gün 24: My Trips (F7)
**Branch:** `feat/client-my-trips`
- [ ] Tab-lar: gələcək, keçmiş, ləğv edilmiş; kartlar, detal modalı
- [ ] Ləğv axını: `cancel-preview` göstər, təsdiq al, `cancel`, siyahını yenilə
- [ ] PNR + soyad ilə axtarış qutusu (giriş tələb etməyən baxış)
**Commit-lər:** `feat(client): add my trips page with tabs`, `feat(client): add booking details and cancellation flow`, `feat(client): add pnr lookup`
**Bitmə meyarı:** bilet alıb görmək və ləğv etmək mümkündür.

### Gün 25: Səsli giriş (F2)
**Branch:** `feat/voice-input` | **Skill:** web-conventions
- [ ] `speechRecognition.js`: dəstək yoxlaması, dil seçimi (az-AZ, en-US, ru-RU), interim nəticə, xəta halları, "dinləyir" göstəricisi
- [ ] Mikrofon düymələri: mənşə, təyinat, tarix, sərnişin sayı
- [ ] `cityMatcher.js` (normallaşdırma, şəkilçi təmizləmə `-dan/-dən/-a/-ə/-ya/-yə`, API alias axtarışı, Levenshtein), `dateParser.js` ("bu gün", "sabah", "birisi gün", "15 noyabr"), `numberParser.js` ("iki nəfər")
- [ ] `sentenceParser.js`: "Bakıdan İstanbula sabah iki nəfər" → bütün sahələr; "Düzgündür?" təsdiq çipi
- [ ] Node testləri (`node --test`): ən azı 20 nümunə cümlə
**Commit-lər:** `feat(voice): add speech recognition wrapper`, `feat(voice): add city matcher with fuzzy match`, `feat(voice): add date and number parsers`, `feat(voice): add sentence parser`, `test(voice): add parser tests`
**Bitmə meyarı:** mikrofonla bütöv cümlə axtarış formunu doldurur.

### Gün 26: Səsləndirmə (TTS) və səs UX
**Branch:** `feat/voice-output`
- [ ] `speechSynthesis.js`: səslər asinxron yüklənir (`voiceschanged`), fallback `az → tr → en`, dayandır
- [ ] Nəticələr: "3 uçuş tapıldı, ən ucuzu 120 manatdır". Təsdiq: PNR-i hərf-hərf oxu
- [ ] "Səsli köməkçi" keçidi (`localStorage`, try/catch), dil seçimi
- [ ] **Demo kompüterində** `getVoices()` siyahısını yoxla, `az-AZ` yoxdursa fallback seç və dondur
**Commit-lər:** `feat(voice): add speech synthesis with fallback voices`, `feat(voice): announce search results`, `feat(voice): read pnr on confirmation`, `feat(client): add voice assistant toggle`
**Bitmə meyarı:** nəticələr və PNR səslə oxunur.

### Gün 27: Client bufer, cila və `v0.2-client`
**Branch:** `style/client-polish` | **Skill:** taste-skill (audit sonra düzəliş)
- [ ] `redesign-existing-projects` ilə **yalnız audit** (tapıntı siyahısı), sonra səhifə-səhifə düzəliş; funksiya sınmamalıdır
- [ ] 360/768/1280px, fokus halları, boş və xəta halları, favicon və başlıqlar
- [ ] Gecikmiş işlər burada bitir; `develop` → `main`, tag `v0.2-client`
**Bitmə meyarı:** istifadəçi saytı mobil və desktopda səliqəlidir.

### MƏRHƏLƏ C: Admin panel (Gün 28-33)

### Gün 28: Admin MVC skeleti
**Branch:** `feat/admin-foundation`
- [ ] Cookie autentifikasiyası, JWT cookie claim-ində; `JwtDelegatingHandler` hər `HttpClient` sorğusuna token əlavə edir; `AddHttpClient<T>()`
- [ ] `AdminOnly` siyasəti, login/logout, layout (Bootstrap 5), `TempData` bildirişləri, səhifələmə partial-ı, `ValidateAntiForgeryToken`, API xətalarını `ModelState`-ə çevirmə
- [ ] API-də `/api/v1/admin/*` üçün `[Authorize(Roles = "Admin")]`
- [ ] Admin `HttpClient`-inin JSON seçimləri API ilə **eyni** olmalıdır (`JsonStringEnumConverter`, camelCase). Enum-lar API-də mətn kimi gedir, əks halda `status`, `seatClass` oxunmur. Test: enum sahəli bir cavabı Admin client-i ilə oxu
- [ ] **Admin heç bir daxili layihəyə istinad etmir** (`.csproj` yoxlanır)
**Commit-lər:** `feat(admin): add cookie auth with jwt handler`, `feat(admin): add layout and shared partials`, `feat(api): add admin controller base with role policy`
**Bitmə meyarı:** admin giriş edir, `Customer` admin panelə girə bilmir.

### Gün 29: Aviaşirkət, aeroport, təyyarə idarəsi
**Branch:** `feat/admin-reference-data`
- [ ] API handler-ləri, validatorlar və MVC səhifələri (siyahı, axtarış, səhifələmə, yaratma, redaktə, silmə): Airlines, Airports (koordinat və alias validasiyası), Aircraft
- [ ] İstifadə olunan aeroport/aviaşirkət/təyyarə silinmir (soft delete və ya "deaktiv" mesajı)
**Commit-lər:** hər entity üçün ayrıca `feat(admin): add <entity> management`
**Bitmə meyarı:** üç referans məlumat CRUD-u işləyir.

### Gün 30: Uçuşlar, cədvəl və marşrutlar (F8)
**Branch:** `feat/admin-flights`
- [ ] Uçuş siyahısı (filtr), yaratma, redaktə, status (`Delayed`, `Cancelled`). **Toqquşma qoruması:** eyni təyyarənin vaxtı üst-üstə düşən reysi olmaz
- [ ] Reys ləğvi: rezervasiyalar tranzaksiyada 100% qaytarma ilə ləğv olunur
- [ ] **Cədvəl:** "Çoxlu uçuş yarat" (marşrut, təyyarə, saat, günlər, tarix aralığı) toqquşmasız uçuşlar yaradır
- [ ] **Marşrutlar** səhifəsi: (çıxış, təyinat) cütləri uçuşlardan qruplanmış, reys sayı və orta qiymət; "bu marşrut üçün reys yarat"
- [ ] Testlər: toqquşma, reys ləğvi
**Commit-lər:** `feat(admin): add flights management`, `feat(admin): prevent aircraft schedule conflicts`, `feat(admin): cancel flight with bookings refund`, `feat(admin): add bulk flight scheduling`, `feat(admin): add routes overview`, `test(admin): cover conflicts and flight cancellation`
**Bitmə meyarı:** admin reys yaradır və Client axtarışında görünür.

### Gün 31: Oturacaq inventarı (F8)
**Branch:** `feat/admin-seat-inventory`
- [ ] Təyyarə sxemi səhifəsi: oturacaq xəritəsi, sinif, çarpan, exit row dəyişmək
- [ ] Reys səhifəsi: oturacaq xəritəsi, dolu oturacaqda sərnişin adı
- [ ] **Reys üçün blok:** `FlightSeatBlock` (FlightId, SeatId, Reason) + migration; doluluq sorğusu bloklanmış yerləri `Occupied/Blocked` göstərir; booked olanı bloklamaq olmaz
- [ ] Axtarışdakı boş yer sayı **bloklanmış yerləri də çıxır**. Qayda saxlanılır: `Search.Available == SeatMap(Available + Held)`, test yenilənir
- [ ] Testlər: blok və bloklanmış yerin rezerv edilməməsi
**Commit-lər:** `feat(admin): add aircraft seat template editor`, `feat(seats): add per flight seat blocks`, `feat(admin): add flight seat inventory view`, `test(seats): cover blocked seats`
**Bitmə meyarı:** admin yeri bloklayır, Client xəritəsində bloklanmış görünür və seçilə bilmir.

### Gün 32: Rezervasiya idarəsi
**Branch:** `feat/admin-bookings`
- [ ] Siyahı: axtarış (PNR, email, soyad), status və tarix filtri, səhifələmə
- [ ] Detal: sərnişinlər, oturacaqlar, ödəniş, qiymət dökümü; admin ləğvi (qaytarma ilə)
**Commit-lər:** `feat(admin): add bookings list with search`, `feat(admin): add booking details and admin cancellation`
**Bitmə meyarı:** admin PNR ilə rezervasiya tapıb ləğv edir.

### Gün 33: KPI dashboard və `v0.3-admin`
**Branch:** `feat/admin-dashboard`
- [ ] Statistika: bu günün reysləri, satılan biletlər, gəlir, doluluq faizi, ləğv faizi, orta bilet qiyməti, ən populyar marşrutlar (verilənlər bazası tərəfində `GroupBy`)
- [ ] Chart.js: gəlir (günlər üzrə), ən çox satılan marşrutlar, reys doluluğu; tarix filtri
- [ ] `DemoDataSeeder` (yalnız Development): qrafiklər dolu görünsün deyə təsadüfi rezervasiyalar
- [ ] Stats üçün unit test (kiçik data ilə dəqiq rəqəmlər); `develop` → `main`, tag `v0.3-admin`
**Bitmə meyarı:** dashboard real data göstərir.

### MƏRHƏLƏ D: Yaradıcılıq, keyfiyyət və hazırlıq (Gün 34-40)

### Gün 34: PDF boarding pass və QR
**Branch:** `feat/boarding-pass`
- [ ] QuestPDF (**Community lisenziyası** kodda elan olunur) + QRCoder: sərnişin, reys, saatlar, oturacaq, PNR, QR (PNR + oturacaq)
- [ ] `GET /api/v1/bookings/{id}/boarding-pass?ticketId=` (sahiblik yoxlaması), yalnız `Confirmed`
- [ ] Client və Admin-də yükləmə (`Authorization` lazım olduğu üçün Client-də `fetch` + blob)
- [ ] Test: PDF boş deyil
**Commit-lər:** `feat(pdf): add boarding pass with qr code`, `feat(api): add boarding pass endpoint`, `feat(client): add boarding pass download`, `test(pdf): verify generated pdf`
**Bitmə meyarı:** My Trips-dən PDF yüklənir.

### Gün 35: Bonus: canlı izləmə (seçim) və ya bufer
**Branch:** `feat/live-tracking`
- [ ] **Seçim A (tövsiyə):** `f = (indi − çıxış) / (gəliş − çıxış)`, böyük dairə qövsü, istiqamət, hündürlük profili, ETA. API `GET /api/v1/tracking/flights/{id}`, Leaflet + OSM, 5 san polling (`visibilitychange` ilə dayanır), "fokuslan" düyməsi, statusu səslə oxu. Aeroport koordinatları tələb olunur (bazada yoxdursa Airport-a `Latitude/Longitude` + migration)
- [ ] `GeoMath` və `TrackingService` `TimeProvider` ilə unit testlidir; admin "demo reys" (çıxış = indi − 1 saat)
- [ ] **Seçim B:** gecikmiş işləri bitir (bufer)
**Bitmə meyarı:** demo reysində təyyarə xəritədə hərəkət edir. Gecikmə olarsa kəs, bufer kimi istifadə et. `v0.4-features` tag-i.

### Gün 36: Test və təhlükəsizlik günü
**Branch:** `chore/quality-security`
- [ ] Test əhatəsinə baxış: qiymət, ləğv, doluluq, booking, payment, validatorlar, `IBusinessTime`, paralel rezervasiya
- [ ] Əl ilə tam ssenari: səslə axtarış → xəritə → ödəniş → PNR → PDF → admin-də görünmə → ləğv
- [ ] Təhlükəsizlik: secret-lər gitdə yoxdur (`git log -p` axtarışı), CORS, sahiblik yoxlamaları, anti-forgery, login limiteri, `innerHTML` istifadəsi yoxdur
- [ ] `dotnet format`, xəbərdarlıq = 0, ölü kod və `TODO` silinir
- [ ] İstifadə olunmayan `Booking.ExpiresAtUtc` və `BookingStatus.Pending/Expired` təmizlənir (yeni migration, alətlə) və ya README-də niyə qaldığı yazılır
**Bitmə meyarı:** testlər yaşıl, siyahı tam yoxlanıb.

### Gün 37: Sənədləşmə və GitHub
**Branch:** `docs/final-documentation`
- [ ] README: layihə təsviri, **Mermaid arxitektura və ER diaqramları**, texnologiyalar (MediatR 12.5.0 Apache 2.0 qeydi), işə salma (SQL Server, migration, user-secrets, seed), demo hesabları, ekran görüntüləri, dizayn qərarları və **məhdudiyyətlər** (demo data, real ödəniş yoxdur, simulyasiya)
- [ ] Scalar son yoxlama (təsvirlər, nümunələr, JWT izahı)
- [ ] `docs/notes.md`: özünütest cavabları; `docs/PLAN.md` real vəziyyətə uyğunlaşdırılır
- [ ] GitHub: branch-lər silinmir, tag-lər qoyulub, müəllim collaborator-dur; istəyə bağlı sadə GitHub Actions (`dotnet build` + `dotnet test`)
**Bitmə meyarı:** yad adam README ilə layihəni qaldıra bilir.

### Gün 38: Kodun dondurulması (`v1.0`) və slaydlar
**Branch:** `release/v1.0`
- [ ] Yeni feature yoxdur. Son regression, kritik bug-lar
- [ ] **Bazanı yenidən seed et** (reyslər müdafiə tarixindən sonra da olmalıdır, hər marşrut hər gün üçün yoxla)
- [ ] Demo kompüteri: Chrome, mikrofon icazəsi, səs (`az-AZ`/fallback), internet, hesablar, üç layihənin qalxması, ehtiyat demo videosu
- [ ] **Slaydlar (12-14):** problem, feature-lar, arxitektura, hibrid qərar, ER, rezervasiya və concurrency, biznes qaydaları, səs, təhlükəsizlik və testlər, Git prosesi, çətinliklər və məhdudiyyətlər. PDF ehtiyatı `docs/` içində
- [ ] `develop` → `main`, tag `v1.0`
**Bitmə meyarı:** `v1.0` tag-i, slaydlar və demo hazırdır.

### Gün 39: Məşq (1)
- [ ] Demo ssenarisini vaxtla 3 dəfə keç (sıra: demo, GitHub, Scalar, sistem dizaynı, kod)
- [ ] **Kod gəzintisi:** `Program.cs`, `AppDbContext` + `Ticket` konfiqurasiyası, booking handler, `PricingService`, `CancellationPolicy`, `ValidationBehavior`, `IBusinessTime`, `seatMap.js`, `JwtDelegatingHandler`. Hər biri ~1 dəqiqə
- [ ] Sual-cavab (səslə): iki nəfər eyni yeri alsa nə olur, niyə iki frontend yanaşması, niyə Admin DB-yə getmir, JWT necə işləyir, niyə CQRS və MediatR 12.5.0, vaxt qurşağı qərarı, səs işləməsə nə edərsən
**Bitmə meyarı:** demo vaxta yerləşir, 9 faylı izah edirsən.

### Gün 40: Mock müdafiə və texniki yoxlama
- [ ] Tam mock müdafiə (slayd + demo + GitHub + Scalar + kod + suallar), vaxtla
- [ ] Texniki siyahı: noutbuk, şarj, brauzer, mikrofon, internet və hotspot, SQL Server, təmiz seed, hesablar, slayd PDF-i, video, repo linki
- [ ] Plan B: internet getsə, səs işləməsə, xəritə yüklənməsə nə edəcəksən
- [ ] Yüngül təkrar, istirahət. Kod dəyişikliyi yalnız kritik bug üçündür

---

## 6. Git qaydaları
- Hər gün ayrıca branch, hər məntiqi işdən sonra atomik commit, `git add` ilə konkret fayllar, commit-dən əvvəl `dotnet build`
- Merge: `develop`-a `--no-ff`; squash yoxdur, branch-lər silinmir, force push yoxdur
- `main`-ə yalnız mərhələ sonlarında: `v0.1-backend` (Gün 19), `v0.2-client` (27), `v0.3-admin` (33), `v0.4-features` (35), `v1.0` (38)
- Hər gün sonunda push; secret commit olunmur
- Hər günün sonunda 15 dəq: kodu öz sözlərinlə izah et, `docs/notes.md`-ə 3 sual-cavab yaz

## 7. Gecikmə qaydası
Bufer günləri: **Gün 19, Gün 27**, Gün 35 (seçim B). Geridə qalsan əvvəlcə orada qapat. Hələ də çatmasa bu sırayla kəs: Gün 35 izləmə → Gün 34-də QR → Gün 26-da səs komandaları → Gün 33-də qrafiklərin sayı 5-dən 3-ə. **Heç vaxt kəsilməyənlər:** F1-F8, Scalar, rezervasiya concurrency testi, README, atomik commit-lər.
