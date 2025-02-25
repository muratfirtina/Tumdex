using Application.Dtos.Sitemap;

namespace Application.Services;

public interface ISitemapService
{
    Task<string> GenerateSitemapIndex();
    Task<string> GenerateProductSitemap();
    Task<string> GenerateCategorySitemap();
    Task<string> GenerateBrandSitemap();
    Task<string> GenerateImageSitemap();
    Task<string> GenerateStaticPagesSitemap();
    Task<SitemapOperationResponse> SubmitSitemapsToSearchEngines();
    Task<SitemapOperationResponse> RefreshSitemaps();
    Task<SitemapMonitoringReport> GetSitemapStatus();
}