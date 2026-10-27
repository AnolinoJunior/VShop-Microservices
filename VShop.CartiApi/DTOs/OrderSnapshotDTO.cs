namespace VShop.CartiApi.DTOs;

public sealed record OrderSnapshotDTO(string? CouponCode, IReadOnlyList<OrderSnapshotItemDTO> Items);
public sealed record OrderSnapshotItemDTO(int ProductId, string ProductName, string? ProductDescription, int Quantity, decimal UnitPrice);
