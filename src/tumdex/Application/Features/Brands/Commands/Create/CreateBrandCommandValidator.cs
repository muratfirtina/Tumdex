using FluentValidation;

namespace Application.Features.Brands.Commands.Create;

public class CreateBrandCommandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandCommandValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Brand name is required.")
            .MinimumLength(1).WithMessage("Brand name must be at least 1 characters.")
            .MaximumLength(20).WithMessage("Brand name cannot exceed 20 characters.");
    }

}