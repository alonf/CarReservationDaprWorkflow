using System.Text.Json.Serialization;

namespace ReservationManager.DTO.BillingDto;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BillingRequestActionType
{
    Charge,
    Refund
}