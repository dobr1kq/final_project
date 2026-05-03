namespace ECommerce.Application.DTOs;

public class CreateOrderDto
{
    public string CustomerEmail { get; set; } = string.Empty;
    public List<CartItemDto> Items { get; set; } = new();
}
