using Application.Features.Orders.Commands.Delete;
using Application.Features.Orders.Dtos;
using Application.Features.Orders.Queries;
using Application.Features.Orders.Queries.GetAll;
using Application.Features.Orders.Queries.GetById;
using Application.Features.Orders.Queries.GetOrdersByDynamic;
using Application.Features.Orders.Queries.GetOrdersByUser;
using Application.Features.Orders.Queries.GetUserOrderById;
using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Dtos;
using AutoMapper;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain;

namespace Application.Features.Orders.Profiles;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<Order, GetAllOrdersQueryResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.OrderCode, opt => opt.MapFrom(src => src.OrderCode))
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId))
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.UserName))
            .ForMember(dest => dest.OrderDate, opt => opt.MapFrom(src => src.OrderDate))
            .ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src => src.TotalPrice))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
            .ReverseMap();

        CreateMap<Order, DeletedOrderCommandResponse>();


        CreateMap<IPaginate<Order>, GetListResponse<GetAllOrdersQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items))
            .ReverseMap();

        CreateMap<Order, GetOrdersByDynamicQueryResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.OrderCode, opt => opt.MapFrom(src => src.OrderCode))
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.UserName))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email)) // Add this line
            .ForMember(dest => dest.OrderDate, opt => opt.MapFrom(src => src.OrderDate))
            .ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src => src.TotalPrice))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
            .ReverseMap();

        CreateMap<IPaginate<Order>, GetListResponse<GetOrdersByDynamicQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items));

        CreateMap<List<Order>, GetListResponse<GetOrdersByDynamicQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));

        CreateMap<Order, GetOrderByIdQueryResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.OrderCode, opt => opt.MapFrom(src => src.OrderCode))
            .ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src => src.TotalPrice))
            .ForMember(dest => dest.OrderDate, opt => opt.MapFrom(src => src.OrderDate))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString())) // Enum string olarak dönüyor
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.UserName))
            .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.User.PhoneNumber))
            .ForMember(dest => dest.UserAddress, opt => opt.MapFrom(src => src.UserAddress))
            .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
            .ForMember(dest => dest.OrderItems, opt => opt.MapFrom(src => src.OrderItems))
            .ReverseMap();

        // OrderItem -> OrderItemDto mapping
        CreateMap<OrderItem, OrderItemDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.ProductName))
            .ForMember(dest => dest.ProductTitle, opt => opt.MapFrom(src => src.ProductTitle))
            .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.Price))
            .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity))
            .ForMember(dest => dest.BrandName, opt => opt.MapFrom(src => src.Product.Brand.Name))
            .ForMember(dest => dest.ProductFeatureValues, opt => opt.MapFrom(src => src.Product.ProductFeatureValues
                .Where(pfv => pfv.FeatureValue != null && pfv.FeatureValue.Feature != null)
                .Select(pfv => new ProductFeatureValueDto
                {
                    FeatureId = pfv.FeatureValue.Feature.Id,
                    FeatureName = pfv.FeatureValue.Feature.Name,
                    FeatureValueId = pfv.FeatureValue.Id,
                    FeatureValueName = pfv.FeatureValue.Name
                })))
            .ForMember(dest => dest.ShowcaseImage, opt => 
                opt.MapFrom(src => src.Product.ProductImageFiles.FirstOrDefault(pif => pif.Showcase)));
        
        CreateMap<Order, GetOrdersByUserQueryResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.OrderDate, opt => opt.MapFrom(src => src.OrderDate))
            .ForMember(dest => dest.OrderCode, opt => opt.MapFrom(src => src.OrderCode))
            .ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src => src.TotalPrice))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.UserName))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.OrderItems, opt => opt.MapFrom(src => src.OrderItems)) // OrderItems için de mapping yapılmalı
            .ReverseMap();
        
        CreateMap<IPaginate<Order>, GetListResponse<GetOrdersByUserQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items)) // IPaginate'den gelen Order'ları GetListResponse'daki Items'a mapler
            .ReverseMap();
        CreateMap<OrderDto, GetUserOrderByIdQueryResponse>()
            .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.OrderId))
            .ForMember(dest => dest.OrderCode, opt => opt.MapFrom(src => src.OrderCode))
            .ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src => src.TotalPrice))
            .ForMember(dest => dest.OrderDate, opt => opt.MapFrom(src => src.OrderDate))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.UserName))
            .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
            .ForMember(dest => dest.UserAddress, opt => opt.MapFrom(src => src.UserAddress))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.OrderItems, opt => opt.MapFrom(src => src.OrderItems))
            .ReverseMap();
        
        CreateMap<Order ,OrderDto>()
            .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.OrderCode, opt => opt.MapFrom(src => src.OrderCode))
            .ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src => src.TotalPrice))
            .ForMember(dest => dest.OrderDate, opt => opt.MapFrom(src => src.OrderDate))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.UserName))
            .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
            .ForMember(dest => dest.UserAddress, opt => opt.MapFrom(src => src.UserAddress))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.OrderItems, opt => opt.MapFrom(src => src.OrderItems))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email))
            .ReverseMap();
        
        CreateMap<Order, GetUserOrderByIdQueryResponse>()
            .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.OrderCode, opt => opt.MapFrom(src => src.OrderCode))
            .ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src => src.TotalPrice))
            .ForMember(dest => dest.OrderDate, opt => opt.MapFrom(src => src.OrderDate))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.UserName))
            .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
            .ForMember(dest => dest.UserAddress, opt => opt.MapFrom(src => src.UserAddress))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.OrderItems, opt => opt.MapFrom(src => src.OrderItems))
            .ReverseMap();
        
    }

}