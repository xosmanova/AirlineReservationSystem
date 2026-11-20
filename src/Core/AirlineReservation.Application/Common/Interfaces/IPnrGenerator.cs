namespace AirlineReservation.Application.Common.Interfaces;

/// <summary>
/// Service contract for generating Passenger Name Records (PNR).
/// </summary>
public interface IPnrGenerator
{
    /// <summary>
    /// Generates a cryptographically secure 6-character PNR code over the 32-character unambiguous alphabet.
    /// </summary>
    string Generate();
}
