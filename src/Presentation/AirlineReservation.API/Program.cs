using AirlineReservation.API.Middlewares;
using AirlineReservation.Application.Extensions;
using AirlineReservation.Infrastructure.Context;
using AirlineReservation.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure CORS for Client Web App
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Register Application Services (MediatR CQRS, Riok.Mapperly)
builder.Services.AddApplicationServices();

// Register Infrastructure Services (EF Core, Repositories, Unit of Work)
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddOpenApi();

var app = builder.Build();

// Global Exception Handling Middleware
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// Apply migrations and seed data on startup (in Development)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        Console.WriteLine("Applying EF Core migrations...");
        await dbContext.Database.MigrateAsync();

        Console.WriteLine("Seeding initial aviation data...");
        await AppDbContextSeed.SeedAsync(dbContext, app.Configuration, app.Logger);
        Console.WriteLine("Database migrated and seeded successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database migration/seeding error: {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowClient");
app.UseAuthorization();

app.MapControllers();

app.Run();
