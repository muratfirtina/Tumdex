using FluentValidation;

namespace Application.Features.Brands.Commands.Update;

public class UpdateBrandCommandValidation : AbstractValidator<UpdateBrandCommand>
{
    public UpdateBrandCommandValidation()
    {
        RuleFor(p => p.Name)
            .NotEmpty().WithMessage("Brand name is required.")
            .MinimumLength(1).WithMessage("Brand name must be at least 1 character.")
            .MaximumLength(20).WithMessage("Brand name cannot exceed 20 characters.");
            
        RuleFor(p => p.Id)
            .NotEmpty().WithMessage("Brand ID is required.");
    }
}