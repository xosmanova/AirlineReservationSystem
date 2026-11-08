# SkyReserve — 40 Günlük İnkişaf Planı (Development Roadmap)

Bu sənəd SkyReserve layihəsinin 40 günlük addım-addım inkişaf planını və cari icra vəziyyətini əks etdirir.

---

### Mərhələ 1: Təməl Arxitektura və Baza (Gün 1 – 7) — [TAMAMLANDI]
* **Gün 1:** Solution strukturu (`AirlineReservationSystem.slnx`), Clean Architecture qatları, .NET 10 setup.
* **Gün 2:** Domain varlıqları (Airport, Airline, Aircraft, Seat, Flight, Passenger, Booking, Ticket, Payment) və enum-lar.
* **Gün 3:** EF Core konfiqurasiyaları, SQL Server (`.\SQLEXPRESS`), unikal indekslər.
* **Gün 4:** İlkin aviasiya seed mexanizmi.
* **Gün 5:** Generic Repository, xüsusi repozitoriyalar və Unit of Work naxışı.
* **Gün 6:** MediatR (CQRS) setup, Riok.Mapperly, `ApiResponse<T>`, xüsusi istisnalar və Global Exception Middleware.
* **Gün 7:** FluentValidation, MediatR `ValidationBehavior`, sərnişin və axtarış qaydaları.

---

### Mərhələ 1.5: Möhkəmləndirmə Protokolu (Hardening Sprint) — [TAMAMLANDI]
* **Step 1 (Domain):** `Flight.Stops`, `Ticket.IsCancelled / CancelledAtUtc`, `Booking.ExpiresAtUtc`, `BookingStatus.Expired`.
* **Step 2 (Infra):** Bütün varlıqlara qlobal `!IsDeleted` sorğu filtri və bütün unikal indekslərə `[IsDeleted] = 0` filtri. `Tickets` üçün `[IsDeleted] = 0 AND [IsCancelled] = 0` filtri.
* **Step 3 (Migrations):** Əl ilə yazılmış köhnə miqrasiyalar silindi, `dotnet ef migrations add InitialCreate` ilə real zaman möhürlü təmiz miqrasiya yaradıldı, `MigrateAsync` yalnız Development-də tətbiq edildi.
* **Step 4 (Generators):** `SeatLayoutGenerator` (2-2 biznes, 3-3 ekonom, B777 3-4-3 `I` hərfsiz) və `FlightScheduleGenerator` pure funksiyaları.
* **Step 5 (Seed Rewrite):** 8 fiktiv təyyarə (`DEMO-xxx`), 4 aviaşirkət, 7 aeroport, 60 günlük 1,816 reys, 1488 yer, sıfır kəsişmə, fasiləsiz məkan zənciri.
* **Step 6 (Validators):** Unicode adlar (`@"^[\p{L}\s\-']+$"`), böyük/kiçik hərfə həssas olmayan pasport validasiyası və normallaşması, `TimeProvider` və "Asia/Baku" saat qurşağı. `AppValidationException` adı ilə toqquşmanın aradan qaldırılması.
* **Step 7 (Tests):** `AirlineReservation.Application.Tests` yaradıldı (xUnit, 37 test, 100% uğurlu). `MediatR 12.5.0` (Apache 2.0) pinned edildi.
* **Step 8 (Docs):** Sənədlər və qaydalar yeniləndi.

---

