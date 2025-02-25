using Application.Features.Carousels.Dtos;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Domain;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Carousels.Commands.Update;

public class UpdateCarouselCommand : IRequest<UpdatedCarouselResponse>
{
    public string Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? Order { get; set; }
    public List<CarouselImageFileDto>? CarouselImageFiles { get; set; }
    public List<IFormFile>? NewCarouselImages { get; set; }
    public List<string>? ExistingImageIds { get; set; }
    
    public class UpdateCarouselCommandHandler : IRequestHandler<UpdateCarouselCommand, UpdatedCarouselResponse>
    {
        private readonly ICarouselRepository _carouselRepository;
        private readonly IStorageService _storageService;
        private readonly IImageFileRepository _imageFileRepository;
        private readonly IMapper _mapper;

        public UpdateCarouselCommandHandler(ICarouselRepository carouselRepository, IStorageService storageService, IImageFileRepository imageFileRepository, IMapper mapper)
        {
            _carouselRepository = carouselRepository;
            _storageService = storageService;
            _imageFileRepository = imageFileRepository;
            _mapper = mapper;
        }

        public async Task<UpdatedCarouselResponse> Handle(UpdateCarouselCommand request, CancellationToken cancellationToken)
        {
            var carousel = await _carouselRepository.GetAsync(x => x.Id == request.Id, include: x => x
                .Include(x => x.CarouselImageFiles));
            _mapper.Map(request, carousel);
            
            if (request.ExistingImageIds != null)
            {
                var existingImages = carousel.CarouselImageFiles.Where(x => request.ExistingImageIds.Contains(x.Id)).ToList();
                foreach (var existingImage in existingImages)
                {
                    carousel.CarouselImageFiles.Remove(existingImage);
                    _imageFileRepository.Delete(existingImage);
                    await _storageService.DeleteFromAllStoragesAsync("carousels",existingImage.Path, existingImage.Name);
                }
            }
            
            if (request.NewCarouselImages != null)
            {
                var uploadFiles = await _storageService.UploadAsync("carousels", carousel.Id, request.NewCarouselImages);
                foreach (var file in uploadFiles)
                {
                    carousel.CarouselImageFiles.Add(new CarouselImageFile(file.fileName, file.entityType, file.path, file.storageType));
                    
                }
            }
            
            await _carouselRepository.UpdateAsync(carousel);
            return _mapper.Map<UpdatedCarouselResponse>(carousel);
        }
    }
}