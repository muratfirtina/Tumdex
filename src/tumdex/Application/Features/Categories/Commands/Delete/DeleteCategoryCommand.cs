using Application.Consts;
using Application.Features.Categories.Rules;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Domain;
using Domain.Entities;
using MediatR;

namespace Application.Features.Categories.Commands.Delete;

public class DeleteCategoryCommand : IRequest<DeletedCategoryResponse>, ICacheRemoverRequest
{
    public string Id { get; set; }
    public string CacheKey => $"Category-{Id}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    
    public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, DeletedCategoryResponse>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly CategoryBusinessRules _categoryBusinessRules;
        private readonly IMapper _mapper;

        public DeleteCategoryCommandHandler(ICategoryRepository categoryRepository, IMapper mapper, CategoryBusinessRules categoryBusinessRules)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _categoryBusinessRules = categoryBusinessRules;
        }

        public async Task<DeletedCategoryResponse> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
        {
            Category? category = await _categoryRepository.GetAsync(p=>p.Id==request.Id,cancellationToken: cancellationToken);
            await _categoryBusinessRules.CategoryShouldExistWhenSelected(category);
            await _categoryRepository.DeleteAsync(category!);
            DeletedCategoryResponse response = _mapper.Map<DeletedCategoryResponse>(category);
            response.Success = true;
            return response;
        }
    }
}