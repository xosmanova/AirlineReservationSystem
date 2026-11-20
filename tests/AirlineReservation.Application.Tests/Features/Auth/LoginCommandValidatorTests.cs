using AirlineReservation.Application.Features.Auth.Commands.Login;

namespace AirlineReservation.Application.Tests.Features.Auth;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCredentialsPayload_Succeeds()
    {
        var inputSecret = $"Test!{Guid.NewGuid():N}aA1";
        var command = new LoginCommand("user@airline.test", inputSecret);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-email")]
    public void Validate_InvalidEmail_Fails(string email)
    {
        var inputSecret = $"Test!{Guid.NewGuid():N}aA1";
        var command = new LoginCommand(email, inputSecret);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginCommand.Email));
    }

    [Fact]
    public void Validate_EmptyCredential_Fails()
    {
        var command = new LoginCommand("user@airline.test", string.Empty);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginCommand.Password));
    }
}
