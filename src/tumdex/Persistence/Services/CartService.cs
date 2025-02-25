using Application.Abstraction.Services;
using Application.Features.Carts.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Application.Repositories;
using Application.Services;
using Domain;
using Domain.Enum;
using Domain.Identity;


namespace Persistence.Services;

public class CartService : ICartService
{
    private readonly IProductRepository _productRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICartRepository _cartRepository;
    private readonly ICartItemRepository _cartItemRepository;
    private readonly IStockReservationRepository _stockReservationRepository;
    private readonly IStockReservationService _stockReservationService;

    public CartService(
        ICurrentUserService currentUserService,
        ICartRepository cartRepository,
        ICartItemRepository cartItemRepository,
        IProductRepository productRepository, IStockReservationRepository stockReservationRepository, IStockReservationService stockReservationService)
    {
        _currentUserService = currentUserService;
        _cartRepository = cartRepository;
        _cartItemRepository = cartItemRepository;
        _productRepository = productRepository;
        _stockReservationRepository = stockReservationRepository;
        _stockReservationService = stockReservationService;
    }

    private async Task<Cart> GetOrCreateCartAsync()
    {
        var userId = await _currentUserService.GetCurrentUserIdAsync();
        
        var cartWithoutOrder = await _cartRepository.GetAsync(
            predicate: c => c.UserId == userId && c.Order == null,
            include: c => c.Include(c => c.Order)
        );

        if (cartWithoutOrder == null)
        {
            cartWithoutOrder = new Cart { UserId = userId };
            await _cartRepository.AddAsync(cartWithoutOrder);
        }

        return cartWithoutOrder;
    }

    public async Task<List<CartItem?>> GetCartItemsAsync()
    {
        Cart? cart = await GetOrCreateCartAsync();
        
        var result = await _cartRepository.GetAsync(
            predicate: c => c.Id == cart.Id,
            include: c => c
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                .ThenInclude(p => p.ProductImageFiles)
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                .ThenInclude(p => p.Brand)
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                .ThenInclude(p => p.ProductFeatureValues)
                .ThenInclude(x => x.FeatureValue)
                .ThenInclude(x => x.Feature)
        );

        return result?.CartItems?.ToList() ?? new List<CartItem?>();
    }

    public async Task AddItemToCartAsync(CreateCartItemDto cartItem)
    {
        Cart? cart = await GetOrCreateCartAsync();

        var product = await _productRepository.GetAsync(predicate: p => p.Id == cartItem.ProductId);
        if (product == null)
            throw new Exception("Product not found.");

        var existingCartItem = await _cartItemRepository.GetAsync(
            predicate: ci => ci.CartId == cart.Id && ci.ProductId == cartItem.ProductId);

        int totalRequestedQuantity = cartItem.Quantity;
        if (existingCartItem != null)
            totalRequestedQuantity += existingCartItem.Quantity;

        if (totalRequestedQuantity > product.Stock)
            throw new Exception("Product stock is not enough.");

        if (existingCartItem != null)
        {
            existingCartItem.Quantity = totalRequestedQuantity;
            if (!cartItem.IsChecked)
                existingCartItem.IsChecked = false;
            await _cartItemRepository.UpdateAsync(existingCartItem);
        }
        else
        {
            await _cartItemRepository.AddAsync(new CartItem
            {
                CartId = cart.Id,
                ProductId = cartItem.ProductId,
                Quantity = cartItem.Quantity,
                IsChecked = cartItem.IsChecked
            });
        }
    }

    public async Task UpdateQuantityAsync(UpdateCartItemDto cartItem)
    {
        var _cartItem = await _cartItemRepository.GetAsync(
            predicate: ci => ci.Id == cartItem.CartItemId,
            include: i => i.Include(ci => ci.Product));
            
        if (_cartItem == null)
            throw new Exception("Cart item not found.");

        if (cartItem.Quantity > _cartItem.Product.Stock || cartItem.Quantity < 0)
            throw new Exception("Invalid quantity.");

        if (cartItem.Quantity == 0)
        {
            await _cartItemRepository.DeleteAsync(_cartItem);
        }
        else
        {
            _cartItem.Quantity = cartItem.Quantity;
            await _cartItemRepository.UpdateAsync(_cartItem);
        }
    }

    public async Task RemoveCartItemAsync(string cartItemId)
    {
        var cartItem = await _cartItemRepository.GetAsync(predicate: ci => ci.Id == cartItemId);
        if (cartItem != null)
        {
            await _cartItemRepository.DeleteAsync(cartItem);
        }
    }

    public async Task UpdateCartItemAsync(UpdateCartItemDto cartItem)
    {
        var _cartItem = await _cartItemRepository.GetAsync(predicate: ci => ci.Id == cartItem.CartItemId);
        if (_cartItem != null)
        {
            _cartItem.IsChecked = cartItem.IsChecked;
            await _cartItemRepository.UpdateAsync(_cartItem);
        }
    }

    public async Task<Cart?> GetUserActiveCart()
    {
        Cart? cart = await GetOrCreateCartAsync();

        return await _cartRepository.GetAsync(
            predicate: c => c.Id == cart.Id,
            include: c => c
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                .ThenInclude(p => p.Brand)
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                .ThenInclude(p => p.ProductImageFiles)
                .Include(c => c.User)
        );
    }

    public async Task<bool> RemoveCartAsync(string cartId)
    {
        var cart = await _cartRepository.GetAsync(predicate: c => c.Id == cartId);
        if (cart != null)
        {
            await _cartRepository.DeleteAsync(cart);
            return true;
        }
        return false;
    }

    public async Task<(string CartId, string UserId)> GetCartInfoAsync(string cartItemId)
    {
        var userId = await _currentUserService.GetCurrentUserIdAsync();
        var cartItem = await _cartItemRepository.GetAsync(
            predicate: ci => ci.Id == cartItemId,
            include: i => i.Include(ci => ci.Cart)
        );

        if (cartItem == null || cartItem.Cart == null)
            throw new Exception("Cart item or cart not found.");

        return (cartItem.CartId, userId);
    }
}