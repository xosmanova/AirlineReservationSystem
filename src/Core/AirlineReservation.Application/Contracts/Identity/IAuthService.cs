using AirlineReservation.Application.DTOs.Auth;

namespace AirlineReservation.Application.Contracts.Identity;

/// <summary>
/// Abstraction over user registration, credential login, and authenticated profile lookup.
/// Keeps Application layer decoupled from ASP.NET Core Identity.
/// </summary>
public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default);

    Task<AuthResponseDto> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<AuthUserDto> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
