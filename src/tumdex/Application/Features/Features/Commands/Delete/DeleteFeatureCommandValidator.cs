using FluentValidation;

namespace Application.Features.Features.Commands.Delete;

public class DeleteFeatureCommandValidator : AbstractValidator<DeleteFeatureCommand>
{
    public DeleteFeatureCommandValidator()
    {
        RuleFor(p => p.Id).NotEmpty();
    }
}