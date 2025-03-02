using Application.Consts;
using Application.Extensions.ImageFileExtensions;
using Application.Features.Brands.Consts;
using Application.Features.Brands.Rules;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using Core.CrossCuttingConcerns.Exceptions;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Brands.Commands.Update;

public class UpdateBrandCommand : IRequest<UpdatedBrandResponse>,ITransactionalRequest,ICacheRemoverRequest
{
    public string Id { get; set; }
    public string Name { get; set; }
    public List<IFormFile>? BrandImage { get; set; }
    public bool RegenerateId { get; set; } = true;
    
    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
}

public class UpdateBrandCommandHandler : IRequestHandler<UpdateBrandCommand, UpdatedBrandResponse>
{
    private readonly IBrandRepository _brandRepository;
    private readonly BrandBusinessRules _brandBusinessRules;
    private readonly IMapper _mapper;
    private readonly IStorageService _storageService;

    public UpdateBrandCommandHandler(
        IBrandRepository brandRepository, 
        IMapper mapper,
        BrandBusinessRules brandBusinessRules,
        IStorageService storageService)
    {
        _brandRepository = brandRepository;
        _mapper = mapper;
        _brandBusinessRules = brandBusinessRules;
        _storageService = storageService;
    }

    public async Task<UpdatedBrandResponse> Handle(UpdateBrandCommand request,
        CancellationToken cancellationToken)
    {
        Brand? brand = await _brandRepository.GetAsync(
            p => p.Id == request.Id,
            include: x => x.Include(b => b.Products)
                          .Include(b => b.BrandImageFiles),
            cancellationToken: cancellationToken);

        await _brandBusinessRules.BrandShouldExistWhenSelected(brand);
        await _brandBusinessRules.BrandNameShouldNotExistWhenInsertingOrUpdating(request.Name, request.Id);

        if (brand == null)
            throw new BusinessException(BrandsBusinessMessages.BrandNotExists);

        string oldId = brand.Id;
        Brand updatedBrand;

        // Handle ID regeneration if name changes
        if (request.RegenerateId && brand.Name != request.Name)
        {
            updatedBrand = new Brand(request.Name)
            {
                CreatedDate = brand.CreatedDate,
                UpdatedDate = DateTime.UtcNow
            };

            if (brand.Products != null)
            {
                foreach (var product in brand.Products)
                {
                    product.BrandId = updatedBrand.Id;
                    product.UpdatedDate = DateTime.UtcNow;
                }
            }

            // Copy existing images if no new images are provided
            if (request.BrandImage == null || !request.BrandImage.Any())
            {
                updatedBrand.BrandImageFiles = brand.BrandImageFiles;
            }

            await _brandRepository.AddAsync(updatedBrand);
            await _brandRepository.DeleteAsync(brand);
        }
        else
        {
            updatedBrand = brand;
            updatedBrand.Name = request.Name;
            updatedBrand.UpdatedDate = DateTime.UtcNow;
        }

        // Handle image updates
        if (request.BrandImage != null && request.BrandImage.Any())
        {
            // Delete old images from storage
            if (updatedBrand.BrandImageFiles != null && updatedBrand.BrandImageFiles.Any())
            {
                foreach (var oldImage in updatedBrand.BrandImageFiles)
                {
                    await _storageService.DeleteFromAllStoragesAsync(oldImage.EntityType, oldImage.Path, oldImage.Name);
                }
                updatedBrand.BrandImageFiles.Clear();
            }

            // Upload new images
            var uploadResult = await _storageService.UploadAsync("brands", updatedBrand.Id, request.BrandImage);
            if (uploadResult.Any())
            {
                var newImages = new List<BrandImageFile>();
                foreach (var (fileName, path, _, storageType, url, format) in uploadResult)
                {
                    var brandImageFile = new BrandImageFile(fileName, "brands", path, storageType)
                    {
                        Format = format
                    };
                    newImages.Add(brandImageFile);
                }
                updatedBrand.BrandImageFiles = newImages;
            }
        }

        await _brandRepository.UpdateAsync(updatedBrand);

        var response = _mapper.Map<UpdatedBrandResponse>(updatedBrand);
        if (oldId != updatedBrand.Id)
            response.OldId = oldId;

        // Map image information to response
        if (updatedBrand.BrandImageFiles != null && updatedBrand.BrandImageFiles.Any())
        {
            response.BrandImage = updatedBrand.BrandImageFiles.ToDtos(_storageService).FirstOrDefault();
        }

        return response;
    }
}