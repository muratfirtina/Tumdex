using System.Security.Claims;
using Application.Consts;
using Application.CustomAttributes;
using Application.Enums;
using Application.Features.Products.Commands.Create;
using Application.Features.Products.Commands.DecriptionImageUpload;
using Application.Features.Products.Commands.Delete;
using Application.Features.Products.Commands.TrackProductView;
using Application.Features.Products.Commands.Update;
using Application.Features.Products.Dtos.FilterDto;
using Application.Features.Products.Queries.GetBestSelling;
using Application.Features.Products.Queries.GetByDynamic;
using Application.Features.Products.Queries.GetById;
using Application.Features.Products.Queries.GetList;
using Application.Features.Products.Queries.GetMostLikedProducts;
using Application.Features.Products.Queries.GetMostViewed;
using Application.Features.Products.Queries.GetRandoms;
using Application.Features.Products.Queries.GetRandoms.GetRandomByCategory;
using Application.Features.Products.Queries.GetRandoms.GetRandomProducts;
using Application.Features.Products.Queries.GetRandoms.GetRandomProductsByProductId;
using Application.Features.Products.Queries.GetRandoms.GetRandomProductsForBrand;
using Application.Features.Products.Queries.SearchAndFilter;
using Application.Features.Products.Queries.SearchAndFilter.Filter;
using Application.Features.Products.Queries.SearchAndFilter.Filter.GetAvailableFilter;
using Application.Features.Products.Queries.SearchAndFilter.Search;
using Application.Storage;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Dynamic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : BaseController
    {
        private readonly IStorageService _storageService;

        public ProductsController(IStorageService storageService)
        {
            _storageService = storageService;
        }

        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] PageRequest pageRequest)
        {
            GetListResponse<GetAllProductQueryResponse> response = await Mediator.Send(new GetAllProductQuery { PageRequest = pageRequest });
            return Ok(response);
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById([FromRoute] string id)
        {
            GetByIdProductResponse response = await Mediator.Send(new GetByIdProductQuery { Id = id });
            return Ok(response);
        }
        [HttpPost]
        [Consumes("multipart/form-data")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Writing, Definition = "Create Product", Menu = AuthorizeDefinitionConstants.Products)]
        public async Task<IActionResult> Add([FromForm] CreateProductCommand createProductCommand)
        {
            CreatedProductResponse response = await Mediator.Send(createProductCommand);
            return Created(uri: "", response);
        }
        [HttpDelete("{id}")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Deleting, Definition = "Delete Product", Menu = AuthorizeDefinitionConstants.Products)]
        public async Task<IActionResult> Delete([FromRoute] string id)
        {
            DeletedProductResponse response = await Mediator.Send(new DeleteProductCommand { Id = id });
            return Ok(response);
        }
        [HttpPut]
        [Consumes("multipart/form-data")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Update Product", Menu = AuthorizeDefinitionConstants.Products)]
        public async Task<IActionResult> Update([FromForm] UpdateProductCommand updateProductCommand)
        {
            UpdatedProductResponse response = await Mediator.Send(updateProductCommand);
            return Ok(response);
        }
        [HttpPost("GetList/ByDynamic")]
        public async Task<IActionResult> GetListByDynamic([FromQuery] PageRequest pageRequest, [FromBody] DynamicQuery? dynamicQuery = null)
        {
            GetListResponse<GetListProductByDynamicDto> response = await Mediator.Send(new GetListProductByDynamicQuery { PageRequest = pageRequest, DynamicQuery = dynamicQuery });
            return Ok(response);
        }
        
        [HttpPost("multiple")]
        [Consumes("multipart/form-data")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Writing, Definition = "Create Multiple Products", Menu = AuthorizeDefinitionConstants.Products)]
        public async Task<IActionResult> CreateMultiple([FromForm] CreateMultipleProductsCommand createMultipleProductsCommand)
        {
            List<CreatedProductResponse> response = await Mediator.Send(createMultipleProductsCommand);
            return Created(uri: "", response);
        }
        
        [HttpGet("GetRandomProductsByCategory/{categoryId}")]
        public async Task<IActionResult> GetRandomProductsByCategory(string categoryId, [FromQuery] int count = 4)
        {
            var products = await Mediator.Send(new GetRandomProductsByCategoryQuery { CategoryId = categoryId, Count = count });
            return Ok(products);
        }
        
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? searchTerm, [FromQuery] PageRequest pageRequest)
        {
            SearchResponse response = await Mediator.Send(new SearchProductQuery { SearchTerm = searchTerm, PageRequest = pageRequest });
            return Ok(response);
        }
        
        [HttpPost("filter")]
        public async Task<IActionResult> Filter([FromBody] FilterProductQuery filterQuery)
        {
            // PageRequest bilgisi artık query parametrelerinden değil, body'den geliyor
            GetListResponse<FilterProductQueryResponse> response = await Mediator.Send(filterQuery);
            return Ok(response);
        }
        
        [HttpGet("filters")]
        public async Task<IActionResult> GetFilters([FromQuery] string? searchTerm = null, [FromQuery] string[]? categoryIds = null, [FromQuery] string[]? brandIds = null)
        {
            var filters = await Mediator.Send(new GetAvailableFiltersQuery { 
                SearchTerm = searchTerm,
                CategoryIds = categoryIds,
                BrandIds = brandIds
            });
            return Ok(filters);
        }
        
        [HttpGet("GetRandomsByProductId/{productId}")]
        public async Task<IActionResult> GetRandomsByProductId([FromRoute]string productId)
        {
            GetListResponse<GetRandomProductsByProductIdQueryResponse> response = await Mediator.Send(new GetRandomProductsByProductIdQuery { ProductId = productId });
            return Ok(response);
        }
        
        [HttpGet("most-liked")]
        public async Task<IActionResult> GetMostLikedProducts([FromQuery] int count = 10)
        {
            var query = new GetMostLikedProductQuery() { Count = count };
            var products = await Mediator.Send(query);
            return Ok(products);
        }
        
        [HttpGet("GetRandomsForBrand/{productId}")]
        public async Task<IActionResult> GetRandomsForBrand([FromRoute]string productId)
        {
            GetListResponse<GetRandomProductsForBrandByProductIdQueryResponse> response = await Mediator.Send(new GetRandomProductsForBrandByProductIdQuery { ProductId = productId });
            return Ok(response);
        }
        [HttpPost("upload-description-image")]
        [Consumes("multipart/form-data")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Upload Product Description Image", Menu = AuthorizeDefinitionConstants.Products)]
        public async Task<IActionResult> UploadDescriptionImage(
            [FromForm] UploadDescriptionImageCommand command)
        {
            var response = await Mediator.Send(command);
            return Ok(response);
        }
        
        [HttpPost("upload-description-video")]
        [Consumes("multipart/form-data")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Upload Product Description Video", Menu = AuthorizeDefinitionConstants.Products)]
        public async Task<IActionResult> UploadDescriptionVideo(IFormFile Video)
        {
            if (Video == null || Video.Length == 0)
                return BadRequest("No file uploaded");

            // Dosya formatı kontrolü
            string extension = Path.GetExtension(Video.FileName).ToLower();
            string[] allowedExtensions = { ".mp4", ".webm", ".mov" };
    
            if (!allowedExtensions.Contains(extension))
                return BadRequest("Unsupported file format. Only MP4, WebM and MOV files are allowed.");
    
            // Dosya boyutu kontrolü (50MB)
            if (Video.Length > 52428800) // 50MB in bytes
                return BadRequest("File size exceeds limit of 50MB");

            var uploadedFiles = await _storageService.UploadAsync(
                "description-videos", 
                Guid.NewGuid().ToString(), 
                new List<IFormFile> { Video });

            var result = uploadedFiles.FirstOrDefault();
            if (!string.IsNullOrEmpty(result.url))
            {
                return Ok(new { 
                    Url = result.url,
                    MimeType = Video.ContentType,
                    FileName = result.fileName,
                    FileSize = Video.Length
                });
            }

            return BadRequest("Upload failed");
        }
        
        [HttpGet("most-viewed")]
        public async Task<IActionResult> GetMostViewedProducts([FromQuery] int count = 10)
        {
            var query = new GetMostViewedProductsQuery { Count = count };
            var products = await Mediator.Send(query);
            return Ok(products);
        }
        
        [HttpPost("track-view/{productId}")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Writing, Definition = "Track Product View", Menu = AuthorizeDefinitionConstants.Products)]
        public async Task<IActionResult> TrackProductView([FromRoute] string productId)
        {
            var command = new TrackProductViewCommand { ProductId = productId };
            await Mediator.Send(command);
            return Ok();
        }
        
        [HttpGet("best-selling")]
        public async Task<IActionResult> GetBestSellingProducts([FromQuery] int count = 10)
        {
            var query = new GetBestSellingProductsQuery { Count = count };
            var products = await Mediator.Send(query);
            return Ok(products);
        }

        [HttpGet("random")]
        public async Task<IActionResult> GetRandomProducts([FromQuery] int count = 10)
        {
            var query = new GetRandomProductsQuery { Count = count };
            var products = await Mediator.Send(query);
            return Ok(products);
        }
    }
}
