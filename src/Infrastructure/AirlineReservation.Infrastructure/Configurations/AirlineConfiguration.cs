using AirlineReservation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineReservation.Infrastructure.Configurations;

public class AirlineConfiguration : IEntityTypeConfiguration<Airline>
{
    public void Configure(EntityTypeBuilder<Airline> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Code)
            .IsRequired()
            .HasMaxLength(5);

        builder.HasIndex(a => a.Code)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.LogoUrl)
            .HasMaxLength(300);

        builder.HasMany(a => a.Aircrafts)
            .WithOne(ac => ac.Airline)
            .HasForeignKey(ac => ac.AirlineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Flights)
            .WithOne(f => f.Airline)
            .HasForeignKey(f => f.AirlineId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
