using System.ComponentModel.DataAnnotations.Schema;
using Core.Persistence.Repositories;

namespace Domain;

public class OrderItem : Entity<string>
{
    public string OrderId { get; set; }
    public Order Order { get; set; } // İlgili sipariş
    public string ProductId { get; set; } // Sipariş edilen ürün
    public Product Product { get; set; }
    public int Quantity { get; set; } // Sipariş edilen miktar
    public bool IsChecked { get; set; } // Ürünün seçili olup olmadığı
    public decimal? Price { get; set; } // Sabitlenen fiyat
    public string? ProductName { get; set; } // Ürün adı
    public string? ProductTitle { get; set; } // Ürünün başlığı
    public string? BrandName { get; set; } // Ürünün markası
    
    public decimal? UpdatedPrice { get; set; }  // Güncellenmiş fiyat
    public int? LeadTime { get; set; }  // Termin süresi (gün cinsinden)
    public DateTime? PriceUpdateDate { get; set; } // Fiyat güncelleme tarihi
    public ICollection<ProductImageFile> ProductImageFiles { get; set; } // Ürünün resimleri
    public ICollection<ProductFeatureValue> ProductFeatureValues { get; set; }
    
        
    public OrderItem() : base("OrderItem")
    {
        ProductImageFiles = new List<ProductImageFile>();
        ProductFeatureValues = new List<ProductFeatureValue>();
    }
}