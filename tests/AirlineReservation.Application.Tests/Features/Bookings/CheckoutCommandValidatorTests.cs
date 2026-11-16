using AirlineReservation.Application.Common.Helpers;
using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Services;
using AirlineReservation.Application.DTOs.Booking;
using AirlineReservation.Application.Features.Bookings.Commands.Checkout;
using AirlineReservation.Application.Validators;
using Xunit;

namespace AirlineReservation.Application.Tests.Features.Bookings;

public class CheckoutCommandValidatorTests
{
    private static readonly TimeZoneInfo BakuTimeZone = TimeZoneHelper.ResolveTimeZone("Asia/Baku");

    // Frozen test clock: November 15, 2026 12:00:00 UTC (16:00 Baku time)
    private static readonly DateTimeOffset FrozenUtc = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly FakeClock _fakeClock = new(FrozenUtc);
    private readonly IBusinessTime _businessTime;
    private readonly CheckoutCommandValidator _validator;

    public CheckoutCommandValidatorTests()
    {
        _businessTime = new BusinessTimeService(_fakeClock, BakuTimeZone);
        var passengerValidator = new PassengerValidator(_businessTime);
        _validator = new CheckoutCommandValidator(_businessTime, passengerValidator);
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static CheckoutCommand CreateValidCommand()
    {
        return new CheckoutCommand
        {
            FlightId = 1,
            Contact = new CheckoutContactDto
            {
                Email = "customer@airline.az",
                PhoneNumber = "+994501234567"
            },
            Passengers =
            [
                new CheckoutPassengerDto
                {
                    FirstName = "Əli",
                    LastName = "Məmmədov",
                    PassportNumber = "AZE123456",
                    DateOfBirth = new DateTime(1995, 5, 20),
                    SeatId = 10
                }
            ],
            Card = new CardPaymentDto
            {
                CardNumber = "4111 2222 3333 4444",
                CardHolderName = "Ali Mammadov",
                ExpiryMonthYear = "11/26",
                Cvv = "123"
            }
        };
    }

    [Fact]
    public void Validate_ValidCommand_ReturnsTrue()
    {
        var command = CreateValidCommand();

        var result = _validator.Validate(command);

        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Theory]
    [InlineData("Əli", "Məmmədov")]
    [InlineData("Nərgiz", "Quliyeva")]
    [InlineData("Şükür", "Hüseynov")]
    [InlineData("Ömər", "Ələkbərov")]
    [InlineData("Güllü", "İsmayılova")]
    [InlineData("Çingiz", "Rüstəmov")]
    [InlineData("Jean-Luc", "O'Connor")]
    public void Validate_AzerbaijaniAndInternationalUnicodePassengerNames_ShouldPass(string firstName, string lastName)
    {
        var command = CreateValidCommand();
        command.Passengers[0].FirstName = firstName;
        command.Passengers[0].LastName = lastName;

        var result = _validator.Validate(command);

        Assert.True(result.IsValid, $"Failed for {firstName} {lastName}: {string.Join(", ", result.Errors.Select(e => e.ErrorMessage))}");
    }

    [Theory]
    [InlineData("Ali123")]
    [InlineData("Nargiz!")]
    [InlineData("<script>")]
    public void Validate_InvalidPassengerNames_ShouldFail(string invalidFirstName)
    {
        var command = CreateValidCommand();
        command.Passengers[0].FirstName = invalidFirstName;

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("FirstName"));
    }

    [Fact]
    public void Validate_DuplicateSeatIdsInPassengers_ShouldFail()
    {
        var command = CreateValidCommand();
        command.Passengers.Add(new CheckoutPassengerDto
        {
            FirstName = "Nərgiz",
            LastName = "Məmmədova",
            PassportNumber = "AZE654321",
            DateOfBirth = new DateTime(1996, 6, 21),
            SeatId = 10 // Same seat ID as first passenger
        });

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("unique seat", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_PassengerCountBoundary_ZeroPassengers_ShouldFail()
    {
        var command = CreateValidCommand();
        command.Passengers.Clear();

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CheckoutCommand.Passengers));
    }

