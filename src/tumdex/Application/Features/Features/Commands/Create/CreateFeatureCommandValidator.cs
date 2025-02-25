using FluentValidation;

namespace Application.Features.Features.Commands.Create;

public class CreateFeatureCommandValidator : AbstractValidator<CreateFeatureCommand>
{
    public CreateFeatureCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
    }

}