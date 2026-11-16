using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Services;
using AirlineReservation.Application.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AirlineReservation.Application.Tests.Services;

public class MockPaymentGatewayTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private readonly FakeClock _clock = new(FixedUtcNow);
    private readonly MockPaymentGateway _sut;

    public MockPaymentGatewayTests()
    {
        _sut = new MockPaymentGateway(_clock);
    }

    private static PaymentProcessRequest CreateValidRequest(
        string cardNumber = MockPaymentGateway.SuccessTestCard,
        string expiry = "12/28",
        string cvv = "123",
        decimal amount = 150.00m,
        string cardHolder = "Ali Mammadov")
    {
        return new PaymentProcessRequest(
            Amount: amount,
            Currency: "AZN",
            CardNumber: cardNumber,
            ExpiryMonthYear: expiry,
            Cvv: cvv,
            CardHolderName: cardHolder
        );
    }

    [Fact]
    public async Task ProcessPaymentAsync_ValidVisaTestCard_ReturnsSuccessWithTransactionReference()
    {
        var request = CreateValidRequest();

        var result = await _sut.ProcessPaymentAsync(request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.TransactionReference);
        Assert.StartsWith("TXN-", result.TransactionReference);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData("4111-1111-1111-1111")]
    [InlineData("4111 1111 1111 1111")]
    [InlineData("  4111111111111111  ")]
    public async Task ProcessPaymentAsync_FormattedCardNumber_StripsDelimitersAndSucceeds(string formattedCard)
    {
        var request = CreateValidRequest(cardNumber: formattedCard);

        var result = await _sut.ProcessPaymentAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(result.TransactionReference);
    }

    [Fact]
    public async Task ProcessPaymentAsync_InvalidLuhnChecksum_ReturnsFailure()
    {
        // 4111111111111112 has invalid Luhn checksum
        var request = CreateValidRequest(cardNumber: "4111111111111112");

        var result = await _sut.ProcessPaymentAsync(request);

        Assert.False(result.Success);
        Assert.Null(result.TransactionReference);
        Assert.Contains("Luhn check failed", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessPaymentAsync_InsufficientFundsTestCard_ReturnsDeclined()
    {
        var request = CreateValidRequest(cardNumber: MockPaymentGateway.InsufficientFundsTestCard);

        var result = await _sut.ProcessPaymentAsync(request);

        Assert.False(result.Success);
        Assert.Null(result.TransactionReference);
        Assert.Contains("Insufficient funds", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessPaymentAsync_LostOrStolenTestCard_ReturnsDeclined()
    {
        var request = CreateValidRequest(cardNumber: MockPaymentGateway.LostOrStolenTestCard);

        var result = await _sut.ProcessPaymentAsync(request);

        Assert.False(result.Success);
        Assert.Null(result.TransactionReference);
        Assert.Contains("lost or stolen", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessPaymentAsync_SuspectedFraudTestCard_ReturnsDeclined()
    {
        var request = CreateValidRequest(cardNumber: MockPaymentGateway.SuspectedFraudTestCard);

        var result = await _sut.ProcessPaymentAsync(request);

        Assert.False(result.Success);
        Assert.Null(result.TransactionReference);
        Assert.Contains("suspected fraud", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessPaymentAsync_IssuerTimeoutTestCard_ReturnsDeclined()
    {
        var request = CreateValidRequest(cardNumber: MockPaymentGateway.IssuerTimeoutTestCard);

        var result = await _sut.ProcessPaymentAsync(request);

        Assert.False(result.Success);
        Assert.Null(result.TransactionReference);
        Assert.Contains("timeout", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ExpiredCard_ReturnsFailure()
    {
        // November 2026 is current clock; October 2026 is past
        var request = CreateValidRequest(expiry: "10/26");

        var result = await _sut.ProcessPaymentAsync(request);

        Assert.False(result.Success);
        Assert.Null(result.TransactionReference);
        Assert.Contains("expired", result.ErrorMessage);
    }

    [Theory]
    [InlineData("12")]       // too short
    [InlineData("12345")]    // too long
    [InlineData("abc")]      // non-digit
    public async Task ProcessPaymentAsync_InvalidCvv_ReturnsFailure(string invalidCvv)
    {
        var request = CreateValidRequest(cvv: invalidCvv);

        var result = await _sut.ProcessPaymentAsync(request);

        Assert.False(result.Success);
        Assert.Contains("Invalid CVV", result.ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task ProcessPaymentAsync_ZeroOrNegativeAmount_ReturnsFailure(decimal amount)
    {
        var request = CreateValidRequest(amount: amount);

        var result = await _sut.ProcessPaymentAsync(request);

        Assert.False(result.Success);
        Assert.Contains("greater than zero", result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProcessPaymentAsync_MissingCardholderName_ReturnsFailure(string invalidName)
    {
        var request = CreateValidRequest(cardHolder: invalidName);

        var result = await _sut.ProcessPaymentAsync(request);

        Assert.False(result.Success);
        Assert.Contains("Cardholder name is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessPaymentAsync_SensitiveCardAndCvv_NeverEchoedInErrorMessage()
    {
        const string rawCard = "4111111111111112";
        const string rawCvv = "789";

        var request = CreateValidRequest(cardNumber: rawCard, cvv: rawCvv);

        var result = await _sut.ProcessPaymentAsync(request);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.DoesNotContain(rawCard, result.ErrorMessage);
        Assert.DoesNotContain(rawCvv, result.ErrorMessage);
    }

    [Fact]
    public void MockPaymentGateway_RegisteredInServiceCollection()
    {
        var services = new ServiceCollection();

        services.AddApplicationServices();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IPaymentGateway));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(MockPaymentGateway), descriptor.ImplementationType);
    }
}
