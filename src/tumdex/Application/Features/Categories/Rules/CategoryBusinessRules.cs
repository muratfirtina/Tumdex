using Application.Features.Categories.Consts;
using Application.Repositories;
using Core.Application.Rules;
using Core.CrossCuttingConcerns.Exceptions;
using Domain;

namespace Application.Features.Categories.Rules;

public class CategoryBusinessRules : BaseBusinessRules
{
    private readonly ICategoryRepository _categoryRepository;

    public CategoryBusinessRules(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public Task CategoryShouldExistWhenSelected(Category? category)
    {
        if (category == null)
            throw new BusinessException(CategoriesBusinessMessages.CategoryNotExists);
        return Task.CompletedTask;
    }

    public async Task CategoryIdShouldExistWhenSelected(string id, CancellationToken cancellationToken)
    {
        Category? category = await _categoryRepository.GetAsync(
            predicate: e => e.Id == id,
            enableTracking: false,
            cancellationToken: cancellationToken
        );
        await CategoryShouldExistWhenSelected(category);
    }
    
    public async Task CategoryNameShouldBeUniqueWhenCreate(string name, CancellationToken cancellationToken)
    {
        Category? category = await _categoryRepository.GetAsync(
            predicate: e => e.Name == name,
            enableTracking: false,
            cancellationToken: cancellationToken
        );
        if (category != null)
            throw new BusinessException(CategoriesBusinessMessages.CategoryNameAlreadyExists);
    }
    
    public async Task CategoryNameShouldBeUniqueWhenUpdate(string name, string id, CancellationToken cancellationToken)
    {
        Category? category = await _categoryRepository.GetAsync(
            predicate: e => e.Name == name && e.Id != id,
            enableTracking: false,
            cancellationToken: cancellationToken
        );
        if (category != null)
            throw new BusinessException(CategoriesBusinessMessages.CategoryNameAlreadyExists);
    }
    
    //kategori güncellenirken parent kategorinin kendisi olmaması gerektiğini kontrol eder.
    public async Task ParentCategoryShouldNotBeSelf(string id, string? parentCategoryId, CancellationToken cancellationToken)
    {
        if (parentCategoryId == id)
            throw new BusinessException(CategoriesBusinessMessages.ParentCategoryShouldNotBeSelf);
    }
    //kategori güncellerken parent kategorinin kendisinin alt kategorisi olmaması gerektiğini kontrol eder.
    public async Task ParentCategoryShouldNotBeChild(string id, string? parentCategoryId, CancellationToken cancellationToken)
    {
        if (parentCategoryId != null)
        {
            Category? parentCategory = await _categoryRepository.GetAsync(
                predicate: e => e.Id == parentCategoryId,
                enableTracking: false,
                cancellationToken: cancellationToken
            );
            if (parentCategory != null)
            {
                if (parentCategory.ParentCategoryId == id)
                    throw new BusinessException(CategoriesBusinessMessages.ParentCategoryShouldNotBeChild);
            }
        }
    }
    
    //kategori güncellenirken alt kategorisini parent olarak seçmemesi gerektiğini kontrol eder.
    public async Task SubCategoryShouldNotBeParent(string id, List<string>? subCategoryIds, CancellationToken cancellationToken)
    {
        if (subCategoryIds != null)
        {
            foreach (var subCategoryId in subCategoryIds)
            {
                Category? subCategory = await _categoryRepository.GetAsync(
                    predicate: e => e.Id == subCategoryId,
                    enableTracking: false,
                    cancellationToken: cancellationToken
                );
                if (subCategory != null)
                {
                    if (subCategory.ParentCategoryId == id)
                        throw new BusinessException(CategoriesBusinessMessages.SubCategoryShouldNotBeParent);
                }
            }
        }
    }
    
    //kategori güncellenirken parent kategorisinin kendisinin alt kategorisi olmaması gerektiğini kontrol eder.
    
    public async Task ParentCategoryShouldNotBeDescendant(string id, string? parentCategoryId, CancellationToken cancellationToken)
    {
        if (parentCategoryId != null)
        {
            await CheckIfParentIsDescendant(id, parentCategoryId, cancellationToken);
        }
    }
    
    private async Task CheckIfParentIsDescendant(string originalId, string currentParentCategoryId, CancellationToken cancellationToken)
    {
        Category? currentCategory = await _categoryRepository.GetAsync(
            predicate: e => e.Id == currentParentCategoryId,
            enableTracking: false,
            cancellationToken: cancellationToken
        );

        if (currentCategory != null)
        {
            // Eğer currentCategory'nin ParentCategoryId'si originalId'ye eşitse döngü var demektir
            if (currentCategory.ParentCategoryId == originalId)
            {
                throw new BusinessException(CategoriesBusinessMessages.ParentCategoryShouldNotBeDescendant);
            }

            // Eğer currentCategory'nin de bir üst kategorisi varsa, özyinelemeli olarak kontrol et
            if (currentCategory.ParentCategoryId != null)
            {
                await CheckIfParentIsDescendant(originalId, currentCategory.ParentCategoryId, cancellationToken);
            }
        }
    }
    
    //bir kategori güncellenirken bir parentCategoryId verilmiyorsa o direkt ana kategori olması gerekiyor. yani parentCategoryId null  olarak güncelleniyor is o en üst kategori olabilmeli.
    public async Task ParentCategoryShouldBeNullWhenUpdate(string id, string? parentCategoryId, CancellationToken cancellationToken)
    {
        if (parentCategoryId == null)
        {
            Category? category = await _categoryRepository.GetAsync(
                predicate: e => e.Id == id,
                enableTracking: false,
                cancellationToken: cancellationToken
            );
            if (category != null)
            {
                if (category.ParentCategoryId != null)
                    throw new BusinessException(CategoriesBusinessMessages.ParentCategoryShouldBeNullWhenUpdate);
            }
        }
    }

}