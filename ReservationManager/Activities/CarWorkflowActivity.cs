using Dapr.Client;
using Dapr.Workflow;

namespace ReservationManager.Activities;

public abstract class CarWorkflowActivity<TRequest> : WorkflowActivity<TRequest, bool>
{
    protected readonly ILogger Logger;
    private readonly ICallbackBindingNameProvider _callbackBindingNameProvider;
    private readonly DaprClient _daprClient;

    protected CarWorkflowActivity(ILoggerFactory loggerFactory, ICallbackBindingNameProvider callbackBindingNameProvider, DaprClient client)
    {
        Logger = loggerFactory.CreateLogger(GetType());
        _callbackBindingNameProvider = callbackBindingNameProvider;
        _daprClient = client;
    }

    protected abstract string QueueName { get; }

    public override async Task<bool> RunAsync(WorkflowActivityContext context, TRequest request)
    {
        try
        {
            LogInformation(request);

            var metadata = new Dictionary<string, string>
            {
                { "x-callback-binding-name", _callbackBindingNameProvider.CallbackBindingName },
                { "x-message-dispatch-time", DateTime.UtcNow.ToString("o") },
                { "x-callback-workflow-id", context.InstanceId },
                { "x-callback-event-name", GetType().Name }
            };

            await _daprClient.InvokeBindingAsync(QueueName, "create", request, metadata);
            return true;
        }
        catch (Exception e)
        {
            LogError(e, request);
            return false;
        }
    }

    protected abstract void LogInformation(TRequest request);
    protected abstract void LogError(Exception e, TRequest request);
}