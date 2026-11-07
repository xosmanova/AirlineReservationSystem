using AirlineReservation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineReservation.Infrastructure.Configurations;

public class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SeatNumber)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(s => s.ColumnLetter)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(s => s.BasePriceMultiplier)
            .HasColumnType("decimal(5,2)")
            .HasDefaultValue(1.0m);

        // A seat number must be unique within a given aircraft
        builder.HasIndex(s => new { s.AircraftId, s.SeatNumber })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(s => s.Aircraft)
            .WithMany(a => a.Seats)
            .HasForeignKey(s => s.AircraftId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