    [Fact]
    public void Validate_PassengerCountBoundary_TenPassengers_ShouldFail()
    {
        var command = CreateValidCommand();
        for (int i = 2; i <= 10; i++)
        {
            command.Passengers.Add(new CheckoutPassengerDto
            {
                FirstName = "Passenger",
                LastName = "Test",
                PassportNumber = $"AZE00000{i}",
                DateOfBirth = new DateTime(1990, 1, 1),
                SeatId = i + 10
            });
        }
        Assert.Equal(10, command.Passengers.Count);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("between 1 and 9 passengers", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_PassengerCountBoundary_NinePassengers_ShouldPass()
    {
        var command = CreateValidCommand();
        for (int i = 2; i <= 9; i++)
        {
            command.Passengers.Add(new CheckoutPassengerDto
            {
                FirstName = "Passenger",
                LastName = "Test",
                PassportNumber = $"AZE00000{i}",
                DateOfBirth = new DateTime(1990, 1, 1),
                SeatId = i + 10
            });
        }
        Assert.Equal(9, command.Passengers.Count);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Validate_PassengerDateOfBirth_InFuture_ShouldFail()
    {
        var command = CreateValidCommand();
        command.Passengers[0].DateOfBirth = _businessTime.TodayLocal.AddDays(1);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("DateOfBirth"));
    }

    [Fact]
    public void Validate_PassengerDateOfBirth_OlderThan120Years_ShouldFail()
    {
        var command = CreateValidCommand();
        command.Passengers[0].DateOfBirth = _businessTime.TodayLocal.AddYears(-121);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("DateOfBirth"));
    }

    [Theory]
    [InlineData("not-an-email", "+994501234567")]
    [InlineData("test@domain.com", "not-a-phone")]
    [InlineData("", "+994501234567")]
    [InlineData("test@domain.com", "")]
    public void Validate_InvalidContact_ShouldFail(string email, string phone)
    {
        var command = CreateValidCommand();
        command.Contact.Email = email;
        command.Contact.PhoneNumber = phone;

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("123456789012")] // 12 digits (too short)
    [InlineData("12345678901234567890")] // 20 digits (too long)
    [InlineData("411122223333444A")] // Non-digit
    [InlineData("")]
    public void Validate_CardNumber_InvalidLengthOrFormat_ShouldFail(string cardNumber)
    {
        var command = CreateValidCommand();
        command.Card.CardNumber = cardNumber;

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("CardNumber"));
    }

    [Fact]
    public void Validate_CardNumber_ThirteenToNineteenDigits_WhitespaceTolerant_ShouldPass()
    {
        var command = CreateValidCommand();
        command.Card.CardNumber = "  4111-2222-3333-4444  ";

        var result = _validator.Validate(command);

        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Validate_CardExpiry_PastMonth_ShouldFail()
    {
        // Clock is 11/2026. 10/26 is in the past!
        var command = CreateValidCommand();
        command.Card.ExpiryMonthYear = "10/26";

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("ExpiryMonthYear"));
    }

    [Fact]
    public void Validate_CardExpiry_CurrentMonth_ShouldPass()
    {
        // Clock is 11/2026. Current month 11/26 is still valid.
        var command = CreateValidCommand();
        command.Card.ExpiryMonthYear = "11/26";

        var result = _validator.Validate(command);

        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Validate_CardExpiry_FutureMonth_ShouldPass()
    {
        var command = CreateValidCommand();
        command.Card.ExpiryMonthYear = "12/28";

        var result = _validator.Validate(command);

        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Theory]
    [InlineData("13/26")] // Month 13 invalid
    [InlineData("00/26")] // Month 00 invalid
    [InlineData("2026/11")] // Wrong format
    [InlineData("11-26")] // Wrong separator
    [InlineData("invalid")]
    public void Validate_CardExpiry_InvalidFormatOrMonth_ShouldFail(string expiry)
    {
        var command = CreateValidCommand();
        command.Card.ExpiryMonthYear = expiry;

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("ExpiryMonthYear"));
    }

