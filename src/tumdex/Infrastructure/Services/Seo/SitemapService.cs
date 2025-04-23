using System.Xml.Linq;
using Application.Dtos.Sitemap;
using Application.Repositories;
using Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly string _baseUrl;

    public SitemapService(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IBrandRepository brandRepository,
        IImageFileRepository imageFileRepository,
        IConfiguration configuration,
        ILogger<SitemapService> logger,
        IImageSeoService imageSeoService,
        IServiceScopeFactory serviceScopeFactory)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _brandRepository = brandRepository;
        _imageFileRepository = imageFileRepository;
        _configuration = configuration;
        _logger = logger;
        _imageSeoService = imageSeoService;
        _serviceScopeFactory = serviceScopeFactory;
        _baseUrl = _configuration["WebAPIConfiguration:APIDomain:0"];
    }

    public async Task<string> GenerateSitemapIndex()
    {
        _logger.LogInformation("Starting sitemap index generation");
        var sitemaps = new List<SitemapUrl>();
        try
        {
            // Tüm alt site haritalarını ekleyelim
            // Site ölçeğine bağlı olarak bunları dinamik olarak oluşturabiliriz
            sitemaps.Add(new SitemapUrl
            {
                Loc = $"{_baseUrl}/sitemaps/products-sitemap.xml",
                LastMod = DateTime.UtcNow
            });
            
            sitemaps.Add(new SitemapUrl
            {
                Loc = $"{_baseUrl}/sitemaps/categories-sitemap.xml",
                LastMod = DateTime.UtcNow
            });
            
            sitemaps.Add(new SitemapUrl
            {
                Loc = $"{_baseUrl}/sitemaps/brands-sitemap.xml",
                LastMod = DateTime.UtcNow
            });
            
            sitemaps.Add(new SitemapUrl
            {
                Loc = $"{_baseUrl}/sitemaps/images-sitemap.xml",
                LastMod = DateTime.UtcNow
            });
            
            sitemaps.Add(new SitemapUrl
            {
                Loc = $"{_baseUrl}/sitemaps/static-pages-sitemap.xml",
                LastMod = DateTime.UtcNow
            });

            // XML oluşturulmadan önce kontrol
            _logger.LogInformation($"Base URL: {_baseUrl}");
            _logger.LogInformation($"Total sitemaps to be included: {sitemaps.Count}");
            foreach (var sitemap in sitemaps)
            {
                _logger.LogInformation($"Sitemap URL: {sitemap.Loc}");
            }

            var xmlResult = GenerateSitemapXml(sitemaps, true);
            _logger.LogInformation($"Generated XML length: {xmlResult?.Length ?? 0}");
            
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
            new()
            {
                Loc = $"{_baseUrl}",
                ChangeFreq = ChangeFrequency.Daily,
                Priority = 1.0
            },
            new()
            {
                Loc = $"{_baseUrl}/about",
                ChangeFreq = ChangeFrequency.Monthly,
                Priority = 0.5
            },
            new()
            {
                Loc = $"{_baseUrl}/contact",
                ChangeFreq = ChangeFrequency.Monthly,
                Priority = 0.5
            },
            new()
            {
                Loc = $"{_baseUrl}/products",
                ChangeFreq = ChangeFrequency.Daily,
                Priority = 0.9
            },
            new()
            {
                Loc = $"{_baseUrl}/categories",
                ChangeFreq = ChangeFrequency.Weekly,
                Priority = 0.8
            },
            new()
            {
                Loc = $"{_baseUrl}/brands",
                ChangeFreq = ChangeFrequency.Weekly,
                Priority = 0.7
            }
            // Diğer statik sayfalar eklenebilir
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
            { "Yandex", $"http://www.yandex.com/ping?sitemap={sitemapUrl}" }
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
                    response.Errors.Add(
                        $"Failed to submit sitemap to {engine.Key}. Status: {engineResponse.StatusCode}");
                    _logger.LogWarning(
                        $"Failed to submit sitemap to {engine.Key}. Status: {engineResponse.StatusCode}");
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
        var report = new SitemapMonitoringReport();
        var sitemapTasks = new List<Task<SitemapStatus>>();
    
        // Doğrudan metot referansları yerine bir fabrika yaklaşımı kullanın
        sitemapTasks.Add(Task.Run(async () => await CheckSitemapHealth("sitemap.xml", 
            scope => scope.ServiceProvider.GetRequiredService<ISitemapService>().GenerateSitemapIndex())));
        sitemapTasks.Add(Task.Run(async () => await CheckSitemapHealth("products-sitemap.xml", 
            scope => scope.ServiceProvider.GetRequiredService<ISitemapService>().GenerateProductSitemap())));
        sitemapTasks.Add(Task.Run(async () => await CheckSitemapHealth("categories-sitemap.xml", 
            scope => scope.ServiceProvider.GetRequiredService<ISitemapService>().GenerateCategorySitemap())));
        sitemapTasks.Add(Task.Run(async () => await CheckSitemapHealth("brands-sitemap.xml", 
            scope => scope.ServiceProvider.GetRequiredService<ISitemapService>().GenerateBrandSitemap())));
        sitemapTasks.Add(Task.Run(async () => await CheckSitemapHealth("images-sitemap.xml", 
            scope => scope.ServiceProvider.GetRequiredService<ISitemapService>().GenerateImageSitemap())));
        sitemapTasks.Add(Task.Run(async () => await CheckSitemapHealth("static-pages-sitemap.xml", 
            scope => scope.ServiceProvider.GetRequiredService<ISitemapService>().GenerateStaticPagesSitemap())));
    
        var statuses = await Task.WhenAll(sitemapTasks);
    
        report.Sitemaps = statuses.ToList();
        report.TotalUrls = report.Sitemaps.Sum(s => s.UrlCount);
        report.HealthScore = CalculateHealthScore(report.Sitemaps);
        report.Issues = GenerateIssuesList(report.Sitemaps);
        report.LastFullCheck = DateTime.UtcNow;
    
        return report;
    }

    private async Task<SitemapStatus> CheckSitemapHealth(string type, Func<IServiceScope, Task<string>> generatorFactory)
    {
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var startTime = DateTime.UtcNow;
            var status = new SitemapStatus
            {
                Url = $"sitemaps/{type}",
                LastChecked = startTime,
                Errors = new List<string>()
            };
        
            try
            {
                var content = await generatorFactory(scope);
                var xmlDoc = XDocument.Parse(content);
            
                // URL sayısını belirleme - hem namespace'li hem de namespace'siz URL'leri sayma
                var sitemapNs = XNamespace.Get("http://www.sitemaps.org/schemas/sitemap/0.9");
                var urlsWithNamespace = xmlDoc.Descendants(sitemapNs + "url").Count();
                var urlsWithoutNamespace = xmlDoc.Descendants("url").Count();
                
                // Sitemap index için <sitemap> elemanlarını sayma
                var sitemapsWithNamespace = xmlDoc.Descendants(sitemapNs + "sitemap").Count();
                var sitemapsWithoutNamespace = xmlDoc.Descendants("sitemap").Count();
            
                status.IsAccessible = true;
                status.ResponseTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                status.FileSize = System.Text.Encoding.UTF8.GetByteCount(content);
                
                // URL sayısı veya sitemap sayısından birini kullan
                status.UrlCount = urlsWithNamespace > 0 ? urlsWithNamespace : urlsWithoutNamespace; 
                
                // Eğer bu bir sitemap indexse, URL sayısı yerine sitemap sayısını kullan
                if (type == "sitemap.xml" && (sitemapsWithNamespace > 0 || sitemapsWithoutNamespace > 0))
                {
                    status.UrlCount = sitemapsWithNamespace > 0 ? sitemapsWithNamespace : sitemapsWithoutNamespace;
                }
            
                // LastModified kısmı için de benzer bir yaklaşım kullanın
                var lastmodWithNs = xmlDoc.Descendants(sitemapNs + "lastmod").FirstOrDefault()?.Value;
                var lastmodWithoutNs = xmlDoc.Descendants("lastmod").FirstOrDefault()?.Value;
            
                status.LastModified = lastmodWithNs != null 
                    ? DateTime.Parse(lastmodWithNs) 
                    : (lastmodWithoutNs != null ? DateTime.Parse(lastmodWithoutNs) : null);
            }
            catch (Exception ex)
            {
                status.IsAccessible = false;
                status.Errors.Add(ex.Message);
            }

            return status;
        }
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

        return Math.Max(0, Math.Min(100, score));
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

        var root = isIndex
            ? new XElement(ns + "sitemapindex")
            : new XElement(ns + "urlset",
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