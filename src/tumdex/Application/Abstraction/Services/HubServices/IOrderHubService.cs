namespace Application.Abstraction.Services.HubServices;

public interface IOrderHubService
{
    Task OrderCreatedMessageAsync(string orderId, string message);
    Task OrderStausChangedMessageAsync(string orderId, string status, string message);
    Task OrderUpdatedMessageAsync(string orderId, string message);
}