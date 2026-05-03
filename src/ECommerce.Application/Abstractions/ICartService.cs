namespace ECommerce.Application.Abstractions;

public interface ICartService
{
    // Додати товар: перевіряє StockQuantity в Domain
    Task AddItemAsync(Guid productId, int quantity);
    
    // Оновити кількість: перевіряє доступність на складі
    Task UpdateQuantityAsync(Guid productId, int quantity);
    
    // Оформлення замовлення: логіка фіксації ціни та очищення кошика
    Task<Guid> CheckoutAsync();
}