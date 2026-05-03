namespace ECommerce.Application.DTOs;

public record ProductDto(Guid Id, string Name, decimal Price, int StockQuantity);