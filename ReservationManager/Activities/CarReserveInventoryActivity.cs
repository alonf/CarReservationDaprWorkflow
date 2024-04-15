using Dapr.Client;
using ReservationManager.DTO.InventoryDto;

namespace ReservationManager.Activities;

// ReSharper disable once ClassNeverInstantiated.Global
public class CarReserveInventoryActivity(
    ILoggerFactory loggerFactory,
    ICallbackBindingNameProvider callbackBindingNameProvider,
    DaprClient client)
    : CarWorkflowActivity<CarInventoryRequest>(loggerFactory, callbackBindingNameProvider, client)
{
    protected override string QueueName => "inventory-queue";

    protected override void LogInformation(CarInventoryRequest request)
    {
        string actionType = request.ActionType.ToString();
        string logMessage;

        switch (actionType)
        {
            case "Create":
                logMessage = $"Reserving inventory for order id: {request.OrderId}";
                break;
            case "Update":
                logMessage = $"Updating inventory for order id: {request.OrderId}";
                break;
            case "Cancel":
                logMessage = $"Canceling inventory reservation for order id: {request.OrderId}";
                break;
            default:
                logMessage = $"Unknown action type for order id: {request.OrderId}";
                break;
        }

        Logger.LogInformation(logMessage);
    }

    protected override void LogError(Exception e, CarInventoryRequest request)
    {
        Logger.LogError(e, "Error in CarReserveInventoryActivity for order id: {orderId}", request.OrderId);
    }
}