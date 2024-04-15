using Dapr.Client;
using Dapr.Workflow;
using ReservationManager.DTO.BillingDto;

namespace ReservationManager.Activities;

// ReSharper disable once ClassNeverInstantiated.Global
public class ValidateBillingForReservationActivity(
    ILogger<ValidateBillingForReservationActivity> logger,
    DaprClient daprClient) : WorkflowActivity<string, bool>
{
    public override async Task<bool> RunAsync(WorkflowActivityContext context, string reservationId)
    {
        logger.LogInformation("Validating billing request");

        try
        {
            var billingState =
                await daprClient.InvokeMethodAsync<BillingState>(HttpMethod.Get, "billingmanager",
                    $"/billing-status/{reservationId}");

            return billingState.Status == "Charged";
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error in ValidateBillReservationAsync for reservation id: {reservationId}", reservationId);
            return false;
        }
    }
}