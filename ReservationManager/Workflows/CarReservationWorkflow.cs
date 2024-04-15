using Dapr.Workflow;
using ReservationManager.Activities;
using ReservationManager.DTO;
using ReservationManager.DTO.BookingDto;
using ReservationManager.DTO.InventoryDto;
using ReservationManager.DTO.BillingDto;

namespace ReservationManager.Workflows;

// ReSharper disable once ClassNeverInstantiated.Global
public class CarReservationWorkflow : Workflow<ReservationInfo, bool>
{
    public override async Task<bool> RunAsync(WorkflowContext context, ReservationInfo reservationInfo)
    {
        var isBooked = false;
        var isInventoryReserved = false;
        var isCharged = false;

        try
        {
            await BookCar(context, reservationInfo);
            context.SetCustomStatus("Car booked request has sent successfully");

            await ReserveInventory(context, reservationInfo);
            context.SetCustomStatus("Inventory reserved request has sent successfully");

            (isBooked, isInventoryReserved) = await WaitForActivities(context);

            if (!isBooked || !isInventoryReserved)
            {
                context.SetCustomStatus("Error booking a car or reserving inventory");
                throw new Exception("Error booking a car or reserving inventory");
            }

            isCharged = await ChargeCustomer(context, reservationInfo);
            context.SetCustomStatus("Customer charged request has sent successfully");

            if (isCharged)
            {
                isCharged = await WaitForBillingOperation(context, reservationInfo);
            }

            if (isCharged) 
                return true;

            //else
            context.SetCustomStatus("Error charging the customer");
            throw new Exception("Error charging the customer");
        }
        catch (Exception e)
        {
            if (isBooked)
            {
                await context.CallActivityAsync(nameof(CarBookingActivity), new CarReservationRequest
                {
                    ActionType = CarReservationRequestActionType.Cancel,
                    CarClass = reservationInfo.CarClass,
                    CustomerName = reservationInfo.CustomerName,
                    ReservationId = reservationInfo.ReservationId
                });
                context.SetCustomStatus("Car booking cancellation request has sent");
            }

            if (isInventoryReserved)
            {
                await context.CallActivityAsync(nameof(CarReserveInventoryActivity), new CarInventoryRequest
                {
                    ActionType = CarInventoryRequestActionType.Cancel,
                    CarClass = reservationInfo.CarClass,
                    OrderId = reservationInfo.ReservationId
                });
                context.SetCustomStatus("Inventory cancellation request has sent");
            }

            if (isCharged)
            {
                await context.CallActivityAsync(nameof(CarBillingActivity), new BillingRequest
                {
                    ActionType = BillingRequestActionType.Refund,
                    CarClass = reservationInfo.CarClass,
                    CustomerName = reservationInfo.CustomerName,
                    ReservationId = reservationInfo.ReservationId
                });
                context.SetCustomStatus("Customer refunded request has sent");
            }

            context.SetCustomStatus($"Error in CarReservationWorkflow, error:{e.Message}");
            return false;
        }
    }

    private async Task BookCar(WorkflowContext context, ReservationInfo reservationInfo)
    {
        CarReservationRequest carReservationRequest = new()
        {
            ActionType = CarReservationRequestActionType.Reserve,
            CarClass = reservationInfo.CarClass,
            CustomerName = reservationInfo.CustomerName,
            ReservationId = reservationInfo.ReservationId
        };

        var bookingResult = await context.CallActivityAsync<bool>(nameof(CarBookingActivity), carReservationRequest);

        if (!bookingResult)
        {
            context.SetCustomStatus("Error booking a car");
            throw new Exception("Error booking a car");
        }

        context.SetCustomStatus("Car booked successfully");
    }

    private async Task ReserveInventory(WorkflowContext context, ReservationInfo reservationInfo)
    {
        var carInventoryRequest = new CarInventoryRequest()
        {
            ActionType = CarInventoryRequestActionType.Reserve,
            CarClass = reservationInfo.CarClass,
            OrderId = reservationInfo.ReservationId,
        };

        var reserveInventoryRequestResult = await context.CallActivityAsync<bool>(nameof(CarReserveInventoryActivity), carInventoryRequest);

        if (!reserveInventoryRequestResult)
        {
            context.SetCustomStatus("Error reserving inventory");
            throw new Exception("Error reserving inventory");
        }

        context.SetCustomStatus("Inventory reserved successfully");
    }

    private async Task<(bool hasReservationOperationSucceedded, bool hasInventoryOperationSucceedded)>
        WaitForActivities(WorkflowContext context)
    {
        Task<ReservationOperationResult> reservationOperationResultTask = context.WaitForExternalEventAsync<ReservationOperationResult>(
            "CarBookingActivity",
            TimeSpan.FromMinutes(2));

        Task<ReservationOperationResult> inventoryReservationOperationResultTask = context.WaitForExternalEventAsync<ReservationOperationResult>(
            "CarReserveInventoryActivity",
            TimeSpan.FromMinutes(2));

        await Task.WhenAll(reservationOperationResultTask, inventoryReservationOperationResultTask);

        return (reservationOperationResultTask.Result.IsSuccess, inventoryReservationOperationResultTask.Result.IsSuccess);
    }

    private async Task<bool> ChargeCustomer(WorkflowContext context, ReservationInfo reservationInfo)
    {
        var billingRequest = new BillingRequest
        {
            ReservationId = reservationInfo.ReservationId,
            CustomerName = reservationInfo.CustomerName,
            ActionType = BillingRequestActionType.Charge,
            CarClass = reservationInfo.CarClass
        };

        var billingResult = await context.CallActivityAsync<bool>(nameof(CarBillingActivity), billingRequest);

        if (!billingResult)
        {
            context.SetCustomStatus("Error billing the customer");
            throw new Exception("Error billing the customer");
        }

        context.SetCustomStatus("Customer charged successfully");

        return true;
    }

    private async Task<bool> WaitForBillingOperation(WorkflowContext context, ReservationInfo reservationInfo)
    {
        for (int i = 1; i <= 3; i++)
        {
            var billingState = await context.CallActivityAsync<bool>(nameof(ValidateBillingForReservationActivity),
                    reservationInfo.ReservationId);

            if (billingState)
                return true;

            await Task.Delay(i * 1000);
        }
        return false;
    }
}


