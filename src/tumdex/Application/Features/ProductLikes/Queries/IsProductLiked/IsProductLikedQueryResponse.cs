using Core.Application.Responses;

namespace Application.Features.ProductLikes.Queries.IsProductLiked;

public class IsProductLikedQueryResponse : IResponse
{
    public bool IsLiked { get; set; }
}