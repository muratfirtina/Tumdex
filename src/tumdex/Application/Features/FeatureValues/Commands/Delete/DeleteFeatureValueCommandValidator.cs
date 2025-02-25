using FluentValidation;

namespace Application.Features.FeatureValues.Commands.Delete;

public class DeleteFeatureValueCommandValidator : AbstractValidator<DeleteFeatureValueCommand>
{
    public DeleteFeatureValueCommandValidator()
    {
        RuleFor(p => p.Id).NotEmpty();
    }
}