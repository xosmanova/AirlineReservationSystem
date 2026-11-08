using AirlineReservation.Application.Common.Behaviors;
using AirlineReservation.Application.Common.Exceptions;
using FluentValidation;
using MediatR;
using Xunit;

namespace AirlineReservation.Application.Tests.Behaviors;

public class ValidationBehaviorTests
{
    public sealed record TestCommand(string Name) : IRequest<string>;

    public class TestCommandValidator : AbstractValidator<TestCommand>
    {
        public TestCommandValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MinimumLength(3).WithMessage("Name must be at least 3 characters.");
        }
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ThrowsAppValidationExceptionWithErrors()
    {
        // Arrange
        var validators = new IValidator<TestCommand>[] { new TestCommandValidator() };
        var behavior = new ValidationBehavior<TestCommand, string>(validators);
        var invalidCommand = new TestCommand("");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppValidationException>(() =>
            behavior.Handle(invalidCommand, (ct) => Task.FromResult("Success"), CancellationToken.None));

        Assert.Contains(nameof(TestCommand.Name), ex.Errors.Keys);
        Assert.NotEmpty(ex.Errors[nameof(TestCommand.Name)]);
    }

    [Fact]
    public async Task Handle_WhenValidationPasses_CallsNextDelegate()
    {
        // Arrange
        var validators = new IValidator<TestCommand>[] { new TestCommandValidator() };
        var behavior = new ValidationBehavior<TestCommand, string>(validators);
        var validCommand = new TestCommand("ValidName");

        // Act
        var result = await behavior.Handle(validCommand, (ct) => Task.FromResult("Success"), CancellationToken.None);

        // Assert
        Assert.Equal("Success", result);
    }
}
