using ECommerce.Application.Abstractions;
using ECommerce.Domain;
using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Services;

public class ProductService(AppDbContext context) : IProductService
{
    public async Task<IEnumerable<Product>> GetProductsAsync(string? category, decimal? minPrice, decimal? maxPrice)
    {
        var query = context.Products.AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(p => p.Category == category);

        if (minPrice.HasValue)
            query = query.Where(p => p.Price >= minPrice.Value);

        if (maxPrice.HasValue)
            query = query.Where(p => p.Price <= maxPrice.Value);

        return await query.ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(Guid id) => 
        await context.Products.FindAsync(id);

    public async Task CreateProductAsync(Product product)
    {
        context.Products.Add(product);
        await context.SaveChangesAsync();
    }
}