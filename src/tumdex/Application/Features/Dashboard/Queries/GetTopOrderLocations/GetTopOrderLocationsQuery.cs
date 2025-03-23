using Application.Repositories;
using AutoMapper;
using MediatR;

namespace Application.Features.Dashboard.Queries.GetTopOrderLocations;

public class GetTopOrderLocationsQuery : IRequest<GetTopOrderLocationsResponse>
{
    public string TimeFrame { get; set; } = "all";
    public int Count { get; set; } = 10;
    
    public class GetTopOrderLocationsQueryHandler : IRequestHandler<GetTopOrderLocationsQuery, GetTopOrderLocationsResponse>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IMapper _mapper;

        public GetTopOrderLocationsQueryHandler(
            IOrderRepository orderRepository,
            IMapper mapper)
        {
            _orderRepository = orderRepository;
            _mapper = mapper;
        }

        public async Task<GetTopOrderLocationsResponse> Handle(GetTopOrderLocationsQuery request, CancellationToken cancellationToken)
        {
            DateTime? startDate = GetStartDateFromTimeFrame(request.TimeFrame);

            // En çok sipariş verilen lokasyonları al
            var topLocations = await _orderRepository.GetTopOrderLocationsByTimeFrameAsync(request.Count, startDate, cancellationToken);

            return new GetTopOrderLocationsResponse { Locations = topLocations };
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