using AirlineReservation.Application.Contracts.Identity;
using AirlineReservation.Application.DTOs.Auth;
using AirlineReservation.Application.Features.Auth.Commands.Login;
using AirlineReservation.Application.Features.Auth.Commands.Register;
using AirlineReservation.Application.Features.Auth.Queries.GetCurrentUser;

namespace AirlineReservation.Application.Tests.Features.Auth;

public class AuthHandlersTests
{
    private sealed class FakeAuthService : IAuthService
    {
        private readonly TimeProvider _timeProvider;
        public string? LastRegisteredEmail { get; private set; }
        public Guid? LastRequestedUserId { get; private set; }

        public FakeAuthService(TimeProvider timeProvider) => _timeProvider = timeProvider;

        public Task<AuthResponseDto> RegisterAsync(
            string email,
            string password,
            string firstName,
            string lastName,
            CancellationToken cancellationToken = default)
        {
            LastRegisteredEmail = email;
            var user = new AuthUserDto(Guid.NewGuid(), email, firstName, lastName, ["Customer"]);
            var response = new AuthResponseDto("jwt-register-token", _timeProvider.GetUtcNow().UtcDateTime.AddHours(1), user);
            return Task.FromResult(response);
        }

        public Task<AuthResponseDto> LoginAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            var user = new AuthUserDto(Guid.NewGuid(), email, "Test", "User", ["Customer"]);
            var response = new AuthResponseDto("jwt-login-token", _timeProvider.GetUtcNow().UtcDateTime.AddHours(1), user);
            return Task.FromResult(response);
        }

        public Task<AuthUserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            LastRequestedUserId = userId;
            return Task.FromResult(new AuthUserDto(userId, "user@airline.test", "Test", "User", ["Customer"]));
        }
    }

    private sealed class FixedClock(DateTimeOffset fixedUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => fixedUtc;
    }

    [Fact]
    public async Task RegisterHandler_Returns201CreatedWithCustomerRole()
    {
        var anchor = new DateTimeOffset(DateTimeOffset.UnixEpoch.UtcDateTime.AddDays(20500), TimeSpan.Zero);
        var fakeService = new FakeAuthService(new FixedClock(anchor));
        var handler = new RegisterCommandHandler(fakeService);
        var inputSecret = $"Unit!{Guid.NewGuid():N}aA1";

        var result = await handler.Handle(
            new RegisterCommand("newuser@airline.test", inputSecret, "Ali", "Aliyev"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("newuser@airline.test", fakeService.LastRegisteredEmail);
        Assert.Equal(["Customer"], result.Data.User.Roles);
        Assert.Equal(anchor.UtcDateTime.AddHours(1), result.Data.ExpiresAtUtc);
    }

    [Fact]
    public async Task LoginHandler_Returns200WithAccessToken()
    {
        var anchor = new DateTimeOffset(DateTimeOffset.UnixEpoch.UtcDateTime.AddDays(20500), TimeSpan.Zero);
        var fakeService = new FakeAuthService(new FixedClock(anchor));
        var handler = new LoginCommandHandler(fakeService);
        var inputSecret = $"Unit!{Guid.NewGuid():N}aA1";

        var result = await handler.Handle(
            new LoginCommand("user@airline.test", inputSecret),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("jwt-login-token", result.Data?.AccessToken);
    }

    [Fact]
    public async Task GetCurrentUserHandler_ReturnsUserProfile()
    {
        var anchor = new DateTimeOffset(DateTimeOffset.UnixEpoch.UtcDateTime.AddDays(20500), TimeSpan.Zero);
        var fakeService = new FakeAuthService(new FixedClock(anchor));
        var handler = new GetCurrentUserQueryHandler(fakeService);
        var userId = Guid.NewGuid();

        var result = await handler.Handle(new GetCurrentUserQuery(userId), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(userId, fakeService.LastRequestedUserId);
        Assert.Equal(userId, result.Data?.Id);
    }
}
