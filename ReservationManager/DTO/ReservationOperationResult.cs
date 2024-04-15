using System.Text.Json.Serialization;

namespace ReservationManager.DTO;

// ReSharper disable once ClassNeverInstantiated.Global
public record ReservationOperationResult
{
    // ReSharper disable UnusedAutoPropertyAccessor.Global
    [JsonPropertyName("reservationId")]
    public Guid ReservationId { get; set; }
    [JsonPropertyName("isSuccess")]
    public bool IsSuccess { get; set; }
}