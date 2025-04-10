using System.Linq.Expressions;
using Application.Features.Dashboard.Dtos;
using Application.Repositories;
using AutoMapper;
using Domain.Entities;
using Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;


namespace Application.Features.Dashboard.Queries.GetDashboardStatistics;

public class GetDashboardStatisticsQuery : IRequest<GetDashboardStatisticsResponse>
{
    public string TimeFrame { get; set; } = "all";
    
    public class GetDashboardStatisticsQueryHandler : IRequestHandler<GetDashboardStatisticsQuery, GetDashboardStatisticsResponse>
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IBrandRepository _brandRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly UserManager<AppUser> _userManager;
        private readonly IMapper _mapper;

        public GetDashboardStatisticsQueryHandler(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IBrandRepository brandRepository,
            IOrderRepository orderRepository,
            UserManager<AppUser> userManager,
            IMapper mapper)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _brandRepository = brandRepository;
            _orderRepository = orderRepository;
            _userManager = userManager;
            _mapper = mapper;
        }

        public async Task<GetDashboardStatisticsResponse> Handle(GetDashboardStatisticsQuery request, CancellationToken cancellationToken)
        {
            // Convert timeFrame to a DateTime range
            DateTime? startDate = GetStartDateFromTimeFrame(request.TimeFrame);

            // User count
            IQueryable<AppUser> userQuery = _userManager.Users;
            if (startDate.HasValue)
                userQuery = userQuery.Where(u => u.CreatedDate >= startDate.Value);
            int userCount = await userQuery.CountAsync(cancellationToken);

            // Product count
            Expression<Func<Product, bool>> productPredicate = p => true;
            if (startDate.HasValue)
                productPredicate = p => p.CreatedDate >= startDate.Value;
            int productCount = await _productRepository.GetListAsync(productPredicate, index: 0, size: 0, enableTracking: false, cancellationToken: cancellationToken)
                .ContinueWith(t => t.Result.Count, cancellationToken);

            // Order count & total revenue
            Expression<Func<Order, bool>> orderPredicate = o => true;
            if (startDate.HasValue)
                orderPredicate = o => o.CreatedDate >= startDate.Value;
            var orders = await _orderRepository.GetListAsync(orderPredicate, index: 0, size: 0, enableTracking: false, cancellationToken: cancellationToken);
            int orderCount = orders.Count;
            decimal? totalRevenue = orders.Items.Sum(o => o.TotalPrice);

            // Category count
            Expression<Func<Category, bool>> categoryPredicate = c => true;
            if (startDate.HasValue)
                categoryPredicate = c => c.CreatedDate >= startDate.Value;
            int categoryCount = await _categoryRepository.GetListAsync(categoryPredicate, index: 0, size: 0, enableTracking: false, cancellationToken: cancellationToken)
                .ContinueWith(t => t.Result.Count, cancellationToken);

            // Brand count
            Expression<Func<Brand, bool>> brandPredicate = b => true;
            if (startDate.HasValue)
                brandPredicate = b => b.CreatedDate >= startDate.Value;
            int brandCount = await _brandRepository.GetListAsync(brandPredicate, index: 0, size: 0, enableTracking: false, cancellationToken: cancellationToken)
                .ContinueWith(t => t.Result.Count, cancellationToken);

            var statistics = new DashboardStatisticsDto
            {
                UserCount = userCount,
                ProductCount = productCount,
                OrderCount = orderCount,
                TotalRevenue = totalRevenue,
                CategoryCount = categoryCount,
                BrandCount = brandCount,
                TimeFrame = request.TimeFrame
            };

            return new GetDashboardStatisticsResponse { Statistics = statistics };
        }

        private DateTime? GetStartDateFromTimeFrame(string timeFrame)
        {
            DateTime now = DateTime.UtcNow;
            
            return timeFrame switch
            {
                "day" => now.AddDays(-1),
                "week" => now.AddDays(-7),
                "month" => now.AddMonths(-1),
                "days10" => now.AddDays(-10),
                "days30" => now.AddDays(-30),
                _ => null, // "all" or any other value returns null (no time filtering)
            };
        }
    }
}