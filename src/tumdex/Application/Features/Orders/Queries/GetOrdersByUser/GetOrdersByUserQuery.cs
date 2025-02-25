using Application.Extensions;
using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Dynamic;
using Core.Persistence.Paging;
using Domain;
using Domain.Enum;
using Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Orders.Queries.GetOrdersByUser;
public class GetOrdersByUserQuery : IRequest<GetListResponse<GetOrdersByUserQueryResponse>>,ICachableRequest
{
   public PageRequest PageRequest { get; set; }
   public string? SearchTerm { get; set; }
   public string? DateRange { get; set; }
   public OrderStatus OrderStatus { get; set; }

   public string CacheKey => $"GetOrdersByUserQuery({PageRequest},{SearchTerm},{DateRange},{OrderStatus})";
   public bool BypassCache { get; }
   public string? CacheGroupKey => "Orders";
   public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(2);
   
   public class GetOrdersByUserQueryHandler : IRequestHandler<GetOrdersByUserQuery, GetListResponse<GetOrdersByUserQueryResponse>>
   {
       private readonly IOrderRepository _orderRepository;
       private readonly IMapper _mapper;
       private readonly IStorageService _storageService;

       public GetOrdersByUserQueryHandler(
           IOrderRepository orderRepository, 
           IMapper mapper, 
           IStorageService storageService)
       {
           _orderRepository = orderRepository;
           _mapper = mapper;
           _storageService = storageService;
       }

       public async Task<GetListResponse<GetOrdersByUserQueryResponse>> Handle(
           GetOrdersByUserQuery request, 
           CancellationToken cancellationToken)
       {
           var orders = await _orderRepository.GetOrdersByUserAsync(
               request.PageRequest, 
               request.OrderStatus, 
               request.DateRange, 
               request.SearchTerm);

           var response = _mapper.Map<GetListResponse<GetOrdersByUserQueryResponse>>(orders);
           
           await EnrichOrderImages(response.Items, orders.Items);
           
           return response;
       }

       private async Task EnrichOrderImages(
           IEnumerable<GetOrdersByUserQueryResponse> orderDtos,
           IEnumerable<Order> orders)
       {
           foreach (var orderDto in orderDtos)
           {
               var order = orders.FirstOrDefault(o => o.Id == orderDto.Id);
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