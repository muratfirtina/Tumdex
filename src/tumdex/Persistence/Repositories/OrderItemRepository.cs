using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;
using System;
using System.Threading.Tasks;
using Domain.Entities;

namespace Persistence.Repositories
{
    public class OrderItemRepository : EfRepositoryBase<OrderItem, string, TumdexDbContext>, IOrderItemRepository
    {
        private readonly IProductRepository _productRepository;

        public OrderItemRepository(TumdexDbContext context, IProductRepository productRepository) : base(context)
        {
            _productRepository = productRepository;
        }

        // OrderItem'ı silme ve stoğu geri yükleme işlemi
        public async Task<bool> RemoveOrderItemAsync(string? orderItemId)
        {

            try
            {
                // OrderItem'ı ve bağlı olduğu Product'ı bul
                var orderItem = await Query().Include(oi => oi.Product)
                    .FirstOrDefaultAsync(oi => oi.Id == orderItemId);

                if (orderItem == null) throw new Exception("Order Item not found.");

                // Ürünün stoğunu geri yükle
                var product = orderItem.Product;
                if (product != null)
                {
                    product.Stock += orderItem.Quantity;
                    await _productRepository.UpdateAsync(product);
                }

                // OrderItem'ı sil
                await DeleteAsync(orderItem);
                
                return true;
            }
            catch
            {
                throw;
            }
        }

        // OrderItem'ın miktarını güncelleme ve stoğu kontrol etme işlemi
        public async Task<bool> UpdateOrderItemQuantityAsync(string orderItemId, int newQuantity)
        {

            try
            {
                // OrderItem'ı ve bağlı olduğu Product'ı bul
                var orderItem = await Query().Include(oi => oi.Product)
                    .FirstOrDefaultAsync(oi => oi.Id == orderItemId);

                if (orderItem == null) throw new Exception("Order Item not found.");
                var product = orderItem.Product;
                if (product == null) throw new Exception("Product not found.");

                // Stok kontrolü: Yeni quantity mevcut stoktan fazla ise hata ver
                int stockDifference = newQuantity - orderItem.Quantity;
                if (stockDifference > product.Stock)
                {
                    throw new Exception($"Not enough stock for product {product.Name}. Available stock: {product.Stock}");
                }

                // Stok güncelleme: Artış varsa stoğu düşür, azalma varsa stoğu arttır
                product.Stock -= stockDifference;
                await _productRepository.UpdateAsync(product);

                // OrderItem'ı güncelle
                orderItem.Quantity = newQuantity;
                await UpdateAsync(orderItem);
                
                return true;
            }
            catch
            {
                throw;
            }
        }
        public async Task<bool> UpdateOrderItemDetailsAsync(string orderItemId, decimal? updatedPrice, int? leadTime)
        {

            try
            {
                var orderItem = await Query()
                    .Include(oi => oi.Product)
                    .FirstOrDefaultAsync(oi => oi.Id == orderItemId);

                if (orderItem == null) throw new Exception("Order Item not found.");

                if (updatedPrice.HasValue)
                {
                    orderItem.UpdatedPrice = updatedPrice;
                    orderItem.PriceUpdateDate = DateTime.UtcNow;
                }

                if (leadTime.HasValue)
                {
                    orderItem.LeadTime = leadTime;
                }

                await UpdateAsync(orderItem);
                return true;
            }
            catch
            {
                throw;
            }
        }
        
        public async Task<List<(string ProductId, int OrderCount)>> GetMostOrderedProductsAsync(int count)
        {
            var result = await Context.OrderItems
                .GroupBy(ci => ci.ProductId)
                .Select(g => new { ProductId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(count)
                .ToListAsync();

            return result.Select(x => (x.ProductId, x.Count)).ToList();
        }
    }
}