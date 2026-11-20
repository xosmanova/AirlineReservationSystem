using System.Security.Cryptography;
using AirlineReservation.Application.Common.Interfaces;

namespace AirlineReservation.Application.Common.Generators;

/// <summary>
/// Cryptographically secure Passenger Name Record (PNR) generator.
/// Generates 6-character alphanumeric codes over an unambiguous 32-character alphabet
/// (A-Z excluding 'I' and 'O', and digits 2-9 excluding '0' and '1').
/// </summary>
public class PnrGenerator : IPnrGenerator
{
    public const int PnrLength = 6;
    public const string AllowedCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private static readonly char[] Alphabet = AllowedCharacters.ToCharArray();

    /// <summary>
    /// Generates a 6-character PNR code using RandomNumberGenerator.GetItems.
    /// </summary>
    public string Generate()
    {
        return new string(RandomNumberGenerator.GetItems(Alphabet, PnrLength));
    }
}
