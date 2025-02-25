using System.Text.Json.Serialization;
using Application.Features.Products.Rules;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Persistence.Repositories.Operation;
using Domain;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.Products.Commands.Create;

public class CreateProductCommand : IRequest<CreatedProductResponse>
{
    public string Name { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string CategoryId { get; set; }
    public string BrandId { get; set; }
    public string? Sku { get; set; }
    public decimal? Price { get; set; }
    public int Stock { get; set; }
    public int? Tax { get; set; }
    public string? VaryantGroupID { get; set; }
    public List<string>? FeatureIds { get; set; }
    public List<string>? FeatureValueIds { get; set; }

    [JsonIgnore] // Bu özellik JSON olarak gelmeyecek, form-data'dan gelecek
    public List<IFormFile>? ProductImages { get; set; }

    public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, CreatedProductResponse>
    {
        private readonly IMapper _mapper;
        private readonly ProductBusinessRules _productBusinessRules;
        private readonly IProductRepository _productRepository;
        private readonly IStorageService _storageService;

        public CreateProductCommandHandler(IProductRepository productRepository, IMapper mapper,
            ProductBusinessRules productBusinessRules, IStorageService storageService)
        {
            _productRepository = productRepository;
            _mapper = mapper;
            _productBusinessRules = productBusinessRules;
            _storageService = storageService;
        }

        public async Task<CreatedProductResponse> Handle(CreateProductCommand request,
            CancellationToken cancellationToken)
        {
            var product = _mapper.Map<Product>(request);

            var normalizename = NameOperation.CharacterRegulatory(request.Name);
            var normalizesku = NameOperation.CharacterRegulatory(request.Sku);

            if (string.IsNullOrEmpty(request.VaryantGroupID))
                product.VaryantGroupID = $"{normalizename}-{normalizesku}";

            product.ProductFeatureValues = new List<ProductFeatureValue>();
            if (request.FeatureValueIds != null)
                foreach (var featureValueId in request.FeatureValueIds)
                    product.ProductFeatureValues.Add(new ProductFeatureValue(product.Id, featureValueId));

            await _productRepository.AddAsync(product);

            // Handle image uploads
            if (request.ProductImages != null && request.ProductImages.Any())
            {
                var uploadedFiles = await _storageService.UploadAsync("products", product.Id, request.ProductImages);
                foreach (var file in uploadedFiles)
                {
                    var productImageFile =
                        new ProductImageFile(file.fileName, file.entityType, file.path, file.storageType);
                    product.ProductImageFiles.Add(productImageFile);
                }

                await _productRepository.UpdateAsync(product);
            }

            var response = _mapper.Map<CreatedProductResponse>(product);
            return response;
        }
    }
}