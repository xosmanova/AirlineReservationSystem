using AirlineReservation.Application.Common.Interfaces;

namespace AirlineReservation.Application.Common.Services;

/// <summary>
/// Temporary always-successful payment gateway stub for Day 16 checkout engine.
/// A full simulation gateway with Luhn validation and rejection cards will be introduced in Day 17.
/// </summary>
public class StubPaymentGateway : IPaymentGateway
{
    public Task<PaymentProcessResult> ProcessPaymentAsync(PaymentProcessRequest request, CancellationToken cancellationToken = default)
    {
        var transactionRef = $"TX-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        return Task.FromResult(new PaymentProcessResult(true, transactionRef, null));
    }
}
