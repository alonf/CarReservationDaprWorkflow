using System.Text.Json.Serialization;

namespace ReservationManager.DTO.InventoryDto;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CarInventoryRequestActionType
{
    Reserve,
    Cancel
}