using FluentValidation;

namespace Application.Features.Features.Commands.Update;

public class UpdateFeatureCommandValidation : AbstractValidator<UpdateFeatureCommand>
{
    public UpdateFeatureCommandValidation()
    {
        RuleFor(p => p.Name).NotEmpty();
        RuleFor(p => p.Name).NotNull();
        RuleFor(p => p.Id).NotEmpty();
    }
}