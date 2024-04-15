using Dapr.Client;
using ReservationManager.DTO.BillingDto;

namespace ReservationManager.Activities;

// ReSharper disable once ClassNeverInstantiated.Global
public class CarBillingActivity(
    ILoggerFactory loggerFactory,
    ICallbackBindingNameProvider callbackBindingNameProvider,
    DaprClient client)
    : CarWorkflowActivity<BillingRequest>(loggerFactory, callbackBindingNameProvider, client)
{
    protected override string QueueName => "billing-queue";

    protected override void LogInformation(BillingRequest request)
    {
        string actionType = request.ActionType.ToString();
        string logMessage;

        switch (actionType)
        {
            case "Create":
                logMessage = $"Billing car for reservation id: {request.ReservationId}";
                break;
            case "Update":
                logMessage = $"Updating billing for reservation id: {request.ReservationId}";
                break;
            case "Cancel":
                logMessage = $"Canceling billing for reservation id: {request.ReservationId}";
                break;
            default:
                logMessage = $"Unknown action type for reservation id: {request.ReservationId}";
                break;
        }

        Logger.LogInformation(logMessage);
    }

    protected override void LogError(Exception e, BillingRequest request)
    {
        Logger.LogError(e, "Error in CarBillingActivity for reservation id: {reservationId}", request.ReservationId);
    }
}

