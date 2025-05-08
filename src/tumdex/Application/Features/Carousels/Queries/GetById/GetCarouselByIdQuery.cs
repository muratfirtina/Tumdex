using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Carousels.Queries.GetById;

public class GetCarouselByIdQuery : IRequest<GetCarouselByIdQueryResponse>, ICachableRequest
{
    public string Id { get; set; }
    
    public string CacheKey => $"Carousel-{Id}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Carousels;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(10);

    public class GetCarouselByIdQueryHandler : IRequestHandler<GetCarouselByIdQuery, GetCarouselByIdQueryResponse>
    {
        private readonly ICarouselRepository _carouselRepository;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;

        public GetCarouselByIdQueryHandler(ICarouselRepository carouselRepository, IStorageService storageService, IMapper mapper)
        {
            _carouselRepository = carouselRepository;
            _storageService = storageService;
            _mapper = mapper;
        }

        public async Task<GetCarouselByIdQueryResponse> Handle(GetCarouselByIdQuery request, CancellationToken cancellationToken)
        {
            Carousel carousel = await _carouselRepository.GetAsync(
                x => x.Id == request.Id,
                include: x => x
                    .Include(c => c.CarouselImageFiles),
                cancellationToken: cancellationToken);
            
            if (carousel == null)
                throw new Exception($"Carousel with id {request.Id} not found");
                
            var response = _mapper.Map<GetCarouselByIdQueryResponse>(carousel);
            
            // CarouselImageFile'ları DTO'ya dönüştürme
            if (carousel.CarouselImageFiles != null)
            {
                var imageFiles = carousel.CarouselImageFiles.Select(imageFile => new CarouselImageFile
                {
                    Id = imageFile.Id,
                    Name = imageFile.Name,
                    Path = imageFile.Path,
                    EntityType = imageFile.EntityType,
                    Storage = imageFile.Storage,
                    Alt = imageFile.Alt
                });

                // ToDtos extension metodunu kullanarak dönüşümü yapma
                response.CarouselImageFiles = imageFiles.ToDtos(_storageService);
            }
            
            return response;
        }
    }
}