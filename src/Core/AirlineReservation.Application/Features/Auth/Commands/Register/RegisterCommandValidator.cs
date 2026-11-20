using System.Text.RegularExpressions;
using FluentValidation;

namespace AirlineReservation.Application.Features.Auth.Commands.Register;

/// <summary>
/// Validation rules enforcing email format, Unicode personal names, and ASP.NET Identity password complexity.
/// </summary>
public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    private static readonly Regex NameRegex = new(@"^[\p{L}\s\-']+$", RegexOptions.Compiled);

    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email address cannot exceed 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");

        ConfigureNameRule(RuleFor(x => x.FirstName), "First name");
        ConfigureNameRule(RuleFor(x => x.LastName), "Last name");
    }

    private static void ConfigureNameRule(IRuleBuilder<RegisterCommand, string> ruleBuilder, string fieldLabel)
    {
        ruleBuilder
            .NotEmpty().WithMessage($"{fieldLabel} is required.")
            .MaximumLength(50).WithMessage($"{fieldLabel} cannot exceed 50 characters.")
            .Must(name => !string.IsNullOrWhiteSpace(name) && NameRegex.IsMatch(name.Trim()))
            .WithMessage($"{fieldLabel} can only contain letters, spaces, hyphens, or apostrophes.");
    }
}
