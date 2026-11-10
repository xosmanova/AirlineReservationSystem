using AirlineReservation.Domain.Entities;
using AirlineReservation.Infrastructure.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineReservation.Infrastructure.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.HasKey(b => b.Id);

        // Mandatory unique 6-character PNR code (Feature F6)
        builder.Property(b => b.PnrCode)
            .IsRequired()
            .HasMaxLength(6);

        builder.HasIndex(b => b.PnrCode)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(b => b.TotalAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(b => b.ContactEmail)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.ContactPhone)
            .IsRequired()
            .HasMaxLength(30);

        // Required relationship to AspNetUsers (Identity AppUser) with Restrict delete behavior
        builder.Property(b => b.UserId)
            .IsRequired();

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(b => b.UserId);

        builder.HasMany(b => b.Tickets)
            .WithOne(t => t.Booking)
            .HasForeignKey(t => t.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Payment)
            .WithOne(p => p.Booking)
            .HasForeignKey<Payment>(p => p.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
