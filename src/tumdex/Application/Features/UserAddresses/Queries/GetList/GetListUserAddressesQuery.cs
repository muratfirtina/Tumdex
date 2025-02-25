using Application.Features.UserAddresses.Dtos;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using MediatR;

namespace Application.Features.UserAddresses.Queries.GetList;

public class GetListUserAddressesQuery : IRequest<GetListResponse<GetListUserAdressesQueryResponse>>
{

    public class GetListUserAddressesQueryHandler : IRequestHandler<GetListUserAddressesQuery, GetListResponse<GetListUserAdressesQueryResponse>>
    {
        private readonly IUserAddressRepository _addressRepository;
        private readonly IMapper _mapper;

        public GetListUserAddressesQueryHandler(IUserAddressRepository addressRepository, IMapper mapper)
        {
            _addressRepository = addressRepository;
            _mapper = mapper;
        }

        public async Task<GetListResponse<GetListUserAdressesQueryResponse>> Handle(GetListUserAddressesQuery request, CancellationToken cancellationToken)
        {
            var addresses = await _addressRepository.GetUserAddressesAsync();
            var response = _mapper.Map<GetListResponse<GetListUserAdressesQueryResponse>>(addresses);
            return response;
        }
        
    }
}