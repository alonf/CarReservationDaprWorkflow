namespace ReservationManager.DTO.InventoryDto;

public class CarInventoryRequest
{
    // ReSharper disable UnusedAutoPropertyAccessor.Global
    public CarInventoryRequestActionType ActionType { get; set; }
    public required string CarClass { get; set; }
    public Guid OrderId { get; set; }
}