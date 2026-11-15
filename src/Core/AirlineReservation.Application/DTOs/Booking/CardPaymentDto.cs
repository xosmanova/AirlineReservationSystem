namespace AirlineReservation.Application.DTOs.Booking;

/// <summary>
/// Sensitive payment card details for processing booking checkout.
/// Implements PCI DSS compliant masking so that raw card numbers and CVVs
/// are NEVER leaked in logs, exception traces, or string representations.
/// </summary>
public class CardPaymentDto
{
    public string CardNumber { get; set; } = string.Empty;
    public string ExpiryMonthYear { get; set; } = string.Empty;
    public string Cvv { get; set; } = string.Empty;
    public string CardHolderName { get; set; } = string.Empty;

    /// <summary>
    /// Returns a PCI DSS compliant masked string representation.
    /// Exposes only the last 4 digits of the card number, completely redacts CVV (***),
    /// and safely formats the expiry date and cardholder name.
    /// </summary>
    public override string ToString()
    {
        var cleaned = (CardNumber ?? string.Empty).Replace(" ", "").Replace("-", "");
        var maskedNumber = cleaned.Length >= 4
            ? $"**** **** **** {cleaned[^4..]}"
            : "****";

        return $"CardPaymentDto {{ CardNumber = {maskedNumber}, Expiry = {ExpiryMonthYear}, CardHolder = {CardHolderName}, Cvv = *** }}";
    }
}
