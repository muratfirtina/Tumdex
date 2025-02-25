using Application.Features.Carts.Dtos;
using Domain;

namespace Application.Services;

public interface ICartService
{
    Task<List<CartItem?>> GetCartItemsAsync();
    Task AddItemToCartAsync(CreateCartItemDto cartItem);
    Task UpdateQuantityAsync(UpdateCartItemDto cartItem);
    Task RemoveCartItemAsync(string cartItemId);
    Task UpdateCartItemAsync(UpdateCartItemDto cartItem);
    Task<Cart?> GetUserActiveCart();
    Task<bool> RemoveCartAsync(string cartId);
    Task<(string CartId, string UserId)> GetCartInfoAsync(string cartItemId);
}