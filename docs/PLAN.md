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

### Gün 10: Aeroport və aviaşirkət API, alias, reseed
**Branch:** `feat/airports-airlines-api`
- [ ] **Gün 9-dan qalan:** hər sıralamaya unikal tie-break (`ThenBy(f => f.Id)`: çıxış vaxtı, qiymət, müddət). "Aktiv bilet" qaydası **tək bir ifadədir** (silinməyib, `IsCancelled` deyil, rezervasiya `Cancelled` deyil), axtarış və oturacaq xəritəsi eyni ifadəni istifadə edir. Test: eyni reys üçün ikisinin boş yer sayı eynidir
- [ ] `Airport.Aliases` (vergüllə ayrılmış) + migration (alətlə). Seed alias-ları ASCII variantlarla: GYD `Baku,Bakı,Baki,Баку`, IST `Istanbul,İstanbul,Istanbul Airport,Стамбул`, SAW `Istanbul,İstanbul,Sabiha Gökçen,Sabiha Gokcen,Стамбул`, DXB `Dubai,Dubay,Дубай`, JFK `New York,Nyu-York,Нью-Йорк`, LHR `London,Londan,Лондон`, FRA `Frankfurt,Франкфурт`. Kollasiyaya güvənmə, `ə` və `ı` alias-larda açıq yazılır
- [ ] Aviaşirkət adları: "Azerbaijan Airlines (AZAL)", "Turkish Airlines", "Emirates", "Lufthansa". Dev bazasını sıfırla (`dotnet ef database drop -f`) və yenidən seed et
- [ ] `GET /api/v1/airports?search=` (IATA, ad, şəhər, alias; hərf registrinə həssas deyil; max 10; boş axtarış şəhər üzrə ilk 10). CQRS query, validator (uzunluq ≤ 50), `ApiResponse<List<AirportDto>>`. `GET /api/v1/airlines`. XML şərhləri və `ProducesResponseType`
- [ ] Testlər: handler və validator unit testləri; inteqrasiya testləri (`Bakı`, `baku`, `Баку`, `GYD`, `ist`, `Sabiha` düzgün aeroportu tapır, tanımayan mətn boş siyahı qaytarır, xəta yox)
**Commit-lər:** `fix(flights): add deterministic tie-break to flight ordering`, `refactor(flights): share active ticket rule between search and seat map`, `feat(airports): add airport aliases with migration and seed`, `feat(airports): add airport search endpoint`, `feat(airlines): add airlines endpoint`, `test(airports): cover alias search`
**Bitmə meyarı:** autocomplete `Bakı`, `Баку`, `ist` üçün düzgün aeroportları qaytarır, axtarış və xəritə eyni boş yer sayını göstərir.

### Gün 11: ASP.NET Core Identity
**Branch:** `feat/identity-setup`
- [ ] `AppUser : IdentityUser<Guid>`, `AppRole`, rollar `Admin` və `Customer` (Infrastructure, Domain Identity-ni tanımır)
- [ ] Admin hesabı seed: parol `user-secrets`-dən, `appsettings`-də yoxdur. README-də "yalnız demo"
- [ ] `Booking.UserId` üçün Infrastructure konfiqurasiyasında `AspNetUsers`-ə FK (`Restrict`) və indeks
- [ ] Migration (alətlə), `has-pending-model-changes` exit 0
**Commit-lər:** `feat(infra): add identity user and roles`, `feat(infra): seed admin from user secrets`, `feat(infra): link booking user to identity`
**Bitmə meyarı:** Identity cədvəlləri bazadadır, admin hesabı secret-dən yaranır.

### Gün 12: JWT və `AuthController`
**Branch:** `feat/jwt-auth`
- [ ] `ITokenService` / `JwtTokenService` (claim-lər: Id, email, rol), açar `user-secrets`-də
- [ ] `register` (rol `Customer`), `login`, `me`; parol siyasəti; validatorlar
- [ ] `[Authorize]` və `[Authorize(Roles = "Admin")]`; Scalar-da Bearer "Authorize"
- [ ] Yalnız login üçün daxili rate limiter (`AddRateLimiter`)
- [ ] Testlər: token claim-ləri, səhv parol, rol yoxlaması
**Commit-lər:** `feat(auth): add jwt token service`, `feat(api): add register login and me endpoints`, `feat(api): add login rate limiter`, `test(auth): cover token and role checks`
**Bitmə meyarı:** Scalar-da qeydiyyat, giriş, token ilə `me` işləyir, qorunan endpoint tokensiz 401 verir.

