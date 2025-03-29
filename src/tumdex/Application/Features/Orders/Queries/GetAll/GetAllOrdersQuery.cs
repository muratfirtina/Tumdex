using Application.Consts;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Orders.Queries.GetAll;

public class GetAllOrdersQuery : IRequest<GetListResponse<GetAllOrdersQueryResponse>>
{
    public PageRequest PageRequest { get; set; }
    
    // More specific cache key that includes pagination information
    public string CacheKey => $"GetAllOrders-Page{PageRequest.PageIndex}-Size{PageRequest.PageSize}";
    public bool BypassCache { get; }
    public string CacheGroupKey => CacheGroups.Orders;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(5);
    
    public class GetAllOrdersQueryHandler : IRequestHandler<GetAllOrdersQuery, GetListResponse<GetAllOrdersQueryResponse>>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IMapper _mapper;

        public GetAllOrdersQueryHandler(IOrderRepository orderRepository, IMapper mapper)
        {
            _orderRepository = orderRepository;
            _mapper = mapper;
        }

        public async Task<GetListResponse<GetAllOrdersQueryResponse>> Handle(GetAllOrdersQuery request, CancellationToken cancellationToken)
        {
            IPaginate<Order> orders = await _orderRepository.GetListAsync(
                index: request.PageRequest.PageIndex,
                size: request.PageRequest.PageSize,
                include: o => o
                    .Include(o => o.OrderItems)
                    .Include(o => o.User),
                orderBy: o => o.OrderByDescending(o => o.CreatedDate),
                cancellationToken: cancellationToken);
            
            GetListResponse<GetAllOrdersQueryResponse> response = _mapper.Map<GetListResponse<GetAllOrdersQueryResponse>>(orders);
            return response;
        }
    }
}