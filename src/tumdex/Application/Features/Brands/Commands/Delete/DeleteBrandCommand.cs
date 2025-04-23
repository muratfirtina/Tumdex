// Application/Features/Brands/Commands/Delete/DeleteBrandCommand.cs
using Application.Consts;
using Application.Features.Brands.Rules;
using Application.Repositories;
using AutoMapper; // IMapper için eklendi
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using Domain; // DeletedBrandResponse için (varsa)
using Domain.Entities; // Brand için
using MediatR;
using Microsoft.Extensions.Logging; // Logger için

namespace Application.Features.Brands.Commands.Delete;

public class DeleteBrandCommand : IRequest<DeletedBrandResponse>, ITransactionalRequest, ICacheRemoverRequest
{
    public string Id { get; set; }

    public string CacheKey => $"Brand-{Id}";
    public bool BypassCache => false;
    public string? CacheGroupKey => $"{CacheGroups.Brands},{CacheGroups.Products},{CacheGroups.Categories}";

    public class DeleteBrandCommandHandler : IRequestHandler<DeleteBrandCommand, DeletedBrandResponse>
    {
        private readonly IBrandRepository _brandRepository;
        private readonly BrandBusinessRules _brandBusinessRules;
        private readonly IMapper _mapper;
        private readonly ILogger<DeleteBrandCommandHandler> _logger;

        public DeleteBrandCommandHandler(
            IBrandRepository brandRepository,
            BrandBusinessRules brandBusinessRules,
            IMapper mapper,
            ILogger<DeleteBrandCommandHandler> logger)
        {
            _brandRepository = brandRepository;
            _brandBusinessRules = brandBusinessRules;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<DeletedBrandResponse> Handle(DeleteBrandCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to delete brand with ID: {BrandId}", request.Id);

            Brand? brand = await _brandRepository.GetAsync(p => p.Id == request.Id, cancellationToken: cancellationToken);
            await _brandBusinessRules.BrandShouldExistWhenSelected(brand);

            if (brand != null)
            {
                await _brandRepository.DeleteAsync(brand);
                _logger.LogInformation("Brand deleted successfully: {BrandId}", request.Id);

                // Silme işlemi başarılı response'u döndür
                // Mapper kullanılmıyorsa doğrudan response oluşturulabilir.
                // DeletedBrandResponse'un içeriğine göre ayarlanmalı.
                // var response = _mapper.Map<DeletedBrandResponse>(brand); // Eğer silinen veriyi döndürmek gerekiyorsa
                // response.Success = true;
                return new DeletedBrandResponse { Success = true, Id = request.Id }; // Basit başarı durumu
            }
            else
            {
                // Bu durum iş kuralı tarafından yakalanmalı, ancak yine de loglayalım.
                _logger.LogWarning("Brand with ID: {BrandId} not found for deletion (should have been caught by business rule).", request.Id);
                // Normalde buraya gelinmemeli, iş kuralı hata fırlatmalı.
                // Güvenlik için yine de bir response döndürelim.
                return new DeletedBrandResponse { Success = false, Id = request.Id };
            }
        }
    }
}
