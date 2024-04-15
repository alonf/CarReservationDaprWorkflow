namespace BookingManager;

public record ReservationOperationResult
{
    // ReSharper disable UnusedAutoPropertyAccessor.Global
    public Guid ReservationId { get; set; }
    public bool IsSuccess { get; set; }
}