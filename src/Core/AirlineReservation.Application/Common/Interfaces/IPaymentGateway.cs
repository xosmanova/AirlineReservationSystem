namespace AirlineReservation.Application.Common.Interfaces;

/// <summary>
/// Request parameters for processing a payment.
/// </summary>
public record PaymentProcessRequest(
    decimal Amount,
    string Currency,
    string CardNumber,
    string ExpiryMonthYear,
    string Cvv,
    string CardHolderName
);

/// <summary>
/// Result returned after attempting to process a payment.
/// </summary>
public record PaymentProcessResult(
    bool Success,
    string? TransactionReference,
    string? ErrorMessage
);

/// <summary>
/// Gateway abstraction for processing customer payment transactions.
/// </summary>
public interface IPaymentGateway
{
    Task<PaymentProcessResult> ProcessPaymentAsync(PaymentProcessRequest request, CancellationToken cancellationToken = default);
}