### Mərhələ 2: Uçuş Axtarışı və İnteraktiv Oturacaq Sistemi (Gün 8 – 14) — [NÖVBƏTİ]
* **Gün 8:** Flight Search & Advanced Filtering API (marşrut, tarix, qiymət, transfer/stop, aviaşirkət üzrə axtarış sorğuları və DTO-lar).
* **Gün 9:** Interactive SVG Seat Map Generator (kabina sxeminin dinamik vizuallaşdırılması).
* **Gün 10:** Seat Availability & Temporary Reservation Lock (seçilmiş oturacağın real vaxtda bloklanması).
* **Gün 11:** Cabin Class Pricing & Extra Amenities Multipliers API.
* **Gün 12:** Web Speech API Client Setup (brauzer səs tanıma mühərriki, Azərbaycan və İngilis dili dəstəyi).
* **Gün 13:** Voice Query Parser (səsli əmrlərdən axtarış parametrlərinin çıxarılması: "sabah Bakıdan İstanbula 2 nəfərlik uçuş").
* **Gün 14:** Voice Navigation & Feedback (səsli təsdiq və cavab bildirişləri).

---

### Mərhələ 3: Rezervasiya, Ödəniş və Müştəri Kabineti (Gün 15 – 21)
* **Gün 15:** Identity & JWT Authentication (müştəri qeydiyyatı, giriş, rollar, Guest checkout dəstəyi).
* **Gün 16:** User Profile & Booking History Management.
* **Gün 17:** Passenger Directory & Saved Travel Documents.
* **Gün 18:** Password Reset, Email Verification & Security Hardening.
* **Gün 19:** Comprehensive Booking Engine (PNR generasiyası, tranzaksion bilet yaradılması, vaxtı keçmiş pending rezervasiyaların avtomatik ləğvi).
* **Gün 20:** Payment Simulator & Integration (ödəniş statusları, 3D Secure simulyasiyası, faktura generasiyası).
* **Gün 21:** E-Ticket Issuance & Boarding Pass Generator (PDF və QR-kod dəstəyi).

---

### Mərhələ 4: Admin Portalı və Əməliyyatların İdarəedilməsi (Gün 22 – 28)
* **Gün 22:** ASP.NET Core MVC Admin Portal Setup (Typed HttpClient, Cookie Authentication).
* **Gün 23:** Flight Management (yeni reys əlavə etmə, təxirə salma, status dəyişmə).
* **Gün 24:** Fleet & Aircraft Management (təyyarələrin texniki sazlığı, oturacaq konfiqurasiyaları).
* **Gün 25:** Airport & Route Management (marşrut şəbəkəsinin monitorinqi).
* **Gün 26:** Booking & Passenger Management (bilet ləğvi, geri qaytarma / refund).
* **Gün 27:** Real-time Dashboard & KPI Metrics (doluluq faizi, gəlir, gündəlik uçuş statistikası).
* **Gün 28:** Role-Based Access Control (Admin, SuperAdmin, Operator).

---

### Mərhələ 5: Fon Xidmətləri, Təhlükəsizlik və Yekun Buraxılış (Gün 29 – 40)
* **Gün 29:** Background Worker Services (vaxtı keçmiş rezervasiyaların təmizlənməsi, uçuş statuslarının yenilənməsi).
* **Gün 30:** Automated Email Notifications (rezervasiya təsdiqi, uçuş xatırlatmaları).
* **Gün 31:** Caching Strategy (Redis / MemoryCache ilə uçuş cədvəllərinin keşlənməsi).
* **Gün 32:** Rate Limiting & API Security (DDOS müdafiəsi, IP limitləri).
* **Gün 33:** Audit Logging & Performance Monitoring.
* **Gün 34:** End-to-End Testing (API, MVC Portal və Müştəri veb tətbiqinin birgə testləri).
* **Gün 35:** Səsli İdarəetmənin Dərinləşdirilmiş Testi (müxtəlif dialekt və vurğularda səsli əmrlər).
* **Gün 36:** Verilənlər Bazasının Yekun İndekslənməsi və Query Optimization.
* **Gün 37:** Docker Containerization (Web API, MVC və SQL Server üçün docker-compose).
* **Gün 38:** CI/CD Pipeline (GitHub Actions avtomatik build və test boru kəməri).
* **Gün 39:** Canlı Mühitə (Deployment) Hazırlıq və İstifadəçi Təlimatı.
* **Gün 40:** Layihənin Yekun Təqdimatı və Kod Müdafiəsi.
