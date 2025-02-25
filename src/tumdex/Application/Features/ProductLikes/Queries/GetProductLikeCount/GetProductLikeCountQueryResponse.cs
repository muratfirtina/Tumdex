using Core.Application.Responses;

namespace Application.Features.ProductLikes.Queries.GetProductLikeCount;

public class GetProductLikeCountQueryResponse : IResponse
{
    public int LikeCount { get; set; }
}