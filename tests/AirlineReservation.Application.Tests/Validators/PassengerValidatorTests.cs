using AirlineReservation.Application.Common.Helpers;
using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Mappings;
using AirlineReservation.Application.Common.Services;
using AirlineReservation.Application.DTOs.Passenger;
using AirlineReservation.Application.Validators;
using Xunit;

namespace AirlineReservation.Application.Tests.Validators;

public class PassengerValidatorTests
{
    private static readonly TimeZoneInfo BakuTimeZone = TimeZoneHelper.ResolveTimeZone("Asia/Baku");
    private readonly IBusinessTime _businessTime = new BusinessTimeService(TimeProvider.System, BakuTimeZone);
    private readonly PassengerValidator _validator;
    private readonly AirlineMappingProfile _mapper = new();

    public PassengerValidatorTests()
    {
        _validator = new PassengerValidator(_businessTime);
    }

    [Theory]
    [InlineData("Əli", "Məmmədov")]
    [InlineData("Nərgiz", "Quliyeva")]
    [InlineData("Şükür", "Hüseynov")]
    [InlineData("Ömər", "Ələkbərov")]
    [InlineData("Güllü", "İsmayılova")]
    [InlineData("Çingiz", "Rüstəmov")]
    [InlineData("Jean-Luc", "O'Connor")]
    public void Validator_AcceptsAzerbaijaniAndInternationalUnicodeNames(string firstName, string lastName)
    {
        // Arrange
        var dto = new PassengerCreateDto
        {
            FirstName = firstName,
            LastName = lastName,
            PassportNumber = "AZE123456",
            DateOfBirth = new DateTime(1995, 5, 20),
            Email = "passenger@test.az",
            PhoneNumber = "+994501234567",
            SeatId = 1
        };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        Assert.True(result.IsValid, $"Validation failed for {firstName} {lastName}: {string.Join(", ", result.Errors.Select(e => e.ErrorMessage))}");
    }

    [Theory]
    [InlineData("Ali123")]
    [InlineData("Nargiz!")]
    [InlineData("<script>")]
    public void Validator_RejectsInvalidCharactersInNames(string invalidName)
    {
        var dto = new PassengerCreateDto
        {
            FirstName = invalidName,
            LastName = "Aliyev",
            PassportNumber = "AZE123456",
            DateOfBirth = new DateTime(1995, 5, 20),
            Email = "test@example.com",
            PhoneNumber = "+994501234567",
            SeatId = 1
        };

        var result = _validator.Validate(dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PassengerCreateDto.FirstName));
    }

    [Theory]
    [InlineData("aze123456", "AZE123456")]
    [InlineData("c02345678", "C02345678")]
    [InlineData("P12345678", "P12345678")]
    public void Validator_AcceptsCaseInsensitivePassport_AndMapperNormalizesToUppercaseTrimmed(string rawPassport, string expectedPassport)
    {
        // Arrange
        var dto = new PassengerCreateDto
        {
            FirstName = "Əli",
            LastName = "Məmmədov",
            PassportNumber = rawPassport,
            DateOfBirth = new DateTime(1990, 1, 1),
            Email = "ali@example.com",
            PhoneNumber = "+994501234567",
            SeatId = 1
        };

        // Act - Validation
        var validationResult = _validator.Validate(dto);
        Assert.True(validationResult.IsValid, $"Passport '{rawPassport}' should be accepted case-insensitively.");

        // Act - Normalization in mapper
        var entity = _mapper.ToEntity(dto);

        // Assert
        Assert.Equal(expectedPassport, entity.PassportNumber);
    }

    [Theory]
    [InlineData(" aze123456 ", "AZE123456")]
    [InlineData("  C02345678  ", "C02345678")]
    public void Validator_AcceptsPassportWithSurroundingWhitespace_AndNormalizes(string rawPassport, string expectedPassport)
    {
        var dto = new PassengerCreateDto
        {
            FirstName = "Əli",
            LastName = "Məmmədov",
            PassportNumber = rawPassport,
            DateOfBirth = new DateTime(1990, 1, 1),
            Email = "ali@example.com",
            PhoneNumber = "+994501234567",
            SeatId = 1
        };

        var validationResult = _validator.Validate(dto);
        Assert.True(validationResult.IsValid, $"Passport '{rawPassport}' with whitespace should pass validation.");

        var entity = _mapper.ToEntity(dto);
        Assert.Equal(expectedPassport, entity.PassportNumber);
    }

    [Fact]
    public void Validator_RejectsDateOfBirthInFuture()
    {
        var futureDob = DateTime.UtcNow.Date.AddDays(2);
        var dto = new PassengerCreateDto
        {
            FirstName = "Əli",
            LastName = "Məmmədov",
            PassportNumber = "AZE123456",
            DateOfBirth = futureDob,
            Email = "test@example.com",
            PhoneNumber = "+994501234567",
            SeatId = 1
        };

        var result = _validator.Validate(dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PassengerCreateDto.DateOfBirth));
    }

    [Fact]
    public void Validator_RejectsDateOfBirthOlderThan120Years()
    {
        var ancientDob = DateTime.UtcNow.Date.AddYears(-121);
        var dto = new PassengerCreateDto
        {
            FirstName = "Əli",
            LastName = "Məmmədov",
            PassportNumber = "AZE123456",
            DateOfBirth = ancientDob,
            Email = "test@example.com",
            PhoneNumber = "+994501234567",
            SeatId = 1
        };

        var result = _validator.Validate(dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PassengerCreateDto.DateOfBirth));
    }
}
