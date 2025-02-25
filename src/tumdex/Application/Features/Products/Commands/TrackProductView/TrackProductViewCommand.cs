using System.Security.Claims;
using Application.Repositories;
using Domain;
using Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Commands.TrackProductView;

public class TrackProductViewCommand : IRequest<Unit>
{
    public string ProductId { get; set; }
}

public class TrackProductViewCommandHandler : IRequestHandler<TrackProductViewCommand, Unit>
{
    private readonly IProductViewRepository _productViewRepository;

    public TrackProductViewCommandHandler(IProductViewRepository productViewRepository)
    {
        _productViewRepository = productViewRepository;
    }

    public async Task<Unit> Handle(TrackProductViewCommand request, CancellationToken cancellationToken)
    {
        await _productViewRepository.TrackProductView(request.ProductId);
        return Unit.Value;
    }
}