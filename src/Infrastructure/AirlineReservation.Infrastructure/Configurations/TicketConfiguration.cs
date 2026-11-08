using AirlineReservation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineReservation.Infrastructure.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TicketNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.HasIndex(t => t.TicketNumber)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(t => t.Price)
            .HasColumnType("decimal(18,2)");

        // A seat cannot be double-booked for the same flight unless the previous ticket is deleted or cancelled
        builder.HasIndex(t => new { t.FlightId, t.SeatId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [IsCancelled] = 0");

        builder.HasOne(t => t.Booking)
            .WithMany(b => b.Tickets)
            .HasForeignKey(t => t.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Flight)
            .WithMany(f => f.Tickets)
            .HasForeignKey(t => t.FlightId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Passenger)
            .WithMany(p => p.Tickets)
            .HasForeignKey(t => t.PassengerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Seat)
            .WithMany(s => s.Tickets)
            .HasForeignKey(t => t.SeatId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
