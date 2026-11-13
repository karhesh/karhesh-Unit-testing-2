
namespace Simulation.Checkout.Tests.TestData;

using AutoFixture;
using Simulation.Checkout.Domain;

public class OrderTestData
{

    public Order CreateOrder()
    {
        var fixture = new Fixture();
        var orderItem = fixture.Build<OrderItem>()
        .With(o => o.Quantity, 1).With(o => o.UnitPrice, 100m).Create();
        var newOrder = fixture.Build<Order>().With(o => o.Items, new List<OrderItem> { orderItem }).Create();
        return newOrder;
    }


}