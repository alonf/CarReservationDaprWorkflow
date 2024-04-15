namespace InventoryManager;

public record ReservationState
{
    // ReSharper disable UnusedAutoPropertyAccessor.Global
    public Guid Id { get; set; }
    public DateTime LastUpdateTime { get; init; }
    public required string CarClass { get; set; }
    public bool IsReserved { get; set; }
}