using System.Text.Json.Serialization;

namespace BillingManager;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActionType
{
    Charge,
    Refund
}