    [Theory]
    [InlineData("12")] // 2 digits (too short)
    [InlineData("12345")] // 5 digits (too long)
    [InlineData("12a")] // Non-digit
    [InlineData("")]
    public void Validate_CardCvv_InvalidDigits_ShouldFail(string cvv)
    {
        var command = CreateValidCommand();
        command.Card.Cvv = cvv;

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("Cvv"));
    }

    [Theory]
    [InlineData("123")] // Standard 3-digit CVV (Visa/Mastercard)
    [InlineData("1234")] // 4-digit CVV (Amex)
    public void Validate_CardCvv_ThreeOrFourDigits_ShouldPass(string cvv)
    {
        var command = CreateValidCommand();
        command.Card.Cvv = cvv;

        var result = _validator.Validate(command);

        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Security_CardPaymentDto_ToString_MustMaskCardNumber_AndRedactCvv()
    {
        // Arrange
        const string rawCardNumber = "4111222233331234";
        const string rawCvv = "897";
        var card = new CardPaymentDto
        {
            CardNumber = rawCardNumber,
            ExpiryMonthYear = "12/27",
            CardHolderName = "John Doe",
            Cvv = rawCvv
        };

        // Act
        var stringRepresentation = card.ToString();

        // Assert
        Assert.DoesNotContain(rawCardNumber, stringRepresentation);
        Assert.DoesNotContain(rawCvv, stringRepresentation);
        Assert.Contains("**** **** **** 1234", stringRepresentation);
        Assert.Contains("Cvv = ***", stringRepresentation);
    }

    [Fact]
    public void Security_CheckoutCommand_ToString_MustNeverContainRawCardNumberOrCvv()
    {
        // Arrange
        const string rawCardNumber = "5500000000009876";
        const string rawCvv = "777";
        var command = CreateValidCommand();
        command.Card.CardNumber = rawCardNumber;
        command.Card.Cvv = rawCvv;

        // Act
        var stringRepresentation = command.ToString();

        // Assert
        Assert.DoesNotContain(rawCardNumber, stringRepresentation);
        Assert.DoesNotContain(rawCvv, stringRepresentation);
        Assert.Contains("**** **** **** 9876", stringRepresentation);
        Assert.Contains("Cvv = ***", stringRepresentation);
    }

    [Fact]
    public void Security_ValidationErrors_NeverEchoRawCardNumberOrCvv()
    {
        // Arrange: Invalid card number and invalid CVV
        const string invalidCardNumber = "4111-bad-9999-0000";
        const string invalidCvv = "99";
        var command = CreateValidCommand();
        command.Card.CardNumber = invalidCardNumber;
        command.Card.Cvv = invalidCvv;

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        foreach (var error in result.Errors)
        {
            Assert.DoesNotContain(invalidCardNumber, error.ErrorMessage);
            Assert.DoesNotContain(invalidCvv, error.ErrorMessage);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_CardHolderName_Empty_ShouldFail(string emptyName)
    {
        var command = CreateValidCommand();
        command.Card.CardHolderName = emptyName;

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("CardHolderName"));
    }

    [Fact]
    public void Validate_CardHolderName_Exceeds100Characters_ShouldFail()
    {
        var command = CreateValidCommand();
        command.Card.CardHolderName = new string('A', 101);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("CardHolderName") && e.ErrorMessage.Contains("100 characters"));
    }

    [Fact]
    public void Validate_StringProperties_ExceedingMaximumLength_ShouldFail()
    {
        // 1. Passport > 12 chars
        var cmdPassport = CreateValidCommand();
        cmdPassport.Passengers[0].PassportNumber = "AZE1234567890"; // 13 chars
        var resPassport = _validator.Validate(cmdPassport);
        Assert.False(resPassport.IsValid);
        Assert.Contains(resPassport.Errors, e => e.PropertyName.Contains("PassportNumber"));

        // 2. Email > 256 chars
        var cmdEmail = CreateValidCommand();
        cmdEmail.Contact.Email = new string('a', 250) + "@domain.com"; // > 256 chars
        var resEmail = _validator.Validate(cmdEmail);
        Assert.False(resEmail.IsValid);
        Assert.Contains(resEmail.Errors, e => e.PropertyName.Contains("Email"));

        // 3. Phone > 20 chars
        var cmdPhone = CreateValidCommand();
        cmdPhone.Contact.PhoneNumber = "+994501234567890123456"; // 22 chars
        var resPhone = _validator.Validate(cmdPhone);
        Assert.False(resPhone.IsValid);
        Assert.Contains(resPhone.Errors, e => e.PropertyName.Contains("PhoneNumber"));

        // 4. Card number > 25 chars
        var cmdCard = CreateValidCommand();
        cmdCard.Card.CardNumber = new string('4', 26);
        var resCard = _validator.Validate(cmdCard);
        Assert.False(resCard.IsValid);
        Assert.Contains(resCard.Errors, e => e.PropertyName.Contains("CardNumber"));

        // 5. CVV > 4 chars
        var cmdCvv = CreateValidCommand();
        cmdCvv.Card.Cvv = "12345";
        var resCvv = _validator.Validate(cmdCvv);
        Assert.False(resCvv.IsValid);
        Assert.Contains(resCvv.Errors, e => e.PropertyName.Contains("Cvv"));

        // 6. Expiry > 5 chars
        var cmdExpiry = CreateValidCommand();
        cmdExpiry.Card.ExpiryMonthYear = "11/2026";
        var resExpiry = _validator.Validate(cmdExpiry);
        Assert.False(resExpiry.IsValid);
        Assert.Contains(resExpiry.Errors, e => e.PropertyName.Contains("ExpiryMonthYear"));
    }
}
