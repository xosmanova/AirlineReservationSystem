using AirlineReservation.Application.Features.Seats.Commands.CreateSeatHold;
using FluentValidation.TestHelper;
using Xunit;

namespace AirlineReservation.Application.Tests.Features.Seats;

public class CreateSeatHoldCommandValidatorTests
{
    private readonly CreateSeatHoldCommandValidator _validator = new();
    private readonly Guid _validUserId = Guid.NewGuid();

    [Fact]
    public void Validate_ValidSingleSeatCommand_ShouldPassValidation()
    {
        var command = new CreateSeatHoldCommand(1, _validUserId, new[] { 10 });
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ValidNineSeatsCommand_ShouldPassValidation()
    {
        var seatIds = Enumerable.Range(1, 9).ToList();
        var command = new CreateSeatHoldCommand(1, _validUserId, seatIds);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptySeatList_ShouldHaveValidationError()
    {
        var command = new CreateSeatHoldCommand(1, _validUserId, Array.Empty<int>());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SeatIds)
            .WithErrorMessage("At least one seat must be selected.");
    }

    [Fact]
    public void Validate_MoreThanNineSeats_ShouldHaveValidationError()
    {
        var seatIds = Enumerable.Range(1, 10).ToList();
        var command = new CreateSeatHoldCommand(1, _validUserId, seatIds);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SeatIds)
            .WithErrorMessage("You can hold between 1 and 9 seats at a time.");
    }

    [Fact]
    public void Validate_DuplicateSeatIds_ShouldHaveValidationError()
    {
        var command = new CreateSeatHoldCommand(1, _validUserId, new[] { 5, 12, 5 });
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SeatIds)
            .WithErrorMessage("Duplicate seat selections are not allowed.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_NonPositiveSeatId_ShouldHaveValidationError(int invalidSeatId)
    {
        var command = new CreateSeatHoldCommand(1, _validUserId, new[] { invalidSeatId });
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor("SeatIds[0]")
            .WithErrorMessage("Seat ID must be greater than zero.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidFlightId_ShouldHaveValidationError(int invalidFlightId)
    {
        var command = new CreateSeatHoldCommand(invalidFlightId, _validUserId, new[] { 10 });
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.FlightId)
            .WithErrorMessage("Flight ID must be greater than zero.");
    }

    [Fact]
    public void Validate_EmptyUserId_ShouldHaveValidationError()
    {
        var command = new CreateSeatHoldCommand(1, Guid.Empty, new[] { 10 });
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("User ID must be provided.");
    }
}
