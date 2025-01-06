using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Dapr.Client;
using Dapr.Workflow;
using OpenTelemetry.Trace;
using ReservationManager.DTO;
using OpenTelemetry.Resources;
using ReservationManager.Activities;
using ReservationManager.Workflows;
using ReservationManager;

var builder = WebApplication.CreateBuilder(args);


builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Register DaprClient
builder.Services.AddControllers().AddDapr().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddDaprWorkflow(options =>
{
    // Note that it's also possible to register a lambda function as the workflow
    // or activity implementation instead of a class.
    options.RegisterWorkflow<CarReservationWorkflow>();

    // These are the activities that get invoked by the workflow(s).
    options.RegisterActivity<CarBillingActivity>();
    options.RegisterActivity<CarBookingActivity>();
    options.RegisterActivity<CarReserveInventoryActivity>();
    options.RegisterActivity<ValidateBillingForReservationActivity>();
});

// Dapr uses a random port for gRPC by default. If we don't know what that port
// is (because this app was started separate from dapr), then assume 4001.
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DAPR_GRPC_PORT")))
{
    Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", "50001");
}

builder.Services.AddOpenTelemetry().WithTracing(tracing =>
{
    tracing.AddAspNetCoreInstrumentation(options =>
    {
        options.Filter = (httpContext) => httpContext.Request.Path != "/healthz";
    });
    tracing.AddHttpClientInstrumentation();
    tracing.AddZipkinExporter(options =>
    {
        options.Endpoint = new Uri("http://zipkin:9411/api/v2/spans");
    }).SetResourceBuilder(
        ResourceBuilder.CreateDefault().AddService("ReservationManagerService"));
});

builder.Services.AddSingleton<ICallbackBindingNameProvider>(
    new CallbackBindingNameProvider("reservation-response-queue"));

builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.MapGet("/reservation/{reservationId}", async ([FromRoute] Guid reservationId, [FromServices] DaprClient daprClient,
    [FromServices] ILogger<Program> logger) =>
{
    logger.LogInformation("Received request to get reservation details for reservation: {ReservationId}", reservationId);

    try
    {
        var reservationInfo = await daprClient.InvokeMethodAsync<BookingInfo>(HttpMethod.Get, "bookingmanager",
            $"/reservations/{reservationId}");

        return Results.Ok(reservationInfo);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error getting reservation details for reservation: {ReservationId}", reservationId);
        return Results.Problem("An error occurred while getting reservation details. Please try again later.");
    }
})
    .WithName("GetReservation")
    .WithOpenApi();


app.MapGet("/reservations/{customerName}", async (
        [FromRoute] string customerName,
        [FromServices] DaprClient daprClient,
        [FromServices] ILogger<Program> logger) =>
{
    logger.LogInformation("Received request to get reservations for customer: {CustomerName}", customerName);

    try
    {
        var customerReservation =
            await daprClient.InvokeMethodAsync<IList<BookingInfo>>(HttpMethod.Get, "bookingmanager",
            $"/customer-reservations?customerName={customerName}");

        return Results.Ok(customerReservation);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error getting reservations for customer: {CustomerName}", customerName);
        return Results.Problem("An error occurred while getting reservations. Please try again later.");
    }
})
    .WithName("GetReservations")
    .WithOpenApi();

app.MapPost("/reserve", async (
        [FromQuery] Guid? reservationId,
        [FromQuery] string customerName,
        [FromQuery] string carClass,
        [FromServices] DaprWorkflowClient daprWorkflowClient,
        [FromServices] ILogger<Program> logger) =>
{

    if (reservationId == null || reservationId == Guid.Empty)
    {
        reservationId = Guid.NewGuid();
    }

    var reservationInfo = new ReservationInfo
    {
        ReservationId = reservationId.Value,
        CustomerName = customerName,
        CarClass = carClass
    };

    logger.LogInformation("Received car reservation request for {CarClass} from {CustomerName}",
               carClass, customerName);

    await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(CarReservationWorkflow), 
        reservationId.Value.ToString("D"), reservationInfo);

    WorkflowState state = await daprWorkflowClient.WaitForWorkflowStartAsync(
        instanceId: reservationId.Value.ToString("D"));

    logger.LogInformation("Workflow {WorkflowId} started with status {Status}",
        reservationId.Value.ToString("D"), state.RuntimeStatus);
    
    return reservationInfo;
})
.WithName("Reserve")
.WithOpenApi();

