using Core.Application.Responses;

namespace Application.Features.ProductLikes.Queries.GetUserLikedProductIds;

public class GetUserLikedProductIdsResponse : IResponse
{
    public List<string> LikedProductIds { get; set; }
}