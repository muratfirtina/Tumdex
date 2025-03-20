using Core.Persistence.Repositories;
using Domain.Entities;

public class Product : Entity<string>
{
    // Sınırsız stok için özel değer
    public const int UnlimitedStock = -1;

    public Product(string? name, string? sku) : base(name, sku)
    {
        Name = name;
        Sku = sku;
        ProductImageFiles = new List<ProductImageFile>();
        ProductFeatureValues = new List<ProductFeatureValue>();
        ProductLikes = new List<ProductLike>();
        ProductViews = new List<ProductView>();
        Stock = 0; // Varsayılan olarak 0 stok
    }

    public string Name { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? CategoryId { get; set; }
    public Category? Category { get; set; }
    public string? BrandId { get; set; }
    public Brand? Brand { get; set; }
    public string? VaryantGroupID { get; set; }
    public string? Sku { get; set; }
    public decimal? Price { get; set; }
    public int? Stock { get; set; } = 0;
    public int? Tax { get; set; }

    // Stok kontrolü için yardımcı metot
    public bool HasUnlimitedStock()
    {
        return Stock == UnlimitedStock;
    }

    // Stok kontrolü için yardımcı metot
    public bool HasSufficientStock(int requestedQuantity)
    {
        return HasUnlimitedStock() || Stock >= requestedQuantity;
    }

    public virtual ICollection<ProductImageFile>? ProductImageFiles { get; set; }
    public virtual ICollection<ProductFeatureValue>? ProductFeatureValues { get; set; }
    
    public virtual ICollection<ProductLike>? ProductLikes { get; set; }
    public virtual ICollection<ProductView>? ProductViews { get; set; }
}