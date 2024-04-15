using System.Text.Json.Serialization;

namespace BookingManager;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActionType
{
    Reserve,
    Cancel
}