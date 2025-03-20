using Application.Consts;
using Application.Features.Brands.Rules;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using Domain;
using Domain.Entities;
using MediatR;

namespace Application.Features.Brands.Commands.Delete;

public class DeleteBrandCommand : IRequest<DeletedBrandResponse>, ITransactionalRequest, ICacheRemoverRequest
{
    public string Id { get; set; }
    
    // Specific cache key with ID
    public string CacheKey => $"Brand-{Id}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    
    public class DeleteBrandCommandHandler : IRequestHandler<DeleteBrandCommand, DeletedBrandResponse>
    {
        private readonly IBrandRepository _brandRepository;
        private readonly BrandBusinessRules _brandBusinessRules;
        private readonly IMapper _mapper;

        public DeleteBrandCommandHandler(IBrandRepository brandRepository, IMapper mapper, BrandBusinessRules brandBusinessRules)
        {
            _brandRepository = brandRepository;
            _mapper = mapper;
            _brandBusinessRules = brandBusinessRules;
        }

        public async Task<DeletedBrandResponse> Handle(DeleteBrandCommand request, CancellationToken cancellationToken)
        {
            Brand? brand = await _brandRepository.GetAsync(p=>p.Id==request.Id,cancellationToken: cancellationToken);
            await _brandBusinessRules.BrandShouldExistWhenSelected(brand);
            await _brandRepository.DeleteAsync(brand!);
            return new DeletedBrandResponse { Success = true };
        }
    }
}