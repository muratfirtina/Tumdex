using Application.Extensions;
using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Dynamic;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Orders.Queries.GetOrdersByDynamic;

public class GetOrdersByDynamicQuery : IRequest<GetListResponse<GetOrdersByDynamicQueryResponse>>,ICachableRequest
{
   public PageRequest PageRequest { get; set; }
   public DynamicQuery DynamicQuery { get; set; }

   public string CacheKey => $"GetOrdersByDynamicQuery({DynamicQuery})";
   public bool BypassCache { get; }
   public string? CacheGroupKey => "Orders";
   public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(2);
   
   public class GetOrdersByDynamicQueryHandler : IRequestHandler<GetOrdersByDynamicQuery, GetListResponse<GetOrdersByDynamicQueryResponse>>
   {
       private readonly IOrderRepository _orderRepository;
       private readonly IMapper _mapper;
       private readonly IStorageService _storageService;

       public GetOrdersByDynamicQueryHandler(
           IOrderRepository orderRepository, 
           IMapper mapper, 
           IStorageService storageService)
       {
           _orderRepository = orderRepository;
           _mapper = mapper;
           _storageService = storageService;
       }

       public async Task<GetListResponse<GetOrdersByDynamicQueryResponse>> Handle(
           GetOrdersByDynamicQuery request, 
           CancellationToken cancellationToken)
       {
           if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
           {
               var allOrders = await _orderRepository.GetAllByDynamicAsync(
                   request.DynamicQuery,
                   include: q => q
                       .Include(o => o.OrderItems)
                           .ThenInclude(oi => oi.Product)
                           .ThenInclude(p => p.ProductImageFiles.Where(pif => pif.Showcase))
                       .Include(o => o.User),
                   cancellationToken: cancellationToken);

               var response = _mapper.Map<GetListResponse<GetOrdersByDynamicQueryResponse>>(allOrders);
               await EnrichOrderImages(response.Items, allOrders);
               return response;
           }
           else
           {
               var orders = await _orderRepository.GetListByDynamicAsync(
                   request.DynamicQuery,
                   include: q => q
                       .Include(o => o.OrderItems)
                       .ThenInclude(oi => oi.Product)
                       .ThenInclude(p => p.ProductImageFiles.Where(pif => pif.Showcase))
                       .Include(o => o.User),
                   index: request.PageRequest.PageIndex,
                   size: request.PageRequest.PageSize,
                   cancellationToken: cancellationToken);

               var response = _mapper.Map<GetListResponse<GetOrdersByDynamicQueryResponse>>(orders);
               await EnrichOrderImages(response.Items, orders.Items);
               return response;
           }
       }

       private async Task EnrichOrderImages(
           IEnumerable<GetOrdersByDynamicQueryResponse> orderDtos, 
           IEnumerable<Order> orders)
       {
           foreach (var orderDto in orderDtos)
           {
               var order = orders.FirstOrDefault(o => o.OrderCode == orderDto.OrderCode);
               if (order?.OrderItems == null) continue;

               foreach (var orderItemDto in orderDto.OrderItems)
               {
                   var orderItem = order.OrderItems.FirstOrDefault(oi => oi.Id == orderItemDto.Id);
                   if (orderItem?.Product?.ProductImageFiles == null) continue;

                   var showcaseImage = orderItem.Product.ProductImageFiles.FirstOrDefault();
                   if (showcaseImage != null)
                   {
                       orderItemDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                   }
               }
           }
       }
   }
}