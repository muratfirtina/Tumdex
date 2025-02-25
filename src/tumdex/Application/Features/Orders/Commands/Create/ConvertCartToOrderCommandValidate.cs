using FluentValidation;

namespace Application.Features.Orders.Commands.Create;

public class ConvertCartToOrderCommandValidate : AbstractValidator<ConvertCartToOrderCommand>
{
    
    public ConvertCartToOrderCommandValidate()
    {
        RuleFor(x => x.AddressId).NotEmpty().NotNull();
        RuleFor(x => x.PhoneNumberId).NotEmpty().NotNull();
    }
}