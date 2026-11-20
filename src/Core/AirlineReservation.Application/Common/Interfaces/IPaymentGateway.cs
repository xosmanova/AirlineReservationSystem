namespace AirlineReservation.Application.Common.Interfaces;

/// <summary>
/// Request parameters for processing a payment.
/// Overrides ToString() to guarantee card number and CVV are masked/redacted in logs.
/// </summary>
public record PaymentProcessRequest(
    decimal Amount,
    string Currency,
    string CardNumber,
    string ExpiryMonthYear,
    string Cvv,
    string CardHolderName
)
{
    public sealed override string ToString()
    {
        var maskedCard = MaskCardNumber(CardNumber);
        return $"PaymentProcessRequest {{ Amount = {Amount}, Currency = {Currency}, CardNumber = {maskedCard}, ExpiryMonthYear = {ExpiryMonthYear}, Cvv = ***, CardHolderName = {CardHolderName} }}";
    }

    private static string MaskCardNumber(string? cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber)) return "[EMPTY]";
        var clean = cardNumber.Replace(" ", "").Replace("-", "");
        if (clean.Length < 4) return "****";
        return $"**** **** **** {clean[^4..]}";
    }
}

/// <summary>
/// Result returned after attempting to process a payment.
/// </summary>
public record PaymentProcessResult(
    bool Success,
    string? TransactionReference,
    string? ErrorMessage
);

/// <summary>
/// Request parameters for reversing (compensating) a previously authorized payment.
/// </summary>
public record PaymentReversalRequest(
    string TransactionReference,
    decimal Amount,
    string Currency = "AZN"
);

/// <summary>
/// Result returned after attempting to reverse a payment.
/// </summary>
public record PaymentReversalResult(
    bool Success,
    string? ErrorMessage
);

/// <summary>
/// Gateway abstraction for processing customer payment transactions and reversals.
/// </summary>
public interface IPaymentGateway
{
    Task<PaymentProcessResult> ProcessPaymentAsync(PaymentProcessRequest request, CancellationToken cancellationToken = default);

    Task<PaymentReversalResult> ReverseAsync(PaymentReversalRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new PaymentReversalResult(true, null));
}
