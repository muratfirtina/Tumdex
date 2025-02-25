using System.Globalization;
using Application.Extensions;
using Application.Extensions.ImageFileExtensions;
using Application.Features.Carts.Dtos;
using Application.Features.Orders.Dtos;
using Application.Features.PhoneNumbers.Dtos;
using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Dtos;
using Application.Features.UserAddresses.Dtos;
using Application.Repositories;
using Application.Services;
using Application.Storage;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Dynamic;
using Core.Persistence.Paging;
using Core.Persistence.Repositories;
using Domain;
using Domain.Enum;
using Domain.Identity;
using Domain.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Persistence.Context;

namespace Persistence.Repositories;

public class OrderRepository : EfRepositoryBase<Order, string, TumdexDbContext>, IOrderRepository
{
    private readonly ICartService _cartService;
    private readonly IProductRepository _productRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<AppUser> _userManager;
    private readonly IStorageService _storageService;
    private readonly IStockReservationService _stockReservationService;
    private readonly IOrderItemRepository _orderItemRepository;
    private readonly ILogger<OrderRepository> _logger;

    public OrderRepository(TumdexDbContext context, IProductRepository productRepository, ICartService cartService,
        IHttpContextAccessor httpContextAccessor, UserManager<AppUser> userManager, IStorageService storageService,
        IOrderItemRepository orderItemRepository, IStockReservationService stockReservationService,
        ILogger<OrderRepository> logger) : base(context)
    {
        _productRepository = productRepository;
        _cartService = cartService;
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _storageService = storageService;
        _orderItemRepository = orderItemRepository;
        _stockReservationService = stockReservationService;
        _logger = logger;
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(userName))
        {
            AppUser? user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.UserName == userName);

            if (user == null)
            {
                throw new Exception("User not found.");
            }

