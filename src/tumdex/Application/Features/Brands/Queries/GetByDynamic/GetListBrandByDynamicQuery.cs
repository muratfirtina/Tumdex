using Application.Consts;
using Application.Extensions;
using Application.Extensions.ImageFileExtensions;
using Application.Features.Brands.Dtos;
using Application.Features.Brands.Rules;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Dynamic;
using Core.Persistence.Paging;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Brands.Queries.GetByDynamic;

public class GetListBrandByDynamicQuery : IRequest<GetListResponse<GetListBrandByDynamicQueryResponse>>
{
    public PageRequest PageRequest { get; set; }
    public DynamicQuery DynamicQuery { get; set; }
    
    public class GetListByDynamicBrandQueryHandler : IRequestHandler<GetListBrandByDynamicQuery, GetListResponse<GetListBrandByDynamicQueryResponse>>
    {
        private readonly IBrandRepository _brandRepository;
        private readonly IMapper _mapper;
        private readonly BrandBusinessRules _brandBusinessRules;
        private readonly IStorageService _storageService;

        public GetListByDynamicBrandQueryHandler(IBrandRepository brandRepository, IMapper mapper, BrandBusinessRules brandBusinessRules, IStorageService storageService)
        {
            _brandRepository = brandRepository;
            _mapper = mapper;
            _brandBusinessRules = brandBusinessRules;
            _storageService = storageService;
        }

        public async Task<GetListResponse<GetListBrandByDynamicQueryResponse>> Handle(GetListBrandByDynamicQuery request, CancellationToken cancellationToken)
        {

            if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
            {
                var allBrands = await _brandRepository.GetAllByDynamicAsync(
                    request.DynamicQuery,
                    include: x => x.Include(x => x.BrandImageFiles),
                    cancellationToken: cancellationToken);

                var brandsDtos = _mapper.Map<GetListResponse<GetListBrandByDynamicQueryResponse>>(allBrands);
                brandsDtos.Items.SetImageUrls(_storageService);
                return brandsDtos;
            }
            else
            {
                IPaginate<Brand> brands = await _brandRepository.GetListByDynamicAsync(
                    request.DynamicQuery,
                    index: request.PageRequest.PageIndex,
                    size: request.PageRequest.PageSize,
                    include: x => x.Include(x => x.BrandImageFiles),
                    cancellationToken: cancellationToken);
                
                var brandsDtos = _mapper.Map<GetListResponse<GetListBrandByDynamicQueryResponse>>(brands);
                foreach (var brandDto in brandsDtos.Items)
                {
                    var brand = brands.Items.FirstOrDefault(b => b.Id == brandDto.Id);
                    if (brand?.BrandImageFiles != null)
                    {
                        var brandImage = brand.BrandImageFiles.FirstOrDefault();
                        if (brandImage != null)
                        {
                            brandDto.BrandImage = brandImage.ToDto(_storageService);
                        }
                    }
                }
                return brandsDtos;

            }
        }
    }
}