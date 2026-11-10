# SkyReserve — Səsli İdarə Edilən Uçuş Rezervasiyası Sistemi

SkyReserve müasir Clean Architecture və CQRS prinsipləri əsasında qurulmuş mərkəzləşdirilmiş RESTful Web API, ASP.NET Core MVC Admin Portalı və Web Speech API dəstəkli müştəri veb tətbiqindən ibarət aviasiya idarəetmə və bilet rezervasiyası platformasıdır.

---

### Texnologiya Yığını (Tech Stack)
* **Mərkəzi Backend:** .NET 10 (C# 14/13), ASP.NET Core Web API, Entity Framework Core 10.
* **Verilənlər Bazası:** Microsoft SQL Server (`.\SQLEXPRESS`).
* **Arxitektura:** Clean Architecture (Domain, Application, Infrastructure, Presentation, Admin, Client).
* **CQRS & Vasitəçi:** `MediatR 12.5.0` (Apache 2.0 lisenziyası altında sonuncu açıq qaynaqlı və pulsuz versiya).
* **Obyekt Xəritələməsi (Mapper):** `Riok.Mapperly 4.3.1` (Compile-time Source Generator, sıfır reflection və yüksək performans).
* **Validasiya:** `FluentValidation` və MediatR `ValidationBehavior` avtomatik boru kəməri (pipeline).
* **Standart Cavab Zərfi:** Vahid `ApiResponse<T>` / `ApiResponse` (Success, Failure, Pagination).
* **Solution Formatı:** `AirlineReservationSystem.slnx` (.NET 10 üçün müasir və yüngül XML formatlı həll faylı).
* **Admin İdarəetmə Portalı:** ASP.NET Core MVC (.NET 10) — Web API ilə typed `HttpClient` vasitəsilə inteqrasiya.
* **Müştəri Veb Tətbiqi:** Vanilla JavaScript (ES6+), Web Speech API (səslə axtarış və naviqasiya), interaktiv SVG oturacaq xəritəsi.
* **Unit Testlər:** xUnit (Apache 2.0 / MIT təmiz lisenziya, sırf standart xUnit assertions).

---

### Verilənlər Bazası və Hub Əsaslı Demo Məlumatları
Layihədə istifadə olunan bütün uçuş, təyyarə və oturacaq məlumatları test və nümayiş üçün fiktiv (DEMO) olaraq generasiya edilir:
* **8 Təyyarə və Hub-lar:**
  * **AZAL (J2):** 2x Airbus A320-200 (Hub: **GYD** — Bakı). Marşrutlar: GYD-IST, GYD-SAW, GYD-DXB.
  * **Turkish Airlines (TK):** 
    * #1 Boeing 737-800 (Hub: **IST** — İstanbul). Marşrutlar: IST-GYD, IST-LHR, IST-FRA, IST-DXB.
    * #2 Boeing 737-800 (Hub: **GYD** — Bakı). Marşrut: GYD-LHR (**1 transfer/stop**, 10% endirimli baza qiymət).
  * **Emirates (EK):** 2x Boeing 777-300ER (Hub: **DXB** — Dubay). Marşrutlar: DXB-JFK, DXB-LHR, DXB-IST, DXB-GYD.
  * **Lufthansa (LH):** 2x Airbus A321 (Hub: **FRA** — Frankfurt). Marşrutlar: FRA-LHR, FRA-IST, FRA-GYD.
* **Oturacaq Düzülüşləri (`SeatLayoutGenerator`):**
  * Dar gövdəli təyyarələr: Biznes 2-2 (`A C | D F`), Ekonom 3-3 (`A B C | D E F`).
  * Geniş gövdəli (B777): Biznes 2-2 (`A C | D F`), Ekonom 3-4-3 (`A B C | D E F G | H J K` — `I` hərfi çıxarılıb).
  * Real oturacaq sayı təyyarənin `TotalSeats` dəyərinə 1:1 bərabərdir (A320: 142, B738: 154, A321: 172, B777: 276 yer).
* **Fasiləsiz Uçuş Qrafiki (`FlightScheduleGenerator`):**
  * 60 günlük 1,816 reys generasiya olunur.
  * Hər bir təyyarə üçün növbəti uçuş əvvəlki enişdən ən azı 90 dəqiqə sonra başlayır (02:00-20:00 UTC pəncərəsində).
  * Heç bir təyyarədə üst-üstə düşən reys yoxdur; hər uçuşun çıxış aeroportu əvvəlki uçuşun eniş aeroportudur (məkan ardıcıllığı).
  * Bakı çıxışlı bütün 5 əsas marşrutda (IST, DXB, SAW, FRA, LHR) 14 gün ərzində hər gün ən azı 1 reys mövcuddur.

---

### Autentifikasiya və Təhlükəsizlik (Identity & User Secrets)
* **ASP.NET Core Identity:** İstifadəçi və rol idarəetməsi `IdentityDbContext<AppUser, AppRole, Guid>` üzərindən aparılır. Rollar: `Admin`, `Customer`.
* **Parol Siyasəti:** Minimum 8 simvol, rəqəm, böyük hərf, kiçik hərf və xüsusi simvol tələb olunur.
* **Yalnız Demo Üçün İlkin Admin Quraşdırması (`admin@airline.test`):** İnkişaf (Development) mühitində yaradılan admin hesabı yalnız demo və test məqsədlidir. Admin parolu kod bazasında və ya `appsettings.json` faylında ƏSLA saxlanılmır. Parol `dotnet user-secrets` vasitəsilə təyin edilir:
  ```powershell
  dotnet user-secrets set "Seed:AdminPassword" '<parolunuz>' --project src/Presentation/AirlineReservation.API
  ```
  Əgər `Seed:AdminPassword` təyin edilməyibsə, sistem qəzaya uğramır (fail-safe), konsola xəbərdarlıq yazaraq admin istifadəçisinin yaradılmasını təhlükəsiz şəkildə ötürür.

---

### Baza Miqrasiyaları Qaydası (Migration Workflow)
Model dəyişikliklərinin miqrasiya snapshot-u ilə tam sinxron olduğunu yoxlamaq üçün (mütləq exit code 0 qaytarmalıdır):
```powershell
dotnet ef migrations has-pending-model-changes --project src/Infrastructure/AirlineReservation.Infrastructure --startup-project src/Presentation/AirlineReservation.API
```

Yeni miqrasiya generasiya etmək üçün:
```powershell
dotnet ef migrations add <MiqrasiyaAdi> --project src/Infrastructure/AirlineReservation.Infrastructure --startup-project src/Presentation/AirlineReservation.API
```

> **QEYD:** `EnsureCreatedAsync()` metodundan qətiyyən istifadə olunmur. Yalnız `Program.cs` daxilində Development mühitində `await dbContext.Database.MigrateAsync()` işlədilir.

> **QEYD (`MakeBookingUserRequired`):** `20261110104715_MakeBookingUserRequired` miqrasiyası `Bookings.UserId` sütunu üçün `defaultValue: Guid.Empty` (`00000000-0000-0000-0000-000000000000`) ehtiva etdiyinə görə yalnız boş `Bookings` cədvəli üzərində tətbiq edilməlidir.

---

### Layihə Qovluq Quruluşu
```text
FinalProject/
├── src/
│   ├── Core/
│   │   ├── AirlineReservation.Domain/         # Varlıqlar, enum-lar, baza interfeysləri
│   │   └── AirlineReservation.Application/    # CQRS Features, DTO-lar, Validators, Generators, Mappings
│   ├── Infrastructure/
│   │   └── AirlineReservation.Infrastructure/ # EF Core AppDbContext, Fluent Konfiqurasiyalar, Repositories, Seed
│   ├── Presentation/
│   │   └── AirlineReservation.API/            # Controller-lər, Middlewares, Program.cs, Konfiqurasiya
│   ├── Admin/
│   │   └── AirlineReservation.Admin/          # ASP.NET Core MVC idarəetmə paneli
│   └── Client/                                # Vanilla JS müştəri tətbiqi (Web Speech API)
├── tests/
│   └── AirlineReservation.Application.Tests/  # xUnit vahid testləri
├── docs/
│   └── PLAN.md                                # 40 günlük inkişaf yol xəritəsi
├── AGENT_RULES.md                             # AI Agent və developer əməkdaşlıq qaydaları
└── AirlineReservationSystem.slnx              # .NET 10 Solution faylı
```