            return user;
        }

        throw new Exception("Unexpected error occurred.");
    }

    public async Task<(bool, OrderDto)> ConvertCartToOrderAsync(string addressId, string phoneNumberId,
        string description)
    {
        try
        {
            // 1. Kullanıcının aktif sepetini al
            Cart? activeCart = await _cartService.GetUserActiveCart();
            if (activeCart == null || !activeCart.CartItems.Any())
                throw new Exception("Aktif sepet bulunamadı veya boş.");

            // 2. Seçili ürünleri ve seçili olmayan ürünleri ayır
            var selectedItems = activeCart.CartItems.Where(item => item.IsChecked).ToList();
            var unselectedItems = activeCart.CartItems.Where(item => !item.IsChecked).ToList();

            if (!selectedItems.Any())
                throw new Exception("Sepette seçili ürün yok.");

            // 3. Kullanıcı, adres ve telefon bilgilerini al
            var user = await _userManager.Users
                .Include(u => u.UserAddresses)
                .Include(u => u.PhoneNumbers)
                .FirstOrDefaultAsync(u => u.Id == activeCart.UserId);
            if (user == null)
                throw new Exception("Kullanıcı bulunamadı.");

            var selectedAddress = user.UserAddresses.FirstOrDefault(a => a.Id == addressId);
            if (selectedAddress == null)
                throw new Exception("Seçilen adres bulunamadı.");

            var selectedPhone = user.PhoneNumbers.FirstOrDefault(p => p.Id == phoneNumberId);
            if (selectedPhone == null)
                throw new Exception("Seçilen telefon numarası bulunamadı.");

            // 4. Her bir ürün için stok kontrolü ve rezervasyon işlemi
            foreach (var cartItem in selectedItems)
            {
                var product = await _productRepository.GetAsync(p => p.Id == cartItem.ProductId);
                if (product == null)
                    throw new Exception("Ürün bulunamadı.");

                // Rezervasyon kontrolü
                var hasActiveReservation = await _stockReservationService.HasActiveReservationAsync(cartItem.Id);

                if (!hasActiveReservation)
                {
                    // Rezervasyon yoksa normal stok kontrolü yap
                    if (product.Stock < cartItem.Quantity)
                        throw new Exception($"{product.Name} ürününden stokta yeterli miktarda bulunmamaktadır.");
                    product.Stock -= cartItem.Quantity;
                    await _productRepository.UpdateAsync(product);
                }
                else
                {
                    // Rezervasyon varsa ve aktifse, rezervasyonu kaldır
                    await _stockReservationService.ReleaseReservationAsync(cartItem.Id);
                }
            }


            // 5. Siparişi oluştur
            var order = new Order
            {
                UserId = activeCart.UserId,
                User = activeCart.User,
                OrderDate = DateTime.UtcNow,
                Status = OrderStatus.Pending,
                OrderCode = GenerateOrderCode(),
                UserAddressId = selectedAddress.Id,
                PhoneNumberId = selectedPhone.Id,
                Description = description,
                OrderItems = selectedItems.Select(item => new OrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.Product.Name,
                    ProductTitle = item.Product.Title,
                    Price = item.Product.Price,
                    BrandName = item.Product.Brand?.Name,
                    Quantity = item.Quantity,
                    ProductFeatureValues = item.Product.ProductFeatureValues.Select(fv => new ProductFeatureValue
                    {
                        FeatureValue = fv.FeatureValue,
                        FeatureValueId = fv.FeatureValueId
                    }).ToList(),
                    IsChecked = true
                }).ToList(),
                TotalPrice = selectedItems.Sum(item => item.Product.Price * item.Quantity)
            };

            await AddAsync(order);

            // 6. Yeni cart oluştur ve seçili olmayan ürünleri aktar
            var newCart = new Cart
            {
                UserId = activeCart.UserId,
                User = activeCart.User
            };

            // Yeni cart'ı veritabanına ekle
            await Context.Carts.AddAsync(newCart);
            await Context.SaveChangesAsync();

            // Seçili olmayan ürünleri yeni cart'a ekle
            foreach (var unselectedItem in unselectedItems)
            {
                var newCartItem = new CartItem
                {
                    CartId = newCart.Id,
                    ProductId = unselectedItem.ProductId,
                    Quantity = unselectedItem.Quantity,
                    IsChecked = false
                };
                await Context.CartItems.AddAsync(newCartItem);
            }

            // 7. Eski sepeti sil
            await _cartService.RemoveCartAsync(activeCart.Id);

            // 8. OrderDto oluştur
            var orderDto = new OrderDto
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                UserName = order.User.UserName,
                Email = user.Email,
                OrderDate = order.OrderDate,
                TotalPrice = order.TotalPrice,
                UserAddress = new UserAddressDto
                {
                    Id = selectedAddress.Id,
                    Name = selectedAddress.Name,
                    AddressLine1 = selectedAddress.AddressLine1,
                    AddressLine2 = selectedAddress.AddressLine2,
                    State = selectedAddress.State,
                    City = selectedAddress.City,
                    Country = selectedAddress.Country,
                    PostalCode = selectedAddress.PostalCode,
                    IsDefault = selectedAddress.IsDefault
                },
                PhoneNumber = new PhoneNumberDto
                {
                    Name = selectedPhone.Name,
                    Number = selectedPhone.Number,
                },
                Description = order.Description,
                OrderItems = selectedItems.Select(item => new OrderItemDto
                {
                    ProductName = item.Product.Name,
                    ProductTitle = item.Product.Title,
                    Quantity = item.Quantity,
                    Price = item.Product.Price,
                    BrandName = item.Product.Brand?.Name,
                    ProductFeatureValues = item.Product.ProductFeatureValues.Select(fv => new ProductFeatureValueDto
                    {
                        FeatureName = fv.FeatureValue.Feature.Name,
                        FeatureValueName = fv.FeatureValue.Name
                    }).ToList(),
                    ShowcaseImage = item.Product.ProductImageFiles
                        .Where(pif => pif.Showcase)
                        .Select(img => img.ToDto(_storageService))
                        .FirstOrDefault()
                }).ToList()
            };

            return (true, orderDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sipariş oluşturma işlemi sırasında hata oluştu");
            return (false, null);
        }
    }

    public async Task<bool> CompleteOrderAsync(string orderId)
    {
        // 1. Siparişi veritabanından sorgula
        var order = await Query()
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            throw new Exception("Order not found.");
        }

        // 2. Siparişin durumunu güncelle (OrderStatus.Confirmed)
        order.Status = OrderStatus.Confirmed;

        // 3. Siparişi güncelle ve kaydet
        await UpdateAsync(order);
        return true;
    }

    // Persistence/Repositories/OrderRepository.cs

    public async Task<Order> GetUserOrderByIdAsync(string orderId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            throw new Exception("User not found.");
        }

        var order = await Query()
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .ThenInclude(p => p.ProductImageFiles)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .ThenInclude(p => p.Brand)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .ThenInclude(p => p.ProductFeatureValues)
            .ThenInclude(pfv => pfv.FeatureValue)
            .ThenInclude(fv => fv.Feature)
            .Include(o => o.User)
            .Include(o => o.UserAddress)
            .Include(o => o.PhoneNumber)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == user.Id);

        if (order == null)
            throw new Exception("Order not found or you don't have permission to view this order.");

        return order;
    }

    private string GenerateOrderCode()
    {
        var orderCode = (new Random().NextDouble() * 10000).ToString(CultureInfo.InvariantCulture);
        orderCode = orderCode.Substring(orderCode.IndexOf(".", StringComparison.Ordinal) + 1,
            orderCode.Length - orderCode.IndexOf(".", StringComparison.Ordinal) - 1);
        return orderCode;
    }

    public async Task<IPaginate<Order>> GetOrdersByUserAsync(PageRequest pageRequest, OrderStatus orderStatus,
        string? dateRange, string? searchTerm)
    {
        AppUser? user = await GetCurrentUserAsync();
        if (user == null)
        {
            throw new Exception("User not found.");
        }

        var query = Context.Orders.AsQueryable();

        // Kullanıcıya göre filtrele
        query = query.Where(o => o.UserId == user.Id);

        // Tarih filtreleme
        if (!string.IsNullOrWhiteSpace(dateRange))
        {
            var currentDate = DateTime.UtcNow;

            switch (dateRange)
            {
                case "30":
                    query = query.Where(o => o.OrderDate >= currentDate.AddDays(-30));
                    break;
                case "180":
                    query = query.Where(o => o.OrderDate >= currentDate.AddDays(-180));
                    break;
                case "365":
                    query = query.Where(o => o.OrderDate >= currentDate.AddDays(-365));
                    break;
                case "older1":
                    var oneYearAgo = currentDate.AddYears(-1);
                    var twoYearsAgo = currentDate.AddYears(-2);
                    query = query.Where(o => o.OrderDate <= oneYearAgo && o.OrderDate >= twoYearsAgo);
                    break;
                case "older2":
                    var twoYearsAgo2 = currentDate.AddYears(-2);
                    var threeYearsAgo = currentDate.AddYears(-3);
                    query = query.Where(o => o.OrderDate <= twoYearsAgo2 && o.OrderDate >= threeYearsAgo);
                    break;
                case "older3":
                    var threeYearsAgo2 = currentDate.AddYears(-3);
                    query = query.Where(o => o.OrderDate <= threeYearsAgo2);
                    break;
            }
        }

        // OrderStatus gruplarına göre filtreleme
        if (orderStatus != OrderStatus.All)
        {
            switch (orderStatus)
            {
                case OrderStatus.Processing: // Devam Edenler grubu
                    query = query.Where(o =>
                        o.Status == OrderStatus.Pending ||
                        o.Status == OrderStatus.Processing ||
                        o.Status == OrderStatus.Confirmed ||
                        o.Status == OrderStatus.Shipped);
                    break;
                case OrderStatus.Cancelled: // İptal Edilenler grubu
                    query = query.Where(o =>
                        o.Status == OrderStatus.Cancelled ||
                        o.Status == OrderStatus.Rejected);
                    break;
                case OrderStatus.Returned: // İade Edilenler grubu
                    query = query.Where(o =>
                        o.Status == OrderStatus.Returned);
                    break;
                case OrderStatus.Completed: // Tamamlananlar grubu
                    query = query.Where(o =>
                        o.Status == OrderStatus.Completed);
                    break;
            }
        }

        // Arama filtresi
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var terms = searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var term in terms)
            {
                var termParam = term.ToLower();
                query = query.Where(o =>
                    EF.Functions.Like(o.Description.ToLower(), $"%{termParam}%") ||
                    EF.Functions.Like(o.OrderCode.ToLower(), $"%{termParam}%") ||
                    EF.Functions.Like(o.OrderItems.Select(oi => oi.ProductName).FirstOrDefault().ToLower(),
                        $"%{termParam}%") ||
                    EF.Functions.Like(o.OrderItems.Select(oi => oi.ProductTitle).FirstOrDefault().ToLower(),
                        $"%{termParam}%") ||
                    EF.Functions.Like(o.OrderItems.Select(oi => oi.BrandName).FirstOrDefault().ToLower(),
                        $"%{termParam}%"));
            }
        }

        query = query
            .OrderByDescending(o => o.OrderDate)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .ThenInclude(p => p.ProductImageFiles)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .ThenInclude(p => p.Brand)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .ThenInclude(p => p.ProductFeatureValues)
            .ThenInclude(pfv => pfv.FeatureValue)
            .ThenInclude(fv => fv.Feature)
            .AsSplitQuery()
            .Include(o => o.PhoneNumber)
            .Include(o => o.UserAddress);

        return await query.ToPaginateAsync(pageRequest.PageIndex, pageRequest.PageSize);
    }

    public async Task<bool> UpdateOrderWithAdminNotesAsync(
        string orderId,
        string adminNote,
        string adminUserName,
        List<(string OrderItemId, decimal? UpdatedPrice, int? LeadTime)> itemUpdates)
    {
        using var transaction = await Context.Database.BeginTransactionAsync();

        try
        {
            var order = await Query()
                .Include(o => o.OrderItems)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) throw new Exception("Order not found");

            // Update order details
            order.AdminNote = adminNote;
            order.LastModifiedBy = adminUserName;

            // Update order items
            foreach (var item in itemUpdates)
            {
                var orderItem = order.OrderItems.FirstOrDefault(oi => oi.Id == item.OrderItemId);
                if (orderItem != null)
                {
                    orderItem.UpdatedPrice = item.UpdatedPrice;
                    orderItem.LeadTime = item.LeadTime;
                    orderItem.PriceUpdateDate = DateTime.UtcNow;
                }
            }

            await UpdateAsync(order);

            // Send notification email
            /*if (order.User?.Email != null)
            {
                await _mailService.SendOrderUpdateNotificationAsync(
                    order.User.Email,
                    order.OrderCode,
                    adminNote,
                    order.OrderItems.ToList(),
                    order.TotalPrice);
            }*/

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    
    public async Task<OrderChanges> GetChanges(string orderId)
    {
        var changeTracker = Context.ChangeTracker;
        var orderEntry = changeTracker.Entries<Order>()
            .FirstOrDefault(e => e.Entity.Id == orderId);

        if (orderEntry == null)
            return new OrderChanges();

        return new OrderChanges
        {
            PreviousStatus = (OrderStatus?)orderEntry.OriginalValues["Status"],
            PreviousTotalPrice = (decimal?)orderEntry.OriginalValues["TotalPrice"],
            PreviousAdminNote = (string?)orderEntry.OriginalValues["AdminNote"],
            PreviousItems = await Context.OrderItems
                .AsNoTracking()
                .Where(i => i.OrderId == orderId)
                .ToListAsync()
        };
    }
    
    public async Task<bool> CancelOrderAsync(string orderId)
{
    try
    {
        // Siparişi tüm ilişkili verilerle birlikte getir
        var order = await Context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == orderId);
            
        if (order == null)
            return false;
            
        // Siparişi iptal et
        order.Status = OrderStatus.Cancelled;
        order.UpdatedDate = DateTime.UtcNow;
        
        // Stokları geri yükle
        foreach (var orderItem in order.OrderItems)
        {
            try
            {
                // Ürünü veritabanından getir
                var product = await _productRepository.GetAsync(p => p.Id == orderItem.ProductId);
                if (product != null)
                {
                    // Stok miktarını artır
                    product.Stock += orderItem?.Quantity ?? 0;
                    await _productRepository.UpdateAsync(product);
                    _logger.LogInformation("Ürün stoğu geri yüklendi. ProductId: {ProductId}, Quantity: {Quantity}", 
                        product.Id, orderItem.Quantity);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ürün stoğu geri yüklenirken hata oluştu. ProductId: {ProductId}", orderItem.ProductId);
                // Hata durumunda bile diğer ürünlerin işlenmesine devam et
            }
        }
        
        // Kullanıcının aktif sepetini bul
        var activeCart = await Context.Carts
            .Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.UserId == order.UserId && !c.DeletedDate.HasValue);
        
        if (activeCart == null)
        {
            // Kullanıcının aktif sepeti yoksa yeni bir tane oluştur
            activeCart = new Cart
            {
                UserId = order.UserId,
                User = order.User
            };
            await Context.Carts.AddAsync(activeCart);
            await Context.SaveChangesAsync(); // Sepeti ekle ve ID al
            _logger.LogInformation("Kullanıcı için yeni sepet oluşturuldu. UserId: {UserId}", order.UserId);
        }
        
        // Sipariş öğelerini sepete ekle
        foreach (var orderItem in order.OrderItems)
        {
            // Ürünün sepette zaten olup olmadığını kontrol et
            var existingCartItem = activeCart.CartItems
                .FirstOrDefault(ci => ci.ProductId == orderItem.ProductId);
                
            if (existingCartItem != null)
            {
                // Ürün zaten sepette varsa, miktarını artır
                existingCartItem.Quantity += orderItem?.Quantity ?? 0;
                existingCartItem.IsChecked = true; // Kullanıcı kolaylığı için seçili olarak işaretle
                Context.CartItems.Update(existingCartItem);
                _logger.LogInformation("Mevcut sepet öğesi güncellendi. CartItemId: {CartItemId}, NewQuantity: {Quantity}", 
                    existingCartItem.Id, existingCartItem.Quantity);
            }
            else
            {
                // Ürün sepette yoksa, yeni bir sepet öğesi ekle
                var newCartItem = new CartItem
                {
                    CartId = activeCart.Id,
                    ProductId = orderItem.ProductId,
                    Quantity = orderItem?.Quantity ?? 0,
                    IsChecked = true // Kullanıcı kolaylığı için seçili olarak işaretle
                };
                await Context.CartItems.AddAsync(newCartItem);
                _logger.LogInformation("Sepete yeni öğe eklendi. ProductId: {ProductId}, Quantity: {Quantity}", 
                    orderItem.ProductId, orderItem.Quantity);
            }
        }
        
        // Değişiklikleri kaydet
        await Context.SaveChangesAsync();
        _logger.LogInformation("Sipariş başarıyla iptal edildi. OrderId: {OrderId}", orderId);
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Sipariş iptal edilirken hata oluştu. OrderId: {OrderId}", orderId);
        return false;
    }
}
}