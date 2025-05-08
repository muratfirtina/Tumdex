using Application.Features.Carousels.Dtos;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Application.Features.Carousels.Commands.Update;

public class UpdateCarouselCommand : IRequest<UpdatedCarouselResponse>, ICacheRemoverRequest
{
    public string Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? Order { get; set; }
    public bool IsActive { get; set; }
    public List<CarouselImageFileDto>? CarouselImageFiles { get; set; }
    public List<IFormFile>? NewCarouselImages { get; set; }
    public List<string>? ExistingImageIds { get; set; }
    
    // Video özellikleri
    public string? MediaType { get; set; } // "image" veya "video"
    public string? VideoType { get; set; } // "upload", "youtube", "vimeo"
    public string? VideoUrl { get; set; }
    public string? VideoId { get; set; }
    public IFormFile? CarouselVideoFile { get; set; }
    
    // Cache özellikleri
    public string CacheKey => $"Carousel-{Id}"; // Spesifik carousel cache'i (varsa)
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Carousels;
    
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
            // 1. Carousel'i getir ve null kontrolü yap
            var carousel = await _carouselRepository.GetAsync(x => x.Id == request.Id, include: x => x
                .Include(x => x.CarouselImageFiles));
            
            if (carousel == null)
            {
                throw new Exception($"Carousel with id '{request.Id}' not found");
            }
            
            // 2. Carousel'e request verilerini map et
            _mapper.Map(request, carousel);
            
            // CarouselImageFiles koleksiyonunun null olmamasını sağla
            if (carousel.CarouselImageFiles == null)
            {
                carousel.CarouselImageFiles = new List<CarouselImageFile>();
            }
            
            // 3. Silinecek resimleri kontrol et - null kontrolü ekle
            if (request.ExistingImageIds != null && request.ExistingImageIds.Count > 0)
            {
                // Silinecek resimleri bul
                var imagesToRemove = carousel.CarouselImageFiles
                    .Where(x => request.ExistingImageIds.Contains(x.Id))
                    .ToList();
                    
                // Resimleri kaldır
                foreach (var imageToRemove in imagesToRemove)
                {
                    carousel.CarouselImageFiles.Remove(imageToRemove);
                    _imageFileRepository.Delete(imageToRemove);
                    
                    // Storage'dan silme - null kontrolü ekle
                    if (!string.IsNullOrEmpty(imageToRemove.Path) && !string.IsNullOrEmpty(imageToRemove.Name))
                    {
                        await _storageService.DeleteFromAllStoragesAsync("carousels", imageToRemove.Path, imageToRemove.Name);
                    }
                }
            }
            
            // 4. Yeni resimler ekle
            if (request.NewCarouselImages != null && request.NewCarouselImages.Any())
            {
                var uploadFiles = await _storageService.UploadAsync("carousels", carousel.Id, request.NewCarouselImages);
                foreach (var file in uploadFiles)
                {
                    carousel.CarouselImageFiles.Add(new CarouselImageFile(
                        file.fileName, 
                        file.entityType ?? "carousels", 
                        file.path, 
                        file.storageType
                    )
                    {
                        // Zorunlu Format alanını ekle - ya file.format'dan al ya da varsayılan değer ver
                        Format = file.format ?? GetDefaultFormat(file.fileName)
                    });
                }
            }
            
            // 5. Video özelliklerini güncelle
            UpdateVideoProperties(request, carousel);
            
            // 6. Carousel'i güncelle
            await _carouselRepository.UpdateAsync(carousel);
            
            // 7. Response'u dön
            return _mapper.Map<UpdatedCarouselResponse>(carousel);
        }
        
        private void UpdateVideoProperties(UpdateCarouselCommand request, Carousel carousel)
        {
            if (request.MediaType == "video")
            {
                carousel.MediaType = "video";
                carousel.VideoType = request.VideoType;
                carousel.VideoUrl = request.VideoUrl;
                
                // Video ID'yi ayıkla
                if (!string.IsNullOrEmpty(request.VideoUrl))
                {
                    if (request.VideoType == "youtube")
                    {
                        var match = Regex.Match(request.VideoUrl, 
                            @"(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^""&?\/\s]{11})");
                        if (match.Success)
                        {
                            carousel.VideoId = match.Groups[1].Value;
                        }
                    }
                    else if (request.VideoType == "vimeo")
                    {
                        var match = Regex.Match(request.VideoUrl, 
                            @"vimeo\.com\/(?:channels\/(?:\w+\/)?|groups\/(?:[^\/]*)\/videos\/|)(\d+)(?:|\/\?)");
                        if (match.Success)
                        {
                            carousel.VideoId = match.Groups[1].Value;
                        }
                    }
                }
                
                // Eğer video yüklendiyse, onu işle
                if (request.CarouselVideoFile != null)
                {
                    // Video upload işlemleri buraya eklenir
                    // Bu örnekte sadece null kontrolü ekledik
                }
            }
            else
            {
                carousel.MediaType = "image";
                carousel.VideoType = null;
                carousel.VideoUrl = null;
                carousel.VideoId = null;
            }
        }
        private string GetDefaultFormat(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "unknown";
    
            string extension = Path.GetExtension(fileName).ToLower();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".avif" => "image/avif",
                ".svg" => "image/svg+xml",
                _ => "image/jpeg" // Varsayılan format
            };
        }
    }
}