### Gün 13: Oturacaq xəritəsi müqaviləsinin təmizlənməsi, qiymət və quote
**Branch:** `feat/seatmap-contract-and-pricing`
`GET /api/v1/flights/{id}/seatmap` artıq var (köhnə Gün 9). Bu gün onu **kilid və blokla uyğun** edirik.
- [ ] `SeatMapDto` müqaviləsi: `bool IsOccupied` → `Status` (`Available`, `Occupied`, `Held`, `Blocked`, JSON-da mətn kimi, `JsonStringEnumConverter`) və `heldByMe` (token varsa). **`SvgMap` əsas cavabdan çıxarılır** (hər sorğuda yüzlərlə oturacağın SVG sətri göndərmək lazımsızdır; server tərəfli SVG ayrıca endpoint kimi qala bilər, Client xəritəni JSON-dan çəkir). `FlightSeatDto` adı `SeatDto` olur (`FlightSeat` entity-si yoxdur)
- [ ] Doluluq sorğusu yalnız aktiv biletləri sayır (ləğv, silinmiş, `Cancelled` rezervasiya çıxarılır, bu hissə var). Bütün uçuşu bilet və rezervasiyalarla yükləmək əvəzinə **`Select` proyeksiyası** ilə tək sorğu
- [ ] `PricingService`: `BasePrice × Seat.PriceMultiplier` (business, exit row), pəncərə/keçid əlavə haqqı (konfiqurasiya), son 3 gündə tələbat çarpanı (default ×1.2). Xəritədəki `Price` **yalnız bu servisdən** gəlir (iki ayrı qiymət məntiqi olmasın)
- [ ] `POST /api/v1/bookings/quote` (flightId, seatIds) → sətir-sətir döküm və yekun
- [ ] Testlər: xəritə statusları, table-driven qiymət testləri (economy, business, exit row, pəncərə, tələbat, yuvarlaqlaşdırma)
**Commit-lər:** `refactor(seats): replace occupied flag with seat status and drop svg from main dto`, `perf(seats): project seat map in a single query`, `feat(pricing): add pricing service with configurable fees`, `feat(bookings): add quote endpoint`, `test(pricing): add table driven pricing tests`
**Bitmə meyarı:** 777 üçün 3-4-3 xəritə `Status` ilə qayıdır, quote düzgün rəqəmlər verir.

### Gün 14: Oturacaq kilidi (`SeatHold`, bazada)
**Branch:** `feat/seat-hold`
- [ ] `SeatHold` entity (`FlightId`, `SeatId`, `UserId`, `ExpiresAtUtc`), **soft delete yoxdur** (fiziki silinir), `(FlightId, SeatId)` üzərində **filtrsiz unikal indeks**, `UserId` indeksi, migration (alətlə), `has-pending-model-changes` exit 0
- [ ] `POST /api/v1/flights/{id}/holds` `[Authorize]`, body `{ seatIds: [] }`, **atomik** (hamısı və ya heç biri), ≤ 9 yer. Tranzaksiyada: (1) bu istifadəçinin həmin reysdəki köhnə hold-larını burax, (2) seçilmiş yerlərin **vaxtı keçmiş hold-larını sil** (Hold qaydası), (3) yerlərin həmin təyyarəyə aid olduğunu, aktiv biletlə dolu və ya bloklu olmadığını yoxla, (4) yaz. Unikal indeks pozuntusu → `ConflictException` (409). `ExpiresAtUtc = TimeProvider + Hold:Minutes` (default 10), **uzatma yoxdur**
- [ ] `GET /api/v1/flights/{id}/holds/mine` (qalan vaxtla), `DELETE /api/v1/flights/{id}/holds` (öz hold-larını burax)
- [ ] Xəritə: başqasının aktiv hold-u `Held`, öz hold-u `Held` + `heldByMe = true`
- [ ] `HoldCleanupService` (`BackgroundService`, hər 30 san, vaxtı keçmişləri silir, `TimeProvider`)
- [ ] Testlər: iki istifadəçi eyni yeri kilidləyir (biri 409), vaxtı bitmiş hold götürülür (`FakeTimeProvider`), 9-dan çox yer rədd, başqasının hold-unu buraxmaq olmur. **Paralel inteqrasiya testi** (real SQL Server test bazası)
**Commit-lər:** `feat(domain): add seat hold entity`, `feat(infra): add seat hold configuration and migration`, `feat(seats): add atomic multi seat hold command`, `feat(seats): add release and my holds endpoints`, `feat(seats): add hold cleanup background service`, `test(seats): cover hold conflicts expiry and parallel access`
**Bitmə meyarı:** iki hesabla eyni yeri kilidləməyə çalışanda biri 409 alır, vaxt bitəndə yer azad olur, restart kilidləri silmir.

