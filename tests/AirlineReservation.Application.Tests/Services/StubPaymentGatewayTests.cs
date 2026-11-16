using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Common.Services;
using AirlineReservation.Application.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AirlineReservation.Application.Tests.Services;

public class StubPaymentGatewayTests
{
    private readonly IPaymentGateway _sut = new StubPaymentGateway();

    [Fact]
    public async Task ProcessPaymentAsync_ShouldReturnSuccessWithValidTransactionReference()
    {
        // Arrange
        var request = new PaymentProcessRequest(
            Amount: 150.00m,
            Currency: "USD",
            CardNumber: "4111222233334444",
            ExpiryMonthYear: "12/28",
            Cvv: "123",
            CardHolderName: "John Doe"
        );

        // Act
        var result = await _sut.ProcessPaymentAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.TransactionReference);
        Assert.StartsWith("TX-", result.TransactionReference);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void StubPaymentGateway_ShouldBeRegisteredInServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplicationServices();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IPaymentGateway));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(StubPaymentGateway), descriptor.ImplementationType);
    }
}
