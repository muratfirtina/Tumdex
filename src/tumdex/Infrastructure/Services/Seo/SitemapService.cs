using System.Xml.Linq;
using Application.Dtos.Sitemap;
using Application.Repositories;
using Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Seo;

public class SitemapService : ISitemapService
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IBrandRepository _brandRepository;
    private readonly IImageFileRepository _imageFileRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SitemapService> _logger;
    private readonly IImageSeoService _imageSeoService;
    private readonly string _baseUrl;

    public SitemapService(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IBrandRepository brandRepository,
        IImageFileRepository imageFileRepository,
        IConfiguration configuration,
        ILogger<SitemapService> logger, IImageSeoService imageSeoService)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _brandRepository = brandRepository;
        _imageFileRepository = imageFileRepository;
        _configuration = configuration;
        _logger = logger;
        _imageSeoService = imageSeoService;
        _baseUrl = _configuration["WebAPIConfiguration:APIDomain:0"];
    }

    public async Task<string> GenerateSitemapIndex()
{
    _logger.LogInformation("Starting sitemap index generation");
    var sitemaps = new List<SitemapUrl>();
    try
    {
        // Ürünler için kontrol ve log
        var products = await _productRepository.GetAllAsync();
        _logger.LogInformation($"Found {products.Count()} products");
        if (products.Any())
        {
            sitemaps.Add(new SitemapUrl
            {
                Loc = $"{_baseUrl}/sitemaps/products.xml",
                LastMod = DateTime.UtcNow
            });
        }

        // Kategoriler için kontrol ve log
        var categories = await _categoryRepository.GetAllAsync();
        _logger.LogInformation($"Found {categories.Count()} categories");
        if (categories.Any())
        {
            sitemaps.Add(new SitemapUrl
            {
                Loc = $"{_baseUrl}/sitemaps/categories.xml",
                LastMod = DateTime.UtcNow
            });
        }

        // Markalar için kontrol ve log
        var brands = await _brandRepository.GetAllAsync();
        _logger.LogInformation($"Found {brands.Count()} brands");
        if (brands.Any())
        {
            sitemaps.Add(new SitemapUrl
            {
                Loc = $"{_baseUrl}/sitemaps/brands.xml",
                LastMod = DateTime.UtcNow
            });
        }

        // XML oluşturulmadan önce kontrol
        _logger.LogInformation($"Base URL: {_baseUrl}");
        _logger.LogInformation($"Total sitemaps to be included: {sitemaps.Count}");
        foreach (var sitemap in sitemaps)
        {
            _logger.LogInformation($"Sitemap URL: {sitemap.Loc}");
        }

        var xmlResult = GenerateSitemapXml(sitemaps, true);
        _logger.LogInformation($"Generated XML length: {xmlResult?.Length ?? 0}");
        _logger.LogInformation($"Generated XML content: {xmlResult}");

        return xmlResult;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error generating sitemap index");
        throw;
    }
}



    public async Task<string> GenerateProductSitemap()
    {
        var products = await _productRepository.GetAllAsync();
        var sitemapUrls = new List<SitemapUrl>();

        foreach (var product in products)
        {
            var url = new SitemapUrl
            {
                Loc = $"{_baseUrl}/product/{product.Id}",
                LastMod = product.UpdatedDate ?? product.CreatedDate,
                ChangeFreq = ChangeFrequency.Daily,
                Priority = 0.8,
                Images = product.ProductImageFiles?
                    .Select(img => new SitemapImage
                    {
                        Loc = img.Url,
                        Title = img.Title ?? product.Name,
                        Caption = img.Description ?? product.Description
                    })
                    .ToList() ?? new List<SitemapImage>()
            };

            sitemapUrls.Add(url);
        }

        return GenerateSitemapXml(sitemapUrls);
    }

    public async Task<string> GenerateCategorySitemap()
    {
        var categories = await _categoryRepository.GetAllAsync();
        var sitemapUrls = categories.Select(category => new SitemapUrl
        {
            Loc = $"{_baseUrl}/category/{category.Id}",
            LastMod = category.UpdatedDate ?? category.CreatedDate,
            ChangeFreq = ChangeFrequency.Weekly,
            Priority = 0.9,
            Images = category.CategoryImageFiles?
                .Select(img => new SitemapImage
                {
                    Loc = img.Url,
                    Title = img.Title ?? category.Name
                })
                .ToList() ?? new List<SitemapImage>()
        }).ToList();

        return GenerateSitemapXml(sitemapUrls);
    }

    public async Task<string> GenerateBrandSitemap()
    {
        var brands = await _brandRepository.GetAllAsync();
        var sitemapUrls = brands.Select(brand => new SitemapUrl
        {
            Loc = $"{_baseUrl}/brand/{brand.Id}",
            LastMod = brand.UpdatedDate ?? brand.CreatedDate,
            ChangeFreq = ChangeFrequency.Weekly,
            Priority = 0.7,
            Images = brand.BrandImageFiles?
                .Select(img => new SitemapImage
                {
                    Loc = img.Url,
                    Title = img.Title ?? brand.Name
                })
                .ToList() ?? new List<SitemapImage>()
        }).ToList();

        return GenerateSitemapXml(sitemapUrls);
    }

    public async Task<string> GenerateImageSitemap()
    {
        return await _imageSeoService.GenerateImageSitemap();
    }

    public async Task<string> GenerateStaticPagesSitemap()
    {
        var staticPages = new List<SitemapUrl>
        {
            new() {
                Loc = $"{_baseUrl}",
                ChangeFreq = ChangeFrequency.Daily,
                Priority = 1.0
            },
            new() {
                Loc = $"{_baseUrl}/about",
                ChangeFreq = ChangeFrequency.Monthly,
                Priority = 0.5
            },
            new() {
                Loc = $"{_baseUrl}/contact",
                ChangeFreq = ChangeFrequency.Monthly,
                Priority = 0.5
            },
            // Diğer statik sayfalar...
        };

        return GenerateSitemapXml(staticPages);
    }

    public async Task<SitemapOperationResponse> SubmitSitemapsToSearchEngines()
    {
        var sitemapUrl = $"{_baseUrl}/sitemap.xml";
        var searchEngines = new Dictionary<string, string>
        {
            { "Google", $"http://www.google.com/ping?sitemap={sitemapUrl}" },
            { "Bing", $"http://www.bing.com/ping?sitemap={sitemapUrl}" },
            { "Yandex", $"http://www.yandex.com/ping?sitemap={sitemapUrl}" },
            { "Baidu", $"http://www.baidu.com/ping?sitemap={sitemapUrl}" },
            { "Sogou", $"http://www.sogou.com/ping?sitemap={sitemapUrl}" },
            
        };

        var response = new SitemapOperationResponse
        {
            Success = true,
            Message = "Sitemap submission completed",
            Errors = new List<string>()
        };

        using var client = new HttpClient();
        foreach (var engine in searchEngines)
        {
            try
            {
                var engineResponse = await client.GetAsync(engine.Value);
                if (engineResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Successfully submitted sitemap to {engine.Key}");
                }
                else
                {
                    response.Success = false;
                    response.Errors.Add($"Failed to submit sitemap to {engine.Key}. Status: {engineResponse.StatusCode}");
                    _logger.LogWarning($"Failed to submit sitemap to {engine.Key}. Status: {engineResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Errors.Add($"Error submitting sitemap to {engine.Key}: {ex.Message}");
                _logger.LogError(ex, $"Error submitting sitemap to {engine.Key}");
            }
        }

        if (!response.Success)
        {
            response.Message = "Some sitemap submissions failed";
        }

        return response;
    }

    public async Task<SitemapOperationResponse> RefreshSitemaps()
    {
        try
        {
            await GenerateSitemapIndex();
            await GenerateProductSitemap();
            await GenerateCategorySitemap();
            await GenerateBrandSitemap();
            await GenerateImageSitemap();
            await GenerateStaticPagesSitemap();

            return new SitemapOperationResponse
            {
                Success = true,
                Message = "All sitemaps refreshed successfully"
            };
        }
        catch (Exception ex)
        {
            return new SitemapOperationResponse
            {
                Success = false,
                Message = "Failed to refresh sitemaps",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<SitemapMonitoringReport> GetSitemapStatus()
    {
        var report = new SitemapMonitoringReport
        {
            LastFullCheck = DateTime.UtcNow,
            Sitemaps = new List<SitemapStatus>()
        };

        try
        {
            // Check each sitemap
            var tasks = new[]
            {
                CheckSitemapHealth("sitemap.xml", GenerateSitemapIndex),
                CheckSitemapHealth("products.xml", GenerateProductSitemap),
                CheckSitemapHealth("categories.xml", GenerateCategorySitemap),
                CheckSitemapHealth("brands.xml", GenerateBrandSitemap),
                CheckSitemapHealth("images.xml", GenerateImageSitemap),
                CheckSitemapHealth("static-pages.xml", GenerateStaticPagesSitemap)
            };

            var statuses = await Task.WhenAll(tasks);
            report.Sitemaps.AddRange(statuses);

            // Calculate metrics
            report.TotalUrls = report.Sitemaps.Sum(s => s.UrlCount);
            report.HealthScore = CalculateHealthScore(report.Sitemaps);
            report.Issues = GenerateIssuesList(report.Sitemaps);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sitemap status");
            throw;
        }
    }

    private async Task<SitemapStatus> CheckSitemapHealth(string type, Func<Task<string>> generator)
    {
        var startTime = DateTime.UtcNow;
        var status = new SitemapStatus
        {
            Url = $"sitemaps/{type}",
            LastChecked = startTime
        };

        try
        {
            var content = await generator();
            var xmlDoc = XDocument.Parse(content);

            status.IsAccessible = true;
            status.ResponseTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            status.FileSize = System.Text.Encoding.UTF8.GetByteCount(content);
            status.UrlCount = xmlDoc.Descendants(XName.Get("url", "http://www.sitemaps.org/schemas/sitemap/0.9")).Count();
            status.LastModified = xmlDoc.Descendants(XName.Get("lastmod", "http://www.sitemaps.org/schemas/sitemap/0.9"))
                .FirstOrDefault()?.Value != null 
                    ? DateTime.Parse(xmlDoc.Descendants(XName.Get("lastmod", "http://www.sitemaps.org/schemas/sitemap/0.9"))
                        .First().Value) 
                    : null;
        }
        catch (Exception ex)
        {
            status.IsAccessible = false;
            status.Errors.Add(ex.Message);
        }

        return status;
    }

    private int CalculateHealthScore(List<SitemapStatus> statuses)
    {
        var score = 100;

        foreach (var status in statuses)
        {
            if (!status.IsAccessible) score -= 20;
            if (status.ResponseTime > 2000) score -= 10;
            if (status.UrlCount == 0) score -= 15;
        }

        return Math.Max(0, score);
    }

    private List<SitemapIssue> GenerateIssuesList(List<SitemapStatus> statuses)
    {
        var issues = new List<SitemapIssue>();

        foreach (var status in statuses)
        {
            if (!status.IsAccessible)
            {
                issues.Add(new SitemapIssue
                {
                    Severity = SitemapIssueSeverity.High,
                    Message = $"Sitemap {status.Url} is not accessible",
                    Sitemap = status.Url
                });
            }

            if (status.ResponseTime > 2000)
            {
                issues.Add(new SitemapIssue
                {
                    Severity = SitemapIssueSeverity.Medium,
                    Message = $"Slow response time ({status.ResponseTime}ms) for {status.Url}",
                    Sitemap = status.Url
                });
            }

            if (status.UrlCount == 0)
            {
                issues.Add(new SitemapIssue
                {
                    Severity = SitemapIssueSeverity.Medium,
                    Message = $"Sitemap {status.Url} contains no URLs",
                    Sitemap = status.Url
                });
            }
        }

        return issues;
    }
    
    private string GenerateSitemapXml(List<SitemapUrl> urls, bool isIndex = false)
    {
        var ns = XNamespace.Get("http://www.sitemaps.org/schemas/sitemap/0.9");
        var imageNs = XNamespace.Get("http://www.google.com/schemas/sitemap-image/1.1");

        var root = isIndex ? new XElement(ns + "sitemapindex") : new XElement(ns + "urlset",
            new XAttribute(XNamespace.Xmlns + "image", imageNs));

        foreach (var url in urls)
        {
            var urlElement = new XElement(ns + (isIndex ? "sitemap" : "url"),
                new XElement(ns + "loc", url.Loc));

            if (url.LastMod.HasValue)
                urlElement.Add(new XElement(ns + "lastmod", 
                    url.LastMod.Value.ToString("yyyy-MM-ddTHH:mm:sszzz")));

            if (!isIndex)
            {
                if (url.ChangeFreq.HasValue)
                    urlElement.Add(new XElement(ns + "changefreq", 
                        url.ChangeFreq.Value.ToString().ToLower()));

                if (url.Priority.HasValue)
                    urlElement.Add(new XElement(ns + "priority", 
                        url.Priority.Value.ToString("0.0")));

                foreach (var image in url.Images)
                {
                    var imageElement = new XElement(imageNs + "image",
                        new XElement(imageNs + "loc", image.Loc));

                    if (!string.IsNullOrEmpty(image.Title))
                        imageElement.Add(new XElement(imageNs + "title", image.Title));

                    if (!string.IsNullOrEmpty(image.Caption))
                        imageElement.Add(new XElement(imageNs + "caption", image.Caption));

                    if (!string.IsNullOrEmpty(image.GeoLocation))
                        imageElement.Add(new XElement(imageNs + "geo_location", image.GeoLocation));

                    if (!string.IsNullOrEmpty(image.License))
                        imageElement.Add(new XElement(imageNs + "license", image.License));

                    urlElement.Add(imageElement);
                }
            }

            root.Add(urlElement);
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), root).ToString();
    }
    
}