### Gün 15: PNR və checkout validatoru
**Branch:** `feat/pnr-and-checkout-validation`
- [ ] `PnrGenerator` (`RandomNumberGenerator`, 6 simvol, `0/O/1/I` yoxdur); unikal pozuntusunda təkrar cəhd
- [ ] `CheckoutCommandValidator`: sərnişin sayı = hold edilmiş yer sayı, 1-9, hər sərnişin üçün ad (`\p{L}`), pasport, doğum tarixi, əlaqə (email, telefon), kart sahələrinin formatı; reys gələcəkdədir və ləğv olunmayıb
- [ ] Testlər: PNR formatı və icazəli simvollar, validator halları (Azərbaycan adları daxil)
**Commit-lər:** `feat(bookings): add pnr generator`, `feat(bookings): add checkout command validator`, `test(bookings): cover pnr and checkout validation`
**Bitmə meyarı:** testlər yaşıldır.

### Gün 16: Checkout mühərriki (ən kritik gün)
**Branch:** `feat/checkout-engine`
- [ ] `CheckoutCommand` + handler, `IUnitOfWork` tranzaksiyası. Addımlar: (1) istifadəçinin bu reysdəki hold-larını yüklə, **vaxtı bitməyibsə** davam et (əks halda 409 "kilidin vaxtı bitib"), (2) yerlərin hələ aktiv biletlə dolu olmadığını yoxla, (3) yekun qiyməti `PricingService`-dən hesabla (müştərinin göndərdiyinə etibar yox), (4) `IPaymentGateway` çağır (bu gün müvəqqəti həmişə-uğurlu stub), (5) `Passenger`, `Ticket`, `Booking (Confirmed, PNR)`, `Payment` yaz, (6) hold-ları sil, commit
- [ ] Unikal indeks pozuntusu → 409. PNR toqquşması → təkrar cəhd
- [ ] `POST /api/v1/bookings/checkout` `[Authorize]`
- [ ] Hold-u olmayan və ya başqasının hold-u ilə checkout rədd edilir
- [ ] **İnteqrasiya testləri** (real SQL Server test bazası): hold yoxdursa rədd; vaxtı bitmiş hold ilə rədd; uğurlu checkout biletləri və PNR yaradır, hold-lar silinir; **paralel iki checkout eyni oturacaq üçün yalnız biri uğurlu**
**Commit-lər:** `feat(bookings): add checkout command consuming seat holds`, `fix(bookings): reject expired or foreign holds`, `feat(api): add checkout endpoint`, `test(bookings): add checkout integration and concurrency tests`
**Bitmə meyarı:** Scalar-da hold → checkout → PNR işləyir, paralel test keçir.

### Gün 17: Ödəniş simulyasiyası
**Branch:** `feat/payment-simulation`
- [ ] `IPaymentGateway` + `MockPaymentGateway`: kart nömrəsi (Luhn), bitmə tarixi (keçməyib), CVV formatı. Test kartları: `4111111111111111` uğurlu, `4000000000000002` rədd
- [ ] Rədd: rezervasiya yaranmır, hold qalır, aydın xəta mesajı (`ApiResponse`). Uğurlu: `Payment` (məbləğ, status `Completed`, saxta istinad kodu)
- [ ] **Kart datası saxlanılmır və loglanmır** (test ilə təsdiqlə)
- [ ] README qeydi: real sistemdə ödəniş xarici çağırışdır və tranzaksiyadan kənarda idarə olunur, burada simulyasiya olduğu üçün sadələşdirilib
- [ ] Testlər: uğurlu, rədd, vaxtı keçmiş kart, hold bitdikdən sonra ödəniş cəhdi
**Commit-lər:** `feat(payments): add mock payment gateway with luhn check`, `feat(bookings): use payment gateway in checkout`, `test(payments): cover payment outcomes and card data safety`
**Bitmə meyarı:** rədd edilən kartla rezervasiya yaranmır, uğurlu kartla PNR alınır.

