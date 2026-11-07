using AirlineReservation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineReservation.Infrastructure.Configurations;

public class FlightConfiguration : IEntityTypeConfiguration<Flight>
{
    public void Configure(EntityTypeBuilder<Flight> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.FlightNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(f => f.BasePrice)
            .HasColumnType("decimal(18,2)");

        builder.HasOne(f => f.Airline)
            .WithMany(al => al.Flights)
            .HasForeignKey(f => f.AirlineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.Aircraft)
            .WithMany(ac => ac.Flights)
            .HasForeignKey(f => f.AircraftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.DepartureAirport)
            .WithMany(ap => ap.DepartingFlights)
            .HasForeignKey(f => f.DepartureAirportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.ArrivalAirport)
            .WithMany(ap => ap.ArrivingFlights)
            .HasForeignKey(f => f.ArrivalAirportId)
            .OnDelete(DeleteBehavior.Restrict);

        // Performance indexes for flight search
        builder.HasIndex(f => new { f.DepartureAirportId, f.ArrivalAirportId, f.DepartureTimeUtc });
    }
}
