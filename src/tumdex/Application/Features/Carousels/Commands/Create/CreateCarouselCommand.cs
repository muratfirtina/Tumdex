using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Domain;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.Carousels.Commands.Create;

public class CreateCarouselCommand : IRequest<CreatedCarouselResponse>,ICacheRemoverRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? Order { get; set; }
    
    public string CacheKey => "";
    public bool BypassCache { get; }
    public string? CacheGroupKey => "Carousels";
    
    public List<IFormFile>? CarouselImageFiles { get; set; }
    
    public class CreateCarouselCommandHandler : IRequestHandler<CreateCarouselCommand, CreatedCarouselResponse>
    {
        private readonly ICarouselRepository _carouselRepository;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;
        public CreateCarouselCommandHandler(ICarouselRepository carouselRepository, IStorageService storageService, IMapper mapper)
        {
            _carouselRepository = carouselRepository;
            _storageService = storageService;
            _mapper = mapper;
        }
        public async Task<CreatedCarouselResponse> Handle(CreateCarouselCommand request, CancellationToken cancellationToken)
        {
            var carousel = _mapper.Map<Carousel>(request);
        
            if (request.CarouselImageFiles != null && request.CarouselImageFiles.Any())
            {
                var uploadResult = await _storageService.UploadAsync("carousels", carousel.Id, request.CarouselImageFiles);
                carousel.CarouselImageFiles = uploadResult.Select(x => new CarouselImageFile
                {
                    Name = x.fileName,
                    EntityType = "carousels",
                    Path = x.path,
                    Storage = x.storageType,
                    Format = x.format
                }).ToList();
            }
        
            await _carouselRepository.AddAsync(carousel);
            return _mapper.Map<CreatedCarouselResponse>(carousel);
        }
    }
   
    
}