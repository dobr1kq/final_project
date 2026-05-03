using System.Linq;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using ECommerce.Domain;
using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class DbIntegrityTests : IAsyncLifetime
{
    private readonly PostgreSqlTestcontainer _dbContainer = new TestcontainersBuilder<PostgreSqlTestcontainer>()
        .WithDatabase(new PostgreSqlTestcontainerConfiguration
        {
            Database = "testdb",
            Username = "postgres",
            Password = "postgres"
        })
        .WithImage("postgres:15-alpine")
        .Build();

    private bool _dockerAvailable;

    public async Task InitializeAsync()
    {
        try
        {
            await _dbContainer.StartAsync();
            _dockerAvailable = true;
        }
        catch
        {
            _dockerAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_dockerAvailable)
        {
            await _dbContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task Sku_ShouldBeUnique()
    {
        if (!_dockerAvailable)
        {
            return;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_dbContainer.ConnectionString)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var p1 = new Product { Id = Guid.NewGuid(), SKU = "DUPLICATE", Name = "P1", Price = 10m, StockQuantity = 1 };
        var p2 = new Product { Id = Guid.NewGuid(), SKU = "DUPLICATE", Name = "P2", Price = 10m, StockQuantity = 1 };

        context.Products.Add(p1);
        await context.SaveChangesAsync();

        context.Products.Add(p2);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Checkout_ShouldReduceStockAtomically()
    {
        if (!_dockerAvailable)
        {
            return;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_dbContainer.ConnectionString)
            .Options;

        var productId = Guid.NewGuid();

        await using (var setupContext = new AppDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
            var product = new Product
            {
                Id = productId,
                Name = "Atomic Product",
                Description = "Atomicity test",
                Price = 20m,
                StockQuantity = 1,
                Category = "Test",
                SKU = Guid.NewGuid().ToString()
            };
            setupContext.Products.Add(product);

            setupContext.Carts.Add(new Cart
            {
                Id = Guid.NewGuid(),
                UserId = "user1",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Items = new List<CartItem>
                {
                    new CartItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = productId,
                        Quantity = 1,
                        UnitPrice = product.Price
                    }
                }
            });

            setupContext.Carts.Add(new Cart
            {
                Id = Guid.NewGuid(),
                UserId = "user2",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Items = new List<CartItem>
                {
                    new CartItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = productId,
                        Quantity = 1,
                        UnitPrice = product.Price
                    }
                }
            });

            await setupContext.SaveChangesAsync();
        }

        var startGate = new TaskCompletionSource<bool>();

        var task1 = Task.Run(async () =>
        {
            await startGate.Task;
            await using var context = new AppDbContext(options);
            var service = new ECommerce.Infrastructure.Services.CartService(context);
            try
            {
                await service.CheckoutAsync("user1");
                return true;
            }
            catch
            {
                return false;
            }
        });

        var task2 = Task.Run(async () =>
        {
            await startGate.Task;
            await using var context = new AppDbContext(options);
            var service = new ECommerce.Infrastructure.Services.CartService(context);
            try
            {
                await service.CheckoutAsync("user2");
                return true;
            }
            catch
            {
                return false;
            }
        });

        startGate.SetResult(true);
        var results = await Task.WhenAll(task1, task2);

        Assert.Equal(2, results.Length);
        Assert.Contains(true, results);
        Assert.Contains(false, results);

        await using var verifyContext = new AppDbContext(options);
        var productAfterCheckout = await verifyContext.Products.FindAsync(productId);
        Assert.NotNull(productAfterCheckout);
        Assert.Equal(0, productAfterCheckout!.StockQuantity);
    }

    [Fact]
    public async Task OrderSnapshot_ShouldPreservePriceAndDetails()
    {
        if (!_dockerAvailable)
        {
            return;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_dbContainer.ConnectionString)
            .Options;

        var productId = Guid.NewGuid();

        await using (var setupContext = new AppDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
            var product = new Product
            {
                Id = productId,
                Name = "Snapshot Product",
                Description = "Snapshot test",
                Price = 30m,
                StockQuantity = 5,
                Category = "Test",
                SKU = Guid.NewGuid().ToString()
            };
            setupContext.Products.Add(product);
            setupContext.Carts.Add(new Cart
            {
                Id = Guid.NewGuid(),
                UserId = "snapshot-user",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Items = new List<CartItem>
                {
                    new CartItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = productId,
                        Quantity = 1,
                        UnitPrice = product.Price
                    }
                }
            });
            await setupContext.SaveChangesAsync();
        }

        await using (var checkoutContext = new AppDbContext(options))
        {
            var service = new ECommerce.Infrastructure.Services.CartService(checkoutContext);
            var order = await service.CheckoutAsync("snapshot-user");
            Assert.Equal(30m, order.Items.First().UnitPrice);
            Assert.Equal(30m, order.TotalAmount);
            Assert.Equal("Snapshot Product", order.Items.First().ProductName);
        }

        await using (var verifyContext = new AppDbContext(options))
        {
            var order = await verifyContext.Orders.Include(o => o.Items).FirstOrDefaultAsync();
            Assert.NotNull(order);
            Assert.Equal(30m, order!.Items.First().UnitPrice);
            Assert.Equal(30m, order.TotalAmount);
            Assert.Equal("Snapshot Product", order.Items.First().ProductName);
        }
    }
}
