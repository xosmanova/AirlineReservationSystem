using AirlineReservation.Application.Common.Exceptions;
using AirlineReservation.Application.Contracts.Identity;
using AirlineReservation.Application.DTOs.Auth;
using AirlineReservation.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;

namespace AirlineReservation.Infrastructure.Identity.Services;

/// <summary>
/// ASP.NET Core Identity implementation of user registration, login verification, and profile retrieval.
/// </summary>
public sealed class IdentityAuthService : IAuthService
{
    private const string CustomerRole = "Customer";

    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;
    private readonly ITokenService _tokenService;
    private readonly TimeProvider _timeProvider;

    public IdentityAuthService(
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        ITokenService tokenService,
        TimeProvider timeProvider)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<AuthResponseDto> RegisterAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default)
    {
        var normalizedInputEmail = email.Trim();

        var existingUser = await _userManager.FindByEmailAsync(normalizedInputEmail);
        if (existingUser is not null)
        {
            throw new ConflictException("A user with this email address already exists.");
        }

        if (!await _roleManager.RoleExistsAsync(CustomerRole))
        {
            await _roleManager.CreateAsync(new AppRole(CustomerRole));
        }

        var user = new AppUser
        {
            UserName = normalizedInputEmail,
            Email = normalizedInputEmail,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            EmailConfirmed = true,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            if (createResult.Errors.Any(e =>
                e.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                throw new ConflictException("A user with this email address already exists.");
            }

            var errorDict = new Dictionary<string, string[]>
            {
                ["Identity"] = createResult.Errors.Select(e => e.Description).ToArray()
            };
            throw new AppValidationException(errorDict);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, CustomerRole);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to assign '{CustomerRole}' role to newly registered user: {string.Join(", ", roleResult.Errors.Select(e => e.Code))}");
        }

        return BuildAuthResponse(user, [CustomerRole]);
    }

    public async Task<AuthResponseDto> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        var roles = (await _userManager.GetRolesAsync(user)).ToArray();
        return BuildAuthResponse(user, roles);
    }

    public async Task<AuthUserDto> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new UnauthorizedException("Authenticated user account was not found.");
        }

        var roles = (await _userManager.GetRolesAsync(user)).ToArray();
        return new AuthUserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.FirstName ?? string.Empty,
            user.LastName ?? string.Empty,
            roles);
    }

    private AuthResponseDto BuildAuthResponse(AppUser user, IReadOnlyCollection<string> roles)
    {
        var email = user.Email ?? string.Empty;
        var firstName = user.FirstName ?? string.Empty;
        var lastName = user.LastName ?? string.Empty;

        var token = _tokenService.GenerateToken(new TokenUserInfo(
            user.Id,
            email,
            firstName,
            lastName,
            roles));

        return new AuthResponseDto(
            token.AccessToken,
            token.ExpiresAtUtc,
            new AuthUserDto(user.Id, email, firstName, lastName, roles));
    }
}
