using Application.Extensions;
using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Carousels.Queries.GetCarousel;

public class GetAllCarouselQuery : IRequest<GetListResponse<GetAllCarouselQueryResponse>>,ICachableRequest
{
    public PageRequest PageRequest { get; set; }
    
    public string CacheKey => $"GetAllCarouselQuery({PageRequest.PageIndex},{PageRequest.PageSize})";
    public bool BypassCache { get; }
    public string? CacheGroupKey => "Carousels";
    public TimeSpan? SlidingExpiration => TimeSpan.FromDays(365);

    public class GetCarouselQueryHandler : IRequestHandler<GetAllCarouselQuery, GetListResponse<GetAllCarouselQueryResponse>>
{
    private readonly ICarouselRepository _carouselRepository;
    private readonly IStorageService _storageService;
    private readonly IMapper _mapper;

    public GetCarouselQueryHandler(ICarouselRepository carouselRepository, IStorageService storageService, IMapper mapper)
    {
        _carouselRepository = carouselRepository;
        _storageService = storageService;
        _mapper = mapper;
    }

    public async Task<GetListResponse<GetAllCarouselQueryResponse>> Handle(GetAllCarouselQuery request, CancellationToken cancellationToken)
    {
        if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
        {
            List<Carousel> carousels = await _carouselRepository.GetAllAsync(
                include: x => x
                    .Include(x => x.CarouselImageFiles),
                cancellationToken: cancellationToken);
            
            var response = _mapper.Map<GetListResponse<GetAllCarouselQueryResponse>>(carousels);
            SetCarouselImageDtos(response.Items);
            return response;
        }
        else
        {
            IPaginate<Carousel> carousels = await _carouselRepository.GetListAsync(
                index: request.PageRequest.PageIndex,
                size: request.PageRequest.PageSize,
                include: x => x
                    .Include(x => x.CarouselImageFiles),
                cancellationToken: cancellationToken
            );
            
            var response = _mapper.Map<GetListResponse<GetAllCarouselQueryResponse>>(carousels);
            SetCarouselImageDtos(response.Items);
            return response;
        }
    }

    private void SetCarouselImageDtos(IEnumerable<GetAllCarouselQueryResponse> carousels)
    {
        foreach (var carousel in carousels)
        {
            if (carousel.CarouselImageFiles != null)
            {
                // CarouselImageFile'ları CarouselImageFileDto'ya dönüştürme
                var imageFiles = carousel.CarouselImageFiles.Select(imageFile => new CarouselImageFile
                {
                    Id = imageFile.Id,
                    Name = imageFile.FileName,
                    Path = imageFile.Path,
                    EntityType = imageFile.EntityType,
                    Storage = imageFile.Storage,
                    Alt = imageFile.Alt
                });

                // ToDtos extension metodunu kullanarak dönüşümü yapma
                carousel.CarouselImageFiles = imageFiles.ToDtos(_storageService);
            }
        }
    }
}
}
