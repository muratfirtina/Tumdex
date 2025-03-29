using Application.Consts;
using Application.Features.Products.Dtos;
using Application.Features.Products.Rules;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Persistence.Repositories.Operation;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Products.Commands.Create;

public class CreateMultipleProductsCommand : IRequest<List<CreatedProductResponse>>, ICacheRemoverRequest
{
    public List<CreateMultipleProductDto> Products { get; set; }

    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;

    public class CreateMultipleProductsCommandHandler 
        : IRequestHandler<CreateMultipleProductsCommand, List<CreatedProductResponse>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IMapper _mapper;
        private readonly ProductBusinessRules _productBusinessRules;
        private readonly IStorageService _storageService;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ILogger<CreateMultipleProductsCommandHandler> _logger;

        public CreateMultipleProductsCommandHandler(
            IProductRepository productRepository,
            IMapper mapper,
            ProductBusinessRules productBusinessRules,
            IStorageService storageService,
            ICategoryRepository categoryRepository,
            ILogger<CreateMultipleProductsCommandHandler> logger)
        {
            _productRepository = productRepository;
            _mapper = mapper;
            _productBusinessRules = productBusinessRules;
            _storageService = storageService;
            _categoryRepository = categoryRepository;
            _logger = logger;
        }

        public async Task<List<CreatedProductResponse>> Handle(
            CreateMultipleProductsCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting to create {Count} products", request.Products.Count);
                var responses = new List<CreatedProductResponse>();

                // 1. Kategori varlık kontrolü (Transaction dışında)
                var categoryIds = request.Products.Select(p => p.CategoryId).Distinct().ToList();
                var categories = await _categoryRepository.GetListAsync(c => categoryIds.Contains(c.Id));
                
                if (categories.Count != categoryIds.Count)
                {
                    var missingCategories = categoryIds.Except(categories.Items.Select(c => c.Id)).ToList();
                    throw new Exception($"Categories not found: {string.Join(", ", missingCategories)}");
                }
                
                // VaryantGroupID'leri saklamak için bir sözlük
                var varyantGroupCache = new Dictionary<string, string>();

                foreach (var productDto in request.Products)
                {
                    try
                    {
                        // 2. Product mapping ve hazırlık
                        var product = _mapper.Map<Product>(productDto);
                        
                        // 3. Normalizasyon işlemleri
                        var normalizeName = NameOperation.CharacterRegulatory(productDto.Name);
                        var normalizeSku = NameOperation.CharacterRegulatory(productDto.Sku);
                        
                        // 4. Varyant grup ID ataması - YENİ KOD
                        if (!string.IsNullOrEmpty(productDto.VaryantGroupID))
                        {
                            // Eğer üründe zaten bir varyant grup ID varsa, onu kullan
                            product.VaryantGroupID = productDto.VaryantGroupID;
                        }
                        else 
                        {
                            // Aynı isimli ürünler için önbelleğe bakma
                            if (varyantGroupCache.TryGetValue(normalizeName, out var cachedGroupId))
                            {
                                // Aynı isimli ürün için daha önce oluşturulan grup ID'yi kullan
                                product.VaryantGroupID = cachedGroupId;
                            }
                            else
                            {
                                // 8 haneli rastgele sayı oluştur
                                var randomCode = GenerateRandomCode(8);
                                product.VaryantGroupID = $"{normalizeName}-{randomCode}";
                                
                                // Önbelleğe ekle
                                varyantGroupCache[normalizeName] = product.VaryantGroupID;
                            }
                        }

                        // 5. Feature value'ları oluşturma
                        if (productDto.FeatureValueIds != null)
                        {
                            product.ProductFeatureValues = productDto.FeatureValueIds
                                .Select(fvId => new ProductFeatureValue(product.Id, fvId))
                                .ToList();
                        }

                        // 6. Product kaydetme (Transaction içinde)
                        await _productRepository.AddAsync(product);

                        // 7. Image upload ve kayıt (Transaction içinde)
                        if (productDto.ProductImages != null && productDto.ProductImages.Any())
                        {
                            var uploadedFiles = await _storageService.UploadAsync(
                                "products", 
                                product.Id, 
                                productDto.ProductImages);

                            product.ProductImageFiles = uploadedFiles.Select((file, index) => 
                                new ProductImageFile(file.fileName, file.entityType, file.path, file.storageType)
                                {
                                    Showcase = index == productDto.ShowcaseImageIndex,
                                    Format = file.format
                                }).ToList();

                            await _productBusinessRules.EnsureOnlyOneShowcaseImage(product.ProductImageFiles);
                            await _productRepository.UpdateAsync(product);
                        }

                        var response = _mapper.Map<CreatedProductResponse>(product);
                        responses.Add(response);
                        
                        _logger.LogInformation("Product created successfully: {ProductId}", product.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating product: {ProductName}", productDto.Name);
                        throw;
                    }
                }

                return responses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk product creation");
                throw;
            }
        }
        
        // 8 haneli rastgele sayı üreten metod
        private string GenerateRandomCode(int length)
        {
            // Güvenli rastgele sayı üretici
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            
            // 32-bit tamsayıya dönüştür (pozitif sayı için Math.Abs)
            var value = Math.Abs(BitConverter.ToInt32(bytes, 0));
            
            // İstenen uzunlukta bir sayı olarak formatla
            return (value % (int)Math.Pow(10, length)).ToString().PadLeft(length, '0');
        }
    }
}