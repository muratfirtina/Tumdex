namespace Application.Features.Carts.Dtos;

public class UpdateCartItemDto
{
    public string ProductId { get; set; }
    public string CartItemId { get; set; }
    public int Quantity { get; set; }
    public bool IsChecked { get; set; }=true;
}