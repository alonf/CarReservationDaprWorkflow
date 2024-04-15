namespace ReservationManager;

public class CallbackBindingNameProvider(string bindingName) : ICallbackBindingNameProvider
{
    public string CallbackBindingName { get; } = bindingName;
}