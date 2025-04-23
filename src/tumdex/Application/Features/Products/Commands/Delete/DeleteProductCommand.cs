using Application.Features.Products.Rules;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Products.Commands.Delete;

public class DeleteProductCommand : IRequest<DeletedProductResponse>, ICacheRemoverRequest // ITransactionalRequest eklenebilir
{
    public string Id { get; set; }
    public string CacheKey => $"Product-{Id}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.ProductRelated;
    public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, DeletedProductResponse>
    {
        private readonly IProductRepository _productRepository;
        private readonly ProductBusinessRules _productBusinessRules;
        private readonly IMapper _mapper;
        private readonly ILogger<DeleteProductCommandHandler> _logger;

        public DeleteProductCommandHandler(
            IProductRepository productRepository,
            ProductBusinessRules productBusinessRules,
            IMapper mapper,
            ILogger<DeleteProductCommandHandler> logger)
        {
            _productRepository = productRepository;
            _productBusinessRules = productBusinessRules;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<DeletedProductResponse> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to delete product with ID: {ProductId}", request.Id);
            
            Product? product = await _productRepository.GetAsync(p => p.Id == request.Id, cancellationToken: cancellationToken);
            await _productBusinessRules.ProductShouldExistWhenSelected(product); // İş kuralı

            if (product == null) // İş kuralı yakalamalı, yine de kontrol
            {
                _logger.LogWarning("Product with ID: {ProductId} not found for deletion (should have been caught by business rule).", request.Id);
                return new DeletedProductResponse { Success = false, Id = request.Id };
            }

            await _productRepository.DeleteAsync(product);
            _logger.LogInformation("Product deleted successfully: {ProductId}", request.Id);

            return new DeletedProductResponse { Success = true, Id = request.Id };
        }
    }
}