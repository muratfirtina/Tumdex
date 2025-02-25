using Application.Storage;
using Core.CrossCuttingConcerns.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.Products.Commands.DecriptionImageUpload;

public class UploadDescriptionImageCommand : IRequest<UploadedDescriptionImageResponse>
{
    public IFormFile Image { get; set; }
    
    // Add a parameterless constructor
    public UploadDescriptionImageCommand()
    {
    }

    // Keep the existing parameterized constructor
    public UploadDescriptionImageCommand(IFormFile image)
    {
        Image = image;
    }
    
    public class UploadDescriptionImageCommandHandler : IRequestHandler<UploadDescriptionImageCommand, UploadedDescriptionImageResponse>
    {
        private readonly IStorageService _storageService;

        public UploadDescriptionImageCommandHandler(IStorageService storageService)
        {
            _storageService = storageService;
        }

        public async Task<UploadedDescriptionImageResponse> Handle(UploadDescriptionImageCommand request, CancellationToken cancellationToken)
        {
            if (request.Image == null || request.Image.Length == 0)
                throw new BusinessException("No file uploaded");

            var uploadedFiles = await _storageService.UploadAsync(
                "description-images", 
                Guid.NewGuid().ToString(), 
                new List<IFormFile> { request.Image });

            var result = uploadedFiles.FirstOrDefault();
            if (!string.IsNullOrEmpty(result.url))
            {
                return new UploadedDescriptionImageResponse { Url = result.url };
            }

            throw new BusinessException("Upload failed");
        }
    }
}