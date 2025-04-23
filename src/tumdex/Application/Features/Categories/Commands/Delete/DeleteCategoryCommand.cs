using Application.Features.Categories.Rules;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Categories.Commands.Delete;
public class DeleteCategoryCommand : IRequest<DeletedCategoryResponse>, ICacheRemoverRequest
{
    public string Id { get; set; }
    public string CacheKey => $"Category-{Id}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.ProductRelated;
    public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, DeletedCategoryResponse>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly CategoryBusinessRules _categoryBusinessRules;
        private readonly IMapper _mapper;
        private readonly ILogger<DeleteCategoryCommandHandler> _logger;

        public DeleteCategoryCommandHandler(
            ICategoryRepository categoryRepository,
            CategoryBusinessRules categoryBusinessRules,
            IMapper mapper,
             ILogger<DeleteCategoryCommandHandler> logger) // Logger eklendi
        {
            _categoryRepository = categoryRepository;
            _categoryBusinessRules = categoryBusinessRules;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<DeletedCategoryResponse> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to delete category with ID: {CategoryId}", request.Id);

            // İlişkili verileri yüklemeye gerek yok, sadece silinecek entity lazım.
            Category? category = await _categoryRepository.GetAsync(p => p.Id == request.Id, cancellationToken: cancellationToken);
            await _categoryBusinessRules.CategoryShouldExistWhenSelected(category); // İş kuralı

             // Kategori null ise iş kuralı hata verir, yine de kontrol.
             if (category == null)
             {
                 _logger.LogWarning("Category with ID: {CategoryId} not found for deletion (should have been caught by business rule).", request.Id);
                 // İş kuralı hata fırlatmalı. Güvenlik için response döndür.
                 return new DeletedCategoryResponse { Success = false, Id = request.Id };
             }
             
            await _categoryRepository.DeleteAsync(category);
            _logger.LogInformation("Category deleted successfully: {CategoryId}", request.Id);

            // Başarı response'u döndür
            // DeletedCategoryResponse response = _mapper.Map<DeletedCategoryResponse>(category); // Silinen veriyi maplemeye gerek yok
            // response.Success = true;
            return new DeletedCategoryResponse { Success = true, Id = request.Id };
        }
    }
}