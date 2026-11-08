# AGENT_RULES.md — Qayda və Tələblər

Bu fayl SkyReserve layihəsi üzərində işləyən developer və AI agent-in riayət etməli olduğu dəmir qaydaları müəyyən edir.

---

### 1. Git və Budaq İdarəetməsi (Strict GitFlow)
1. **İstifadəçidən İcazəsiz Commit Qadağandır:**
   Agent heç vaxt özbaşına `git commit` icra etmir. Hər addımdan sonra dəyişən faylları, `dotnet build` / `dotnet test` nəticəsini və təklif olunan commit mesajını göstərir, dayanır və istifadəçinin "bəli" təsdiqini gözləyir.
2. **Keçmişi Yenidən Yazmaq və Force Push Qadağandır:**
   GitHub-a göndərilmiş commit-ləri heç vaxt silmə, `git push --force` və ya `git rebase -i` etmə. Səhvlər yeni düzəliş commit-ləri ilə aradan qaldırılır.
3. **Conventional Commits Formatı:**
   Bütün commit mesajları bu standartda olmalıdır:
   * `feat(...)`: yeni funksionallıq
   * `fix(...)`: xətanın düzəldilməsi
   * `chore(...)`: sazlama, konfiqurasiya və ya paket yeniləməsi
   * `test(...)`: testlərin əlavə olunması və ya yenilənməsi
   * `docs(...)`: sənədləşmə dəyişiklikləri
4. **Budaq Strategiyası:**
   Əsas işçi budaq `develop`, buraxılış üçün `main`. Hər bir tapşırıq ayrıca `feat/` və ya `fix/` budağında işlənir və `--no-ff` ilə birləşdirilir.

---

### 2. Texnoloji və Lisenziya Məhdudiyyətləri
1. **MediatR:**
   `MediatR` paketi dəqiq **`12.5.0`** versiyasında saxlanılmalıdır (Apache 2.0 açıq qaynaq lisenziyası altında buraxılmış sonuncu pulsuz versiya).
2. **Paket Lisenziyaları:**
   Yalnız MIT və ya Apache 2.0 lisenziyalı paketlərə icazə verilir. Qeyri-pulsuz / kommersiya lisenziyalı kitabxanalar (məsələn, FluentAssertions v8) qadağandır. Testlərdə standart `xUnit` asserts istifadə olunur.
3. **System.Security.Cryptography.Pkcs:**
   Tranzitiv `NU1903` yüksək dərəcəli zəiflik xəbərdarlığını susdurmaq üçün `10.0.14` versiyasında saxlanılır və `.csproj` faylında müvafiq XML şərhlə sənədləşdirilir.
4. **Zaman və Saat Qurşağı İdarəetməsi (TimeProvider):**
   Kod bazasında birbaşa `DateTime.UtcNow` və ya `DateTime.Now` çağırışları qadağandır. Həmişə `TimeProvider.GetUtcNow()` (və ya `IBusinessTime` servisi) inyeksiya olunmalıdır ki, xUnit testlərində `FakeTimeProvider` ilə zaman dəqiq simulyasiya edilə bilsin.

---

### 3. Arxitektura və Verilənlər Bazası Qaydaları
1. **Model Dəyişikliyi və Miqrasiya:**
   * Entity dəyişdirildikdə hər zaman `dotnet ef migrations has-pending-model-changes` yoxlanılmalı və exit code 0 olmalıdır.
   * Miqrasiyalar heç vaxt əl ilə yazılmamalı, yalnız `dotnet ef migrations add` aləti ilə generasiya olunmalıdır.
   * `EnsureCreatedAsync` qadağandır, yalnız `MigrateAsync` (yalnız Development mühitində) işlədilir.
2. **Soft Delete və Filtrlər:**
   * Bütün `BaseEntity`-dən törəyən cədvəllərə `AppDbContext.OnModelCreating` daxilində qlobal `!IsDeleted` filtri tətbiq olunur.
   * Repozitoriyalar təkrar `.Where(x => !x.IsDeleted)` yazmamalıdır.
   * Bütün unikal indekslər `[IsDeleted] = 0` filtri ilə təmin edilməlidir.
3. **Ləğvolunma və Silinmə Fərqi:**
   * Bilet (`Ticket`) və ya Rezervasiya (`Booking`) ləğv edildikdə və ya vaxtı bitdikdə **heç vaxt `IsDeleted` istifadə edilmir**.
   * Bilet üçün: `IsCancelled = true`, `CancelledAtUtc = timeProvider.GetUtcNow().UtcDateTime` (birbaşa `DateTime.UtcNow` qadağandır).
   * Rezervasiya üçün: `Status = BookingStatus.Cancelled` və ya `BookingStatus.Expired`.
   * Tickets unikal cütlüyü (`FlightId, SeatId`) üçün indeks filtri: `[IsDeleted] = 0 AND [IsCancelled] = 0`.
4. **Rezervasiya və Oturacaq Əməliyyatları (Booking Rule):**
   * **Booking rule:** before taking a seat, in the SAME transaction cancel tickets of Pending bookings whose `ExpiresAtUtc < timeProvider.GetUtcNow().UtcDateTime` for that seat (`Ticket.IsCancelled = true`, Booking status `Expired`). The background service is only cleanup, not the safety mechanism.
5. **Generators və Pure Funksiyalar:**
   * Oturacaq düzülüşü (`SeatLayoutGenerator`) və reys cədvəli (`FlightScheduleGenerator`) `Application` qatında pure funksiyalar (EF-dən kənar) kimi saxlanılmalıdır.
   * Təyyarənin `TotalSeats` sayı hər zaman generator-dan çıxan real yerlərin sayı ilə eyni olmalıdır (yeganə həqiqət mənbəyi).
   * Reys cədvəlində eyni təyyarə üzrə üst-üstə düşmə sıfır olmalı, növbəti uçuş əvvəlki enişdən ən azı 90 dəqiqə sonra başlamalı və əvvəlki eniş aeroportu növbəti çıxış aeroportu olmalıdır.
