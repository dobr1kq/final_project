using ECommerce.Application.DTOs;
using FluentValidation;

namespace ECommerce.Application.Validators
{
    public class CreateOrderDtoValidator : AbstractValidator<CreateOrderDto>
    {
        public CreateOrderDtoValidator()
        {
            RuleFor(x => x.CustomerEmail).EmailAddress().NotEmpty();
            RuleFor(x => x.Items).NotEmpty().WithMessage("Кошик не може бути порожнім");
        }
    }
}