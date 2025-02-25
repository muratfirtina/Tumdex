using Application.Features.Features.Commands.Create;
using FluentValidation;

namespace Application.Features.FeatureValues.Commands.Create;

public class CreateFeatureValueCommandValidator : AbstractValidator<CreateFeatureValueCommand>
{
    public CreateFeatureValueCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
    }

}