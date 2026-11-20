using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AirlineReservation.Infrastructure.Context;
using AirlineReservation.Infrastructure.Identity.Entities;
using AirlineReservation.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AirlineReservation.IntegrationTests.Identity;

public class IdentitySeedingIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;

    public IdentitySeedingIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseCreatedAsync();
        await _factory.ClearTestDataAsync();
        await _factory.SeedTestDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SeedIdentityAsync_CreatesAdminAndCustomerRoles_InDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var roleNames = await context.Roles
            .Select(r => r.Name)
            .ToListAsync();

        Assert.Equal(2, roleNames.Count);
        Assert.Contains("Admin", roleNames);
        Assert.Contains("Customer", roleNames);
    }

    [Fact]
    public async Task SeedIdentityAsync_CreatesAdminUser_WithAdminRole_ConfirmedEmail_AndHashedCredential()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var adminUser = await context.Users.SingleOrDefaultAsync(u => u.Email == CustomWebApplicationFactory.TestAdminEmail);

        Assert.NotNull(adminUser);
        Assert.Equal(CustomWebApplicationFactory.TestAdminEmail, adminUser.UserName);
        Assert.True(adminUser.EmailConfirmed);
        Assert.False(string.IsNullOrWhiteSpace(adminUser.PasswordHash));
        Assert.StartsWith("AQAAAA", adminUser.PasswordHash);
        Assert.NotEqual(_factory.TestAdminCredential, adminUser.PasswordHash);

        var isValid = await userManager.CheckPasswordAsync(adminUser, _factory.TestAdminCredential);
        Assert.True(isValid);

        var roles = await userManager.GetRolesAsync(adminUser);
        var singleRole = Assert.Single(roles);
        Assert.Equal("Admin", singleRole);
    }

    [Fact]
    public void Configuration_ClearsUserSecrets_AndUsesFactoryInMemoryOverride()
    {
        using var scope = _factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        Assert.Equal(CustomWebApplicationFactory.TestAdminEmail, configuration["Seed:AdminEmail"]);
        Assert.Equal(_factory.TestAdminCredential, configuration["Seed:AdminPassword"]);

        if (configuration is IConfigurationRoot root)
        {
            Assert.DoesNotContain(root.Providers, provider =>
                provider is JsonConfigurationProvider jsonProvider &&
                string.Equals(Path.GetFileName(jsonProvider.Source.Path), "secrets.json", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task SeedIdentityAsync_WhenExecutedTwice_IsIdempotentAndDoesNotDuplicate()
    {
        using var scope = _factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await AppDbContextSeed.SeedIdentityAsync(scope.ServiceProvider, configuration);

        Assert.Equal(2, await context.Roles.CountAsync());
        Assert.Equal(1, await context.Users.CountAsync(u => u.Email == CustomWebApplicationFactory.TestAdminEmail));
    }

    [Fact]
    public async Task SeedIdentityAsync_WhenCredentialMissing_SkipsAdminCreationWithoutCrashing()
    {
        await _factory.ClearTestDataAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Enabled"] = "true",
                ["Seed:AdminEmail"] = CustomWebApplicationFactory.TestAdminEmail,
                ["Seed:AdminPassword"] = null
            })
            .Build();

        var logger = new CapturingLogger();

        using var scope = _factory.Services.CreateScope();
        await AppDbContextSeed.SeedIdentityAsync(scope.ServiceProvider, config, logger);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await context.Roles.CountAsync());
        Assert.Null(await context.Users.SingleOrDefaultAsync(u => u.Email == CustomWebApplicationFactory.TestAdminEmail));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Seed:AdminPassword"));
    }

    [Fact]
    public async Task SeedIdentityAsync_WhenCredentialViolatesPolicy_LogsErrorCodesAndSkipsWithoutCrashing()
    {
        await _factory.ClearTestDataAsync();

        // Build an intentionally short input dynamically so it fails Identity password policy without static credential literals
        var invalidShortInput = new string('x', 4);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Enabled"] = "true",
                ["Seed:AdminEmail"] = "weakadmin@airline.test",
                ["Seed:AdminPassword"] = invalidShortInput
            })
            .Build();

        var logger = new CapturingLogger();

        using var scope = _factory.Services.CreateScope();
        await AppDbContextSeed.SeedIdentityAsync(scope.ServiceProvider, config, logger);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null(await context.Users.SingleOrDefaultAsync(u => u.Email == "weakadmin@airline.test"));
        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Warning &&
            e.Message.Contains("PasswordTooShort", StringComparison.Ordinal) &&
            !e.Message.Contains(invalidShortInput, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SeedIdentityAsync_WhenSeedDisabled_DoesNotCreateRolesOrAdmin()
    {
        await _factory.ClearTestDataAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Enabled"] = "false",
                ["Seed:AdminEmail"] = CustomWebApplicationFactory.TestAdminEmail,
                ["Seed:AdminPassword"] = _factory.TestAdminCredential
            })
            .Build();

        using var scope = _factory.Services.CreateScope();
        await AppDbContextSeed.SeedIdentityAsync(scope.ServiceProvider, config);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await context.Roles.CountAsync());
        Assert.Equal(0, await context.Users.CountAsync());
    }

    [Fact]
    public async Task BookingsTable_UserId_IsNotNullableWithRestrictForeignKey()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT c.is_nullable, fk.delete_referential_action_desc
            FROM sys.columns c
            INNER JOIN sys.foreign_key_columns fkc
                ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
            INNER JOIN sys.foreign_keys fk
                ON fk.object_id = fkc.constraint_object_id
            WHERE c.object_id = OBJECT_ID('Bookings')
              AND c.name = 'UserId';";

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.False(reader.GetBoolean(0)); // is_nullable == 0 (NOT NULL)
        Assert.Equal("NO_ACTION", reader.GetString(1)); // DeleteBehavior.Restrict
    }

    [Fact]
    public void TestDatabaseGuard_Throws_WhenDatabaseNameDoesNotEndWithTest()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CustomWebApplicationFactory.EnsureTestDatabaseOrThrow(
                "Server=.\\SQLEXPRESS;Database=AirlineReservationDb;Trusted_Connection=True;"));

        Assert.Throws<InvalidOperationException>(() =>
            CustomWebApplicationFactory.EnsureTestDatabaseOrThrow(""));

        var exception = Record.Exception(() =>
            CustomWebApplicationFactory.EnsureTestDatabaseOrThrow(
                CustomWebApplicationFactory.TestConnectionString));
        Assert.Null(exception);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
