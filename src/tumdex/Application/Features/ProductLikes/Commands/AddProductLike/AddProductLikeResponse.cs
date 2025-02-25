using Core.Application.Responses;

namespace Application.Features.ProductLikes.Commands.AddProductLike;

public class AddProductLikeResponse:IResponse
{
    public bool IsLiked { get; set; }
}