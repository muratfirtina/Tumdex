using Application.Abstraction.Services;
using Application.Features.Carts.Dtos;
using Application.Repositories;
using Application.Services;
using Domain;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Persistence.Services;

public class CartService : ICartService
{
    private readonly IProductRepository _productRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICartRepository _cartRepository;
    private readonly ICartItemRepository _cartItemRepository;
    private readonly IStockReservationRepository _stockReservationRepository;
    private readonly IStockReservationService _stockReservationService;
    private readonly ILogger<CartService> _logger;

    public CartService(
        ICurrentUserService currentUserService,
        ICartRepository cartRepository,
        ICartItemRepository cartItemRepository,
        IProductRepository productRepository,
        IStockReservationRepository stockReservationRepository,
        IStockReservationService stockReservationService,
        ILogger<CartService> logger)
    {
        _currentUserService = currentUserService;
        _cartRepository = cartRepository;
        _cartItemRepository = cartItemRepository;
        _productRepository = productRepository;
        _stockReservationRepository = stockReservationRepository;
        _stockReservationService = stockReservationService;
        _logger = logger;
    }

    private async Task<Cart> GetOrCreateCartAsync()
    {
        var userId = await _currentUserService.GetCurrentUserIdAsync();
        _logger.LogDebug("Getting cart for user: {UserId}", userId);

        var cartWithoutOrder = await _cartRepository.GetAsync(
            predicate: c => c.UserId == userId && c.Order == null,
            include: c => c.Include(c => c.Order)
        );

        if (cartWithoutOrder == null)
        {
            _logger.LogInformation("Creating new cart for user: {UserId}", userId);
            cartWithoutOrder = new Cart { UserId = userId };
            await _cartRepository.AddAsync(cartWithoutOrder);
        }

        return cartWithoutOrder;
    }

    public async Task<List<CartItem?>> GetCartItemsAsync()
    {
        var userId = await _currentUserService.GetCurrentUserIdAsync();
        _logger.LogDebug("Getting cart items for user: {UserId}", userId);

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
    var userId = await _currentUserService.GetCurrentUserIdAsync();
    _logger.LogInformation("Adding item to cart for user: {UserId}, Product: {ProductId}", userId,
        cartItem.ProductId);

    Cart? cart = await GetOrCreateCartAsync();
    
    var product = await _productRepository.GetAsync(predicate: p => p.Id == cartItem.ProductId);
    if (product == null)
    {
        _logger.LogWarning("Product not found: {ProductId}", cartItem.ProductId);
        throw new Exception("Product not found.");
    }

    var existingCartItem = await _cartItemRepository.GetAsync(
        predicate: ci => ci.CartId == cart.Id && ci.ProductId == cartItem.ProductId);

    int totalRequestedQuantity = cartItem.Quantity;
    if (existingCartItem != null)
        totalRequestedQuantity += existingCartItem.Quantity;

    // Sınırsız stok kontrolü - sadece bu kontrol yeterli
    if (product.Stock != Product.UnlimitedStock && totalRequestedQuantity > product.Stock)
    {
        _logger.LogWarning(
            "Insufficient stock for product: {ProductId}, Requested: {Requested}, Available: {Available}",
            cartItem.ProductId, totalRequestedQuantity, product.Stock);
        throw new Exception("Product stock is not enough.");
    }

    if (existingCartItem != null)
    {
        _logger.LogDebug("Updating existing cart item for user: {UserId}, Product: {ProductId}", userId,
            cartItem.ProductId);
        existingCartItem.Quantity = totalRequestedQuantity;
        if (!cartItem.IsChecked)
            existingCartItem.IsChecked = false;
        await _cartItemRepository.UpdateAsync(existingCartItem);
    }
    else
    {
        _logger.LogDebug("Creating new cart item for user: {UserId}, Product: {ProductId}", userId,
            cartItem.ProductId);
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
        var userId = await _currentUserService.GetCurrentUserIdAsync();
        _logger.LogInformation("Updating quantity for user: {UserId}, CartItem: {CartItemId}", userId,
            cartItem.CartItemId);

        var _cartItem = await _cartItemRepository.GetAsync(
            predicate: ci => ci.Id == cartItem.CartItemId,
            include: i => i.Include(ci => ci.Product));

        if (_cartItem == null)
        {
            _logger.LogWarning("Cart item not found: {CartItemId}", cartItem.CartItemId);
            throw new Exception("Cart item not found.");
        }

        // Sadece negatif değer kontrolü yap
        if (cartItem.Quantity < 0)
        {
            _logger.LogWarning(
                "Invalid quantity for cart item: {CartItemId}, Requested: {Requested}",
                cartItem.CartItemId, cartItem.Quantity);
            throw new Exception("Invalid quantity.");
        }

        // Sınırsız stok kontrolü
        if (_cartItem.Product.Stock != Product.UnlimitedStock && cartItem.Quantity > _cartItem.Product.Stock)
        {
            _logger.LogWarning(
                "Insufficient stock for cart item: {CartItemId}, Requested: {Requested}, Available: {Available}",
                cartItem.CartItemId, cartItem.Quantity, _cartItem.Product.Stock);
            throw new Exception("Insufficient stock.");
        }

        if (cartItem.Quantity == 0)
        {
            _logger.LogDebug("Removing cart item due to zero quantity: {CartItemId}", cartItem.CartItemId);
            await _cartItemRepository.DeleteAsync(_cartItem);
        }
        else
        {
            _logger.LogDebug("Updating cart item quantity: {CartItemId}, Quantity: {Quantity}", cartItem.CartItemId,
                cartItem.Quantity);
            _cartItem.Quantity = cartItem.Quantity;
            await _cartItemRepository.UpdateAsync(_cartItem);
        }
    }

    public async Task RemoveCartItemAsync(string cartItemId)
    {
        var userId = await _currentUserService.GetCurrentUserIdAsync();
        _logger.LogInformation("Removing cart item for user: {UserId}, CartItem: {CartItemId}", userId, cartItemId);

        var cartItem = await _cartItemRepository.GetAsync(predicate: ci => ci.Id == cartItemId);
        if (cartItem != null)
        {
            await _cartItemRepository.DeleteAsync(cartItem);
            await _stockReservationService.ReleaseReservationAsync(cartItemId);
            _logger.LogDebug("Cart item removed: {CartItemId}", cartItemId);
        }
        else
        {
            _logger.LogWarning("Cart item not found for removal: {CartItemId}", cartItemId);
        }
    }

    public async Task UpdateCartItemAsync(UpdateCartItemDto cartItem)
    {
        var userId = await _currentUserService.GetCurrentUserIdAsync();
        _logger.LogInformation("Updating cart item for user: {UserId}, CartItem: {CartItemId}", userId,
            cartItem.CartItemId);

        var _cartItem = await _cartItemRepository.GetAsync(predicate: ci => ci.Id == cartItem.CartItemId);
        if (_cartItem != null)
        {
            _cartItem.IsChecked = cartItem.IsChecked;
            await _cartItemRepository.UpdateAsync(_cartItem);
            _logger.LogDebug("Cart item updated: {CartItemId}, IsChecked: {IsChecked}", cartItem.CartItemId,
                cartItem.IsChecked);
        }
        else
        {
            _logger.LogWarning("Cart item not found for update: {CartItemId}", cartItem.CartItemId);
        }
    }

    public async Task<Cart?> GetUserActiveCart()
    {
        var userId = await _currentUserService.GetCurrentUserIdAsync();
        _logger.LogDebug("Getting active cart for user: {UserId}", userId);

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
        var userId = await _currentUserService.GetCurrentUserIdAsync();
        _logger.LogInformation("Removing cart for user: {UserId}, Cart: {CartId}", userId, cartId);

        var cart = await _cartRepository.GetAsync(predicate: c => c.Id == cartId);
        if (cart != null)
        {
            // Security check - only allow removing own carts
            if (cart.UserId != userId)
            {
                _logger.LogWarning(
                    "Unauthorized attempt to remove cart: {CartId} by user: {UserId}, owner is: {OwnerId}",
                    cartId, userId, cart.UserId);
                return false;
            }

            await _cartRepository.DeleteAsync(cart);
            _logger.LogDebug("Cart removed: {CartId}", cartId);
            return true;
        }

        _logger.LogWarning("Cart not found for removal: {CartId}", cartId);
        return false;
    }

    public async Task<(string CartId, string UserId)> GetCartInfoAsync(string cartItemId)
    {
        var userId = await _currentUserService.GetCurrentUserIdAsync();
        _logger.LogDebug("Getting cart info for cartItem: {CartItemId}, user: {UserId}", cartItemId, userId);

        var cartItem = await _cartItemRepository.GetAsync(
            predicate: ci => ci.Id == cartItemId,
            include: i => i.Include(ci => ci.Cart)
        );

        if (cartItem == null || cartItem.Cart == null)
        {
            _logger.LogWarning("Cart item or cart not found: {CartItemId}", cartItemId);
            throw new Exception("Cart item or cart not found.");
        }

        // Security check - only allow access to own cart items
        if (cartItem.Cart.UserId != userId)
        {
            _logger.LogWarning(
                "Unauthorized attempt to access cart item: {CartItemId} by user: {UserId}, owner is: {OwnerId}",
                cartItemId, userId, cartItem.Cart.UserId);
            throw new Exception("Unauthorized access to cart item.");
        }

        return (cartItem.CartId, userId);
    }
}