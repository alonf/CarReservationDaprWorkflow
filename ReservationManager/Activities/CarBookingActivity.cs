using Dapr.Client;
using ReservationManager.DTO.BookingDto;

namespace ReservationManager.Activities;

// CarBookingActivity implementation
// ReSharper disable once ClassNeverInstantiated.Global
public class CarBookingActivity(
    ILoggerFactory loggerFactory,
    ICallbackBindingNameProvider callbackBindingNameProvider,
    DaprClient client)
    : CarWorkflowActivity<CarReservationRequest>(loggerFactory, callbackBindingNameProvider, client)
{
    protected override string QueueName => "booking-queue";

    protected override void LogInformation(CarReservationRequest request)
    {
        string actionType = request.ActionType.ToString();
        string logMessage;

        switch (actionType)
        {
            case "Create":
                logMessage = $"Booking car reservation for reservation id: {request.ReservationId}";
                break;
            case "Update":
                logMessage = $"Updating car reservation for reservation id: {request.ReservationId}";
                break;
            case "Cancel":
                logMessage = $"Canceling car reservation for reservation id: {request.ReservationId}";
                break;
            default:
                logMessage = $"Unknown action type for reservation id: {request.ReservationId}";
                break;
        }

        Logger.LogInformation(logMessage);
    }

    protected override void LogError(Exception e, CarReservationRequest request)
    {
        Logger.LogError(e, "Error in CarBookingActivity for reservation id: {reservationId}", request.ReservationId);
    }
}
