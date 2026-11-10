using AirlineReservation.Application.Features.Auth.Commands.Register;

namespace AirlineReservation.Application.Tests.Features.Auth;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();
    private static string BuildValidSecret() => $"Val!{Guid.NewGuid():N}aA1";

    [Theory]
    [InlineData("Ali", "Aliyev")]
    [InlineData("Əhməd", "Həsən-Zadə")]
    [InlineData("O'Connor", "İbrahimova")]
    public void Validate_ValidInputWithUnicodeNames_Succeeds(string firstName, string lastName)
    {
        var command = new RegisterCommand("customer@airline.test", BuildValidSecret(), firstName, lastName);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    public void Validate_InvalidEmail_Fails(string email)
    {
        var command = new RegisterCommand(email, BuildValidSecret(), "Ali", "Aliyev");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCommand.Email));
    }

    [Theory]
    [InlineData("Ab1!")]             // < 8 characters
    [InlineData("lowercase1!")]      // missing uppercase
    [InlineData("UPPERCASE1!")]      // missing lowercase
    [InlineData("NoDigitsHere!")]    // missing digit
    [InlineData("NoSpecialChar1A")]  // missing non-alphanumeric
    public void Validate_WeakCredential_FailsPolicyChecks(string candidateSecret)
    {
        var command = new RegisterCommand("user@airline.test", candidateSecret, "Ali", "Aliyev");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCommand.Password));
    }

    [Theory]
    [InlineData("", "Aliyev", nameof(RegisterCommand.FirstName))]
    [InlineData("Ali123", "Aliyev", nameof(RegisterCommand.FirstName))]
    [InlineData("Ali", "", nameof(RegisterCommand.LastName))]
    [InlineData("Ali", "Aliyev!", nameof(RegisterCommand.LastName))]
    public void Validate_InvalidPersonalName_Fails(string firstName, string lastName, string expectedProperty)
    {
        var command = new RegisterCommand("user@airline.test", BuildValidSecret(), firstName, lastName);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == expectedProperty);
    }
}
