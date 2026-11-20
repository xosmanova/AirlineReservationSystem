using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AirlineReservation.Application.Common.Models;
using AirlineReservation.Application.Contracts.Identity;
using AirlineReservation.Application.DTOs.Airline;
using AirlineReservation.Application.DTOs.Auth;
using AirlineReservation.Infrastructure.Context;
using AirlineReservation.Infrastructure.Identity.Services;
using AirlineReservation.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AirlineReservation.IntegrationTests.Auth;

public class AuthControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseCreatedAsync();
        await _factory.ClearTestDataAsync();
        await _factory.SeedTestDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static string CreateStrongSecret() => $"TestAuth!{Guid.NewGuid():N}aA1";

    private sealed class FixedOffsetClock(DateTimeOffset fixedUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => fixedUtc;
    }

    [Theory]
    [InlineData("/api/v1/auth/me")]
    [InlineData("/api/v1/auth/admin-check")]
    public async Task ProtectedEndpoints_WithoutToken_Return401AndNeverRedirect302(string endpoint)
    {
        var response = await _client.GetAsync(endpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.Equal(401, body.StatusCode);
    }

    [Fact]
    public async Task Register_CreatesCustomerUser_HashesSecret_AndIssuesValidToken()
    {
        var secret = CreateStrongSecret();
        var request = new RegisterRequestDto(
            "newcustomer@airline.test",
            secret,
            "Əli",
            "Məmmədov");

        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var registerPayload = await registerResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        Assert.NotNull(registerPayload?.Data);
        Assert.True(registerPayload.Success);
        Assert.Equal("newcustomer@airline.test", registerPayload.Data.User.Email);
        var role = Assert.Single(registerPayload.Data.User.Roles);
        Assert.Equal("Customer", role);

        // Verify password is hashed in database
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbUser = await context.Users.SingleOrDefaultAsync(u => u.Email == "newcustomer@airline.test");
            Assert.NotNull(dbUser);
            Assert.False(string.IsNullOrWhiteSpace(dbUser.PasswordHash));
            Assert.StartsWith("AQAAAA", dbUser.PasswordHash);
            Assert.NotEqual(secret, dbUser.PasswordHash);
        }

        // Token works on /me
        using var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", registerPayload.Data.AccessToken);

        var meResponse = await authedClient.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var mePayload = await meResponse.Content.ReadFromJsonAsync<ApiResponse<AuthUserDto>>();
        Assert.NotNull(mePayload?.Data);
        Assert.Equal(registerPayload.Data.User.Id, mePayload.Data.Id);
        Assert.Equal("newcustomer@airline.test", mePayload.Data.Email);
        Assert.Equal("Əli", mePayload.Data.FirstName);
        Assert.Contains("Customer", mePayload.Data.Roles);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409Conflict()
    {
        var secret = CreateStrongSecret();
        var first = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequestDto("dup@airline.test", secret, "A", "B"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequestDto("dup@airline.test", CreateStrongSecret(), "A", "B"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var payload = await second.Content.ReadFromJsonAsync<ApiResponse>();
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal(409, payload.StatusCode);
    }

    [Fact]
    public async Task Register_WeakInput_Returns400BadRequest()
    {
        var weakInput = new string('a', 5);
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequestDto("weak@airline.test", weakInput, "Ali", "Aliyev"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse>();
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal(400, payload.StatusCode);
    }

    [Fact]
    public async Task Login_WithSeededAdmin_IssuesTokenWithAdminClaimAndGrantsAdminCheck()
    {
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequestDto(CustomWebApplicationFactory.TestAdminEmail, _factory.TestAdminCredential));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        Assert.NotNull(loginPayload?.Data);
        Assert.Contains("Admin", loginPayload.Data.User.Roles);

        var parsed = new JwtSecurityTokenHandler { MapInboundClaims = false }
            .ReadJwtToken(loginPayload.Data.AccessToken);
        Assert.Equal(loginPayload.Data.User.Id.ToString(), parsed.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(CustomWebApplicationFactory.TestAdminEmail, parsed.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Contains(parsed.Claims, c => c.Type == "role" && c.Value == "Admin");

        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginPayload.Data.AccessToken);

        var adminCheck = await adminClient.GetAsync("/api/v1/auth/admin-check");
        Assert.Equal(HttpStatusCode.OK, adminCheck.StatusCode);
    }

    [Fact]
    public async Task AdminCheck_WithCustomerToken_Returns403Forbidden()
    {
        var registerResp = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequestDto("custrole@airline.test", CreateStrongSecret(), "Nigar", "Hasanli"));
        Assert.Equal(HttpStatusCode.Created, registerResp.StatusCode);

        var auth = (await registerResp.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>())!.Data!;

        using var custClient = _factory.CreateClient();
        custClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await custClient.GetAsync("/api/v1/auth/admin-check");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.Equal(403, body.StatusCode);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401Unauthorized()
    {
        var wrongInput = CreateStrongSecret();

        var wrongPassResp = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequestDto(CustomWebApplicationFactory.TestAdminEmail, wrongInput));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassResp.StatusCode);

        var unknownUserResp = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequestDto("unknown@airline.test", wrongInput));

        Assert.Equal(HttpStatusCode.Unauthorized, unknownUserResp.StatusCode);
    }

    [Fact]
    public async Task Me_TokenIssuingAndValidation_UseSameTimeProvider_RejectsExpiredAcceptsRelativeActive()
    {
        // Seed and retrieve real admin user id from DB
        using var scope = _factory.Services.CreateScope();
        var serverClock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var jwtOptions = scope.ServiceProvider.GetRequiredService<IOptions<JwtSettings>>().Value;
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = await context.Users.SingleAsync(u => u.Email == CustomWebApplicationFactory.TestAdminEmail);

        var userInfo = new TokenUserInfo(admin.Id, admin.Email!, admin.FirstName, admin.LastName, ["Admin"]);

        // 1. Token issued 120 minutes BEFORE frozen factory clock with 60-min lifetime -> expired at -60m relative to factory clock
        var expiredClock = new FixedOffsetClock(serverClock.GetUtcNow().AddMinutes(-120));
        var expiredService = new JwtTokenService(Options.Create(jwtOptions), expiredClock);
        var expiredToken = expiredService.GenerateToken(userInfo);

        using var expiredClient = _factory.CreateClient();
        expiredClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", expiredToken.AccessToken);

        var expiredResp = await expiredClient.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, expiredResp.StatusCode);

        // 2. Token issued 30 minutes BEFORE frozen factory clock with 60-min lifetime -> expires 30m AFTER frozen factory clock
        //    This passes even though real wall-clock is far in the future, proving server validation uses frozen TimeProvider!
        var activeClock = new FixedOffsetClock(serverClock.GetUtcNow().AddMinutes(-30));
        var activeService = new JwtTokenService(Options.Create(jwtOptions), activeClock);
        var activeToken = activeService.GenerateToken(userInfo);

        using var activeClient = _factory.CreateClient();
        activeClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", activeToken.AccessToken);

        var activeResp = await activeClient.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, activeResp.StatusCode);
    }

    [Fact]
    public async Task LoginRateLimiter_BlocksExcessLoginAttemptsWith429_WithoutAffectingOtherEndpoints()
    {
        using var limitedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:Login:PermitLimit"] = "2",
                    ["RateLimiting:Login:WindowSeconds"] = "60"
                });
            });
        });

        using var limitedClient = limitedFactory.CreateClient();
        var wrongInput = CreateStrongSecret();

        // First 2 attempts are permitted by limiter (fail credential check with 401)
        var attempt1 = await limitedClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequestDto("unknown@airline.test", wrongInput));
        var attempt2 = await limitedClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequestDto("unknown@airline.test", wrongInput));

        Assert.Equal(HttpStatusCode.Unauthorized, attempt1.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, attempt2.StatusCode);

        // 3rd attempt exceeds PermitLimit=2 -> 429 Too Many Requests with ApiResponse
        var attempt3 = await limitedClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequestDto("unknown@airline.test", wrongInput));

        Assert.Equal(HttpStatusCode.TooManyRequests, attempt3.StatusCode);
        var payload = await attempt3.Content.ReadFromJsonAsync<ApiResponse>();
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal(429, payload.StatusCode);

        // Non-login endpoints must NOT be rate limited
        var airlinesResponse = await limitedClient.GetAsync("/api/v1/airlines");
        Assert.Equal(HttpStatusCode.OK, airlinesResponse.StatusCode);
        var airlinesPayload = await airlinesResponse.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<AirlineDto>>>();
        Assert.NotNull(airlinesPayload);
        Assert.True(airlinesPayload.Success);
    }
}
