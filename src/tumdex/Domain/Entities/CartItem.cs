using Core.Persistence.Repositories;

namespace Domain;

public class CartItem: Entity<string>
{
    public string CartId { get; set; }
    public string ProductId { get; set; }
    public Cart Cart { get; set; }
    public Product Product { get; set; }
    public int Quantity { get; set; }
    public bool IsChecked { get; set; }
    public ICollection<ProductImageFile> ProductImageFiles { get; set; }
    
    public CartItem() : base("CartItem")
    {
        ProductImageFiles = new List<ProductImageFile>();
    }
}