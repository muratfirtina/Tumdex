using FluentValidation;

namespace Application.Features.Products.Commands.Update;

public class UpdateProductCommandValidation : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidation()
    {
        RuleFor(p => p.Name).NotEmpty();
        RuleFor(p => p.CategoryId).NotEmpty();
        RuleFor(p => p.BrandId).NotEmpty();
        RuleFor(p => p.Id).NotEmpty();
    }
}