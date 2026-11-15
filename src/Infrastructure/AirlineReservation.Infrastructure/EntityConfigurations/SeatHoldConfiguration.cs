using AirlineReservation.Domain.Entities;
using AirlineReservation.Infrastructure.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineReservation.Infrastructure.EntityConfigurations;

public class SeatHoldConfiguration : IEntityTypeConfiguration<SeatHold>
{
    public void Configure(EntityTypeBuilder<SeatHold> builder)
    {
        builder.ToTable("SeatHolds");

        builder.HasKey(h => h.Id);

        // Unfiltered unique index: database engine prevents duplicate concurrent holds for the same flight seat
        builder.HasIndex(h => new { h.FlightId, h.SeatId })
            .IsUnique();

        // Composite index for fast "my active holds" and "release flight holds" queries
        builder.HasIndex(h => new { h.FlightId, h.UserId });

        // Index for fast expired holds querying and background cleanup
        builder.HasIndex(h => h.ExpiresAtUtc);

        builder.Property(h => h.CreatedAtUtc)
            .IsRequired();

        builder.Property(h => h.ExpiresAtUtc)
            .IsRequired();

        builder.HasOne(h => h.Flight)
            .WithMany()
            .HasForeignKey(h => h.FlightId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Seat)
            .WithMany()
            .HasForeignKey(h => h.SeatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
