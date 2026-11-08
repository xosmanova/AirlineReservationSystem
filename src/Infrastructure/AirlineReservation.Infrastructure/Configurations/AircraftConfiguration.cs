using AirlineReservation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineReservation.Infrastructure.Configurations;

public class AircraftConfiguration : IEntityTypeConfiguration<Aircraft>
{
    public void Configure(EntityTypeBuilder<Aircraft> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Model)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.TailNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(a => a.TailNumber)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(a => a.Airline)
            .WithMany(al => al.Aircrafts)
            .HasForeignKey(a => a.AirlineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Seats)
            .WithOne(s => s.Aircraft)
            .HasForeignKey(s => s.AircraftId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Flights)
            .WithOne(f => f.Aircraft)
            .HasForeignKey(f => f.AircraftId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