//app.MapPost("/cancel", async (
//        [FromQuery] Guid reservationId,
//        [FromServices] DaprWorkflowClient daprWorkflowClient,
//        [FromServices] DaprClient daprClient,
//        [FromServices] ILogger<Program> logger) =>
//{
//    logger.LogInformation("Received car reservation cancellation request for {ReservationId}", reservationId);

//    if (reservationId == Guid.Empty)
//    {
//        return Results.BadRequest("Invalid reservation ID.");
//    }

//    BookingInfo? bookingInfo = null;
//    InventoryInfo? inventoryInfo = null;

//    //Get the reservation details from the booking service
//    try
//    {
//        bookingInfo =
//            await daprClient.InvokeMethodAsync<BookingInfo>(HttpMethod.Get, "bookingmanager",
//                $"/reservations/{reservationId}");

//        inventoryInfo =
//            await daprClient.InvokeMethodAsync<InventoryInfo>(HttpMethod.Get, "inventorymanager",
//                                   $"/reservation-state/{reservationId}");
//    }
//    catch (Exception e)
//    {
//        logger.LogError(e, "Error in ValidateBookCarReservationAsync for reservation id: {reservationId}",
//            reservationId);
//        Results.Problem("An error occurred while fetching the reservation details. Please try again later.");
//    }

//    if (bookingInfo == null || inventoryInfo == null)
//    {
//        return Results.NotFound("Reservation not found.");
//    }

//    if (!bookingInfo.IsReserved)
//    {
//        return Results.BadRequest("Reservation is not exist.");
//    }

//    var reservationInfo = new ReservationInfo
//    {
//        ReservationId = reservationId,
//        CustomerName = bookingInfo.CustomerName,
//        CarClass = inventoryInfo.CarClass,
//    };

//    try
//    {
//        var proxy = actorProxyFactory.CreateActorProxy<ICarReservationCancellationActor>(
//            new ActorId(reservationId.ToString("D")), "CarReservationCancellationActor");

//        await proxy.CancelCarReservationAsync(reservationInfo);

//        logger.LogInformation("Successfully cancelled car reservation for {ReservationId}", reservationId);
//        return Results.Ok($"Cancelling process has started for Reservation {reservationId}");
//    }
//    catch (Exception ex)
//    {
//        logger.LogError(ex, "Error cancelling reservation {ReservationId}", reservationId);
//        return Results.Problem("An error occurred while cancelling the reservation. Please try again later.");
//    }
//})
//    .WithName("Cancel")
//    .WithOpenApi();

app.MapPost("/reservation-response-queue", async (
    HttpRequest httpRequest,
    [FromBody] JsonNode payload,
    [FromServices] DaprWorkflowClient daprWorkflowClient,
    [FromServices] ILogger < Program > logger) =>
{

    var callbackEventName = httpRequest.Headers["x-callback-event-name"].FirstOrDefault();
    if (string.IsNullOrEmpty(callbackEventName))
    {
        logger.LogError("x-callback-event-name header is missing or empty.");
        return Results.Ok();
    }

    var workflowId = httpRequest.Headers["x-callback-workflow-id"].FirstOrDefault();
    if (string.IsNullOrEmpty(workflowId))
    {
        logger.LogError("x-callback-workflow-id is missing or empty.");
        return Results.Ok();
    }

    try
    {
        await daprWorkflowClient.RaiseEventAsync(workflowId, callbackEventName, payload);
        logger.LogInformation("Raised event {EventName} for workflow {WorkflowId}", callbackEventName, workflowId);
        
        return Results.Ok();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error raising event {EventName} for workflow {WorkflowId}", callbackEventName, workflowId);
        return Results.Ok();
    }
}).ExcludeFromDescription(); 


app.MapHealthChecks("/healthz");
app.MapControllers();
app.MapSubscribeHandler();

app.UseRouting();

app.Run();