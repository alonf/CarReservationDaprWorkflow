using System.Text.Json.Serialization;

namespace ReservationManager.DTO.BookingDto;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CarReservationRequestActionType
{
    Reserve,
    Cancel
}