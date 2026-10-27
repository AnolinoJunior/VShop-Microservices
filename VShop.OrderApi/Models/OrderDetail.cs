namespace VShop.OrderApi.Models;

public class OrderDetail
{
    public long OrderDetailId { get; set; }
    public long OrderHeaderId { get; set; }
    public OrderHeader OrderHeader { get; set; } = null!;
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductDescription { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}
