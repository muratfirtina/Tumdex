using System.Diagnostics;
using Application.Abstraction.Services;
using Application.Consts;
using Application.CustomAttributes;
using Application.Enums;
using Application.Features.OrderItems.Commands.Delete;
using Application.Features.OrderItems.Commands.Update;
using Application.Features.Orders.Commands;
using Application.Features.Orders.Commands.Create;
using Application.Features.Orders.Commands.Delete;
using Application.Features.Orders.Commands.Update;
using Application.Features.Orders.Queries;
using Application.Features.Orders.Queries.GetAll;
using Application.Features.Orders.Queries.GetById;
using Application.Features.Orders.Queries.GetOrdersByDynamic;
using Application.Features.Orders.Queries.GetOrdersByUser;
using Application.Features.Orders.Queries.GetUserOrderById;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Dynamic;
using Domain.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Admin")]
    public class OrdersController : BaseController
    {
        private readonly IMetricsService _metricsService;

        public OrdersController(IMetricsService metricsService)
        {
            _metricsService = metricsService;
        }

        [HttpPost("convert-cart-to-order")]
        [AuthorizeDefinition(ActionType = ActionType.Writing, Definition = "Convert Cart To Order", Menu = AuthorizeDefinitionConstants.Orders)]
        public async Task<IActionResult> ConvertCartToOrder([FromBody] ConvertCartToOrderCommand command)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                ConvertCartToOrderCommandResponse response = await Mediator.Send(command);
                stopwatch.Stop();
        
                // Sipariş tamamlama metrikleri
                _metricsService.RecordCheckoutDuration(
                    "registered", 
                    "standard", 
                    stopwatch.Elapsed.TotalSeconds);
        
                return Ok(response);
            }
            catch (Exception ex)
            {
                _metricsService.IncrementFailedPayments(
                    ex.GetType().Name,
                    "order_creation");
                throw;
            }
        }
        
        [HttpGet]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get All Orders", Menu = AuthorizeDefinitionConstants.Orders)]
        public async Task<IActionResult> GetList([FromQuery] PageRequest pageRequest)
        {
            GetListResponse<GetAllOrdersQueryResponse> response = await Mediator.Send(new GetAllOrdersQuery { PageRequest = pageRequest });
            return Ok(response);
        }
        
        [HttpPost ("get-orders-by-dynamic")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get Orders By Dynamic", Menu = AuthorizeDefinitionConstants.Orders)]
        public async Task<IActionResult> GetOrdersByDynamic([FromQuery] PageRequest pageRequest, [FromBody] DynamicQuery? dynamicQuery = null)
        {
            GetListResponse<GetOrdersByDynamicQueryResponse> response = await Mediator.Send(new GetOrdersByDynamicQuery { PageRequest = pageRequest, DynamicQuery = dynamicQuery });
            return Ok(response);
        }
        
        [HttpGet("{id}")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get Order By Id", Menu = AuthorizeDefinitionConstants.Orders)]
        public async Task<IActionResult> GetById([FromRoute] GetOrderByIdQuery getOrderByIdQuery)
        {
            GetOrderByIdQueryResponse response = await Mediator.Send (getOrderByIdQuery);
            return Ok(response);
        }
        
        [HttpPut("update")]
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Update Order", Menu = AuthorizeDefinitionConstants.Orders)]
        public async Task<IActionResult> Update([FromBody] UpdateOrderCommand updateOrderCommand)
        {
            var result = await Mediator.Send(updateOrderCommand);
            if (!result)
                return BadRequest("Failed to update the order.");

            return Ok(new { Message = "Order updated successfully." });
        }
        
        [HttpDelete("{id}")]
        [AuthorizeDefinition(ActionType = ActionType.Deleting, Definition = "Delete Order", Menu = AuthorizeDefinitionConstants.Orders)]
        public async Task<IActionResult> Delete([FromRoute] string id)
        {
            DeletedOrderCommandResponse response = await Mediator.Send(new DeleteOrderCommand { Id = id });
            return Ok(response);
        }

        [HttpDelete("delete-item/{id}")]
        [AuthorizeDefinition(ActionType = ActionType.Deleting, Definition = "Delete Order Item", Menu = AuthorizeDefinitionConstants.Orders)]
        public async Task<IActionResult> DeleteOrderItem([FromRoute] string id)
        {
            var result = await Mediator.Send(new DeleteOrderItemCommand { Id = id });
            if (!result)
                return BadRequest("Failed to delete the order item.");

            return Ok(new { Message = "Order item deleted successfully." });
        }
        
        [HttpPut("update-item")]
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Update Order Item", Menu = AuthorizeDefinitionConstants.Orders)]
        public async Task<IActionResult> UpdateOrderItem([FromBody] UpdateOrderItemCommand updateOrderItemCommand)
        {
            var result = await Mediator.Send(updateOrderItemCommand);
            if (!result)
                return BadRequest("Failed to update the order item.");

            return Ok(new { Message = "Order item updated successfully." });
        }
        
        [HttpGet("user-orders")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get Orders By User", Menu = AuthorizeDefinitionConstants.Orders)]
        public async Task<IActionResult> GetOrdersByUser([FromQuery] PageRequest pageRequest, [FromQuery] string? searchTerm, [FromQuery] string? dateRange, [FromQuery] OrderStatus orderStatus)
        {
            GetListResponse<GetOrdersByUserQueryResponse> response = await Mediator.Send(new GetOrdersByUserQuery { PageRequest = pageRequest, SearchTerm = searchTerm, DateRange = dateRange, OrderStatus = orderStatus });
            return Ok(response);
        }
        
        [HttpGet("user-order/{id}")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get User Order By Id", Menu = AuthorizeDefinitionConstants.Orders)]
        public async Task<IActionResult> GetUserOrderById([FromRoute] GetUserOrderByIdQuery getUserOrderByIdQuery)
        {
            GetUserOrderByIdQueryResponse response = await Mediator.Send(getUserOrderByIdQuery);
            return Ok(response);
        }
        
    }
}
