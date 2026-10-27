using System.Text.Json.Serialization;

namespace VShop.OrderApi.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CheckoutSagaRequest
{
    public bool SimulateDecline { get; set; }
}