### Gün 18: Rezervasiya sorğuları və ləğv
**Branch:** `feat/bookings-queries-cancel`
- [ ] `GET /api/v1/bookings/pnr/{pnr}?lastName=` `[AllowAnonymous]` (yalnız baxış), `GET /api/v1/bookings/my?status=upcoming|past|cancelled` (səhifəli)
- [ ] `CancellationPolicy`: uçuşdan >24 saat qalıb 100%, ≤24 saat 50%, uçuş başlayıbsa ləğv yoxdur. `Booking.RefundAmount`, `CancelledAtUtc` + migration
- [ ] `GET .../cancel-preview`, `POST .../cancel` (sahiblik yoxlaması: yalnız öz rezervasiyası; admin hamısı). Ləğv: `Booking = Cancelled`, biletlər `IsCancelled`, `Payment = Refunded`
- [ ] Testlər: siyasət sərhədləri (tam 24 saat), başqasının rezervasiyasını ləğv cəhdi (403)
**Commit-lər:** `feat(bookings): add pnr lookup and my trips queries`, `feat(bookings): add cancellation policy with refund amount`, `feat(bookings): add cancel and cancel preview endpoints`, `test(bookings): cover cancellation boundaries and ownership`
**Bitmə meyarı:** ləğvdən sonra oturacaq xəritədə boşdur, qaytarma məbləği düzgündür.

### Gün 19: Backend inteqrasiyası (bufer) və `v0.1-backend`
**Branch:** `chore/backend-integration`
- [ ] Scalar-da tam ssenari: qeydiyyat → axtarış → xəritə → quote → rezervasiya → ödəniş → my → ləğv
- [ ] Bug-lar, çatışmayan testlər, `has-pending-model-changes` exit 0, CORS (`AllowClient` Client origin-i ilə)
- [ ] README: backend işə salma (SQL Server, migration, user-secrets, seed)
- [ ] `AGENT_RULES.md` və bu plan real vəziyyətə uyğunlaşdırılır (Hold qaydası, tək addımlı checkout)
- [ ] `develop` → `main` merge, tag `v0.1-backend`
**Bitmə meyarı:** bütün backend axını Scalar-da sınaqdan keçib.

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
- [ ] **"Davam et"** düyməsi `POST .../holds` çağırır (bütün seçilmiş yerlər atomik). 409 olsa "bu yer artıq tutulub" göstərir və xəritəni yeniləyir. Uğurlu olanda **10:00 geri sayım** (`ExpiresAtUtc`-dən) başlayır
- [ ] Xəritə 8 saniyədən bir yenilənir (başqalarının hold-ları görünür), səkmə gizli olanda dayanır
- [ ] "Seçimi dəyiş" → `DELETE .../holds`
- [ ] Klaviatura və `aria` dəstəyi, mobil üfüqi sürüşdürmə
**Commit-lər:** `feat(client): render interactive seat map from json`, `feat(client): add seat selection with live price`, `feat(client): add seat hold with countdown`, `feat(client): poll seat map for held seats`, `feat(client): improve seat map accessibility`
**Bitmə meyarı:** iki brauzerdə eyni yeri seçəndə ikincisi 409 alır və xəritə yenilənir.

### Gün 23: Checkout, ödəniş və PNR təsdiqi (F5, F6)
**Branch:** `feat/client-checkout`
- [ ] Sərnişin forması (ad `\p{L}` qaydası ilə, pasport, doğum tarixi, əlaqə), `validators.js`
- [ ] Geri sayım hold-un `ExpiresAtUtc`-ündən davam edir; bitəndə xəbərdarlıq və xəritəyə qayıdış
- [ ] Ödəniş (kart forması, Luhn), **tək `checkout` sorğusu**. Rədd: xəta göstərilir, hold qalır, yenidən cəhd olar. 409 "kilidin vaxtı bitib": xəritəyə qaytar
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
