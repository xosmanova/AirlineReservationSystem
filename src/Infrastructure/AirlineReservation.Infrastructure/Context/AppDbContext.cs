using System.Linq.Expressions;
using AirlineReservation.Domain.Common;
using AirlineReservation.Domain.Entities;
using AirlineReservation.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AirlineReservation.Infrastructure.Context;

public class AppDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Airport> Airports => Set<Airport>();
    public DbSet<Airline> Airlines => Set<Airline>();
    public DbSet<Aircraft> Aircrafts => Set<Aircraft>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<Passenger> Passengers => Set<Passenger>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Global query filter for soft delete across every BaseEntity-derived type (skips Identity entities)
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
                var notDeleted = Expression.Not(property);
                var lambda = Expression.Lambda(notDeleted, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
