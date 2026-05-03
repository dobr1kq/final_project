using AutoFixture;
using ECommerce.Domain;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECommerce.UnitTests;

public class CartServiceTests
{
    private readonly Fixture _fixture;
    private readonly AppDbContext _context;
    private readonly CartService _cartService;

    public CartServiceTests()
    {
        _fixture = new Fixture();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new AppDbContext(options);
        _cartService = new CartService(_context);
    }

    [Fact]
    public async Task CheckoutAsync_ShouldCreateOrder_WhenStockIsSufficient()
    {
        var userId = "user-123";
        var product = _fixture.Build<Product>()
            .With(p => p.StockQuantity, 10)
            .With(p => p.Price, 100m)
            .Create();
        
        _context.Products.Add(product);
        
        var cart = new Cart { Id = Guid.NewGuid(), UserId = userId };
        cart.Items.Add(new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, ProductId = product.Id, Quantity = 2, UnitPrice = product.Price });
        _context.Carts.Add(cart);
        await _context.SaveChangesAsync();

        var order = await _cartService.CheckoutAsync(userId);

        Assert.NotNull(order);
        Assert.Equal(userId, order.UserId);
        Assert.NotEmpty(order.Items);
        Assert.Equal(2, order.Items.First().Quantity);
        Assert.Equal(100m, order.Items.First().UnitPrice);
        
        var updatedProduct = await _context.Products.FindAsync(product.Id);
        Assert.NotNull(updatedProduct);
        Assert.Equal(8, updatedProduct!.StockQuantity);
    }

    [Fact]
    public async Task CheckoutAsync_ShouldThrowException_WhenNotEnoughStock()
    {
        var userId = "user-456";
        var product = _fixture.Build<Product>()
            .With(p => p.StockQuantity, 1)
            .Create();
        
        _context.Products.Add(product);
        
        var cart = new Cart { Id = Guid.NewGuid(), UserId = userId };
        cart.Items.Add(new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, ProductId = product.Id, Quantity = 5, UnitPrice = product.Price });
        _context.Carts.Add(cart);
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _cartService.CheckoutAsync(userId));
    }

    [Fact]
    public async Task AddItemAsync_ShouldCreateItem_WhenStockAvailable()
    {
        var userId = "user-additem";
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Description = "Test",
            StockQuantity = 5,
            Price = 22.5m,
            Category = "Test",
            SKU = Guid.NewGuid().ToString()
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        await _cartService.AddItemAsync(userId, product.Id, 2);

        var cart = await _cartService.GetCartByUserIdAsync(userId);

        Assert.NotEmpty(cart.Items);
        Assert.Equal(2, cart.Items.First().Quantity);
        Assert.Equal(22.5m, cart.Items.First().UnitPrice);
    }

    [Fact]
public void CalculateTotal_ShouldSumCorrectly()
{
    var cart = new Cart();
    cart.Items.Add(new CartItem { UnitPrice = 100m, Quantity = 2 });
    cart.Items.Add(new CartItem { UnitPrice = 50.5m, Quantity = 1 });

    var total = cart.Items.Sum(i => i.UnitPrice * i.Quantity);

    Assert.Equal(250.5m, total);
}

[Fact]
public async Task Checkout_ShouldFreezePriceInSnapshot()
{
    var userId = "user-price-test";
    var product = new Product { Id = Guid.NewGuid(), Price = 99.99m, StockQuantity = 10 };
    _context.Products.Add(product);
    
    var cart = new Cart { Id = Guid.NewGuid(), UserId = userId };
    cart.Items.Add(new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, ProductId = product.Id, Quantity = 1, UnitPrice = product.Price });
    _context.Carts.Add(cart);
    await _context.SaveChangesAsync();

    product.Price = 150.00m;
    await _context.SaveChangesAsync();

    var order = await _cartService.CheckoutAsync(userId);

    var orderItem = order.Items.First();
    Assert.Equal(150.00m, orderItem.UnitPrice);

    product.Price = 200.00m;
    await _context.SaveChangesAsync();

    Assert.Equal(150.00m, orderItem.UnitPrice);
}
}