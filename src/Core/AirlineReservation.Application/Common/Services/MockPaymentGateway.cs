using AirlineReservation.Application.Common.Interfaces;

namespace AirlineReservation.Application.Common.Services;

/// <summary>
/// Simulated payment gateway implementing Luhn checksum validation, card expiry verification,
/// CVV format checking, and deterministic outcome triggers for designated test card numbers.
/// Card numbers and CVVs are strictly protected and never echoed into log messages or exceptions.
/// </summary>
public class MockPaymentGateway : IPaymentGateway
{
    private readonly TimeProvider _timeProvider;

    // Special test card numbers triggering deterministic gateway responses
    public const string SuccessTestCard = "4111111111111111";
    public const string InsufficientFundsTestCard = "4000000000000002";
    public const string LostOrStolenTestCard = "4000000000000010";
    public const string SuspectedFraudTestCard = "4000000000000028";
    public const string IssuerTimeoutTestCard = "4000000000000036";

    public MockPaymentGateway(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<PaymentProcessResult> ProcessPaymentAsync(
        PaymentProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Amount validation
        if (request.Amount <= 0m)
        {
            return Task.FromResult(new PaymentProcessResult(false, null, "Payment amount must be greater than zero."));
        }

        // 2. Cardholder Name validation
        if (string.IsNullOrWhiteSpace(request.CardHolderName))
        {
            return Task.FromResult(new PaymentProcessResult(false, null, "Cardholder name is required."));
        }

        // 3. Card Number sanitization and format validation
        if (string.IsNullOrWhiteSpace(request.CardNumber))
        {
            return Task.FromResult(new PaymentProcessResult(false, null, "Card number is required."));
        }

        var cleanCardNumber = request.CardNumber.Replace(" ", "").Replace("-", "");
        if (!cleanCardNumber.All(char.IsDigit) || cleanCardNumber.Length < 13 || cleanCardNumber.Length > 19)
        {
            return Task.FromResult(new PaymentProcessResult(false, null, "Invalid card number format. Must contain 13 to 19 digits."));
        }

        // 4. Luhn algorithm checksum validation (Mod 10)
        if (!IsValidLuhn(cleanCardNumber))
        {
            return Task.FromResult(new PaymentProcessResult(false, null, "Invalid card number checksum (Luhn check failed)."));
        }

        // 5. Expiry validation (MM/YY format and future date check)
        if (string.IsNullOrWhiteSpace(request.ExpiryMonthYear))
        {
            return Task.FromResult(new PaymentProcessResult(false, null, "Card expiry date is required."));
        }

        var expiryParts = request.ExpiryMonthYear.Split('/');
        if (expiryParts.Length != 2 ||
            !int.TryParse(expiryParts[0], out var expMonth) ||
            !int.TryParse(expiryParts[1], out var expYearShort) ||
            expMonth < 1 || expMonth > 12)
        {
            return Task.FromResult(new PaymentProcessResult(false, null, "Invalid card expiry date format. Expected MM/YY."));
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var currentYear = nowUtc.Year;
        var currentMonth = nowUtc.Month;
        var expYear = 2000 + expYearShort;

        if (expYear < currentYear || (expYear == currentYear && expMonth < currentMonth))
        {
            return Task.FromResult(new PaymentProcessResult(false, null, "Payment card has expired."));
        }

        if (expYear > currentYear + 25)
        {
            return Task.FromResult(new PaymentProcessResult(false, null, "Card expiry year exceeds allowable range."));
        }

        // 6. CVV format validation (3 or 4 digits)
        if (string.IsNullOrWhiteSpace(request.Cvv) ||
            (request.Cvv.Length != 3 && request.Cvv.Length != 4) ||
            !request.Cvv.All(char.IsDigit))
        {
            return Task.FromResult(new PaymentProcessResult(false, null, "Invalid CVV format. Expected 3 or 4 digits."));
        }

        // 7. Deterministic outcome triggers based on test card numbers
        switch (cleanCardNumber)
        {
            case InsufficientFundsTestCard:
                return Task.FromResult(new PaymentProcessResult(false, null, "Payment declined: Insufficient funds in account."));

            case LostOrStolenTestCard:
                return Task.FromResult(new PaymentProcessResult(false, null, "Payment declined: Card reported lost or stolen."));

            case SuspectedFraudTestCard:
                return Task.FromResult(new PaymentProcessResult(false, null, "Payment declined: Transaction declined due to suspected fraud."));

            case IssuerTimeoutTestCard:
                return Task.FromResult(new PaymentProcessResult(false, null, "Payment declined: Card issuer unreachable or network timeout."));
        }

        // 8. Successful payment simulation
        var transactionRef = $"TXN-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        return Task.FromResult(new PaymentProcessResult(true, transactionRef, null));
    }

    /// <summary>
    /// Computes the Luhn checksum (Mod 10) on a sanitized digit string.
    /// </summary>
    public static bool IsValidLuhn(string digits)
    {
        if (string.IsNullOrEmpty(digits)) return false;

        int sum = 0;
        bool alternate = false;

        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int n = digits[i] - '0';
            if (n < 0 || n > 9) return false;

            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }

            sum += n;
            alternate = !alternate;
        }

        return (sum % 10 == 0);
    }
}
