using Domain.Entities;

namespace Application.Abstraction.Services;

public interface INewsletterEmailService : IEmailService
{
    string BuildProductCard(Product product, string? additionalInfo = null);
    Task<string> BuildNewsletterContent(
        IList<Product> newProducts,
        List<Product> mostLikedProducts, 
        List<Product> bestSellingProducts,
        string email);
    (string email, string guid) DecodeUnsubscribeToken(string token);
}