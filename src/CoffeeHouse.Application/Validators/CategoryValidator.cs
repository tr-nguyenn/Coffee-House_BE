using CoffeeHouse.Application.DTOs.Categories;
using FluentValidation;

namespace CoffeeHouse.Application.Validators
{
    public class CategoryValidator : AbstractValidator<CreateUpdateCategoryDto>
    {
        public CategoryValidator() {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên loại sản phẩm không được để trống")
                .MaximumLength(100).WithMessage("Tên không được vượt quá 100 ký tự");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Mô tả không được vượt quá 500 ký tự");
        }
    }
}
