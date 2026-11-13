using Bogus;
using Bogus.DataSets;
using Simulation.Checkout.Domain;

namespace Simulation.Checkout.Tests.TestData;

public class CustomerTestData
{
    public static Faker<Order> CreateOrderFaker()
    {
        return new Faker<Order>().UseSeed(2026)
        .RuleFor(o => o.CustomerName, f => f.Name.FullName())
        .RuleFor(o => o.CustomerEmail, f => f.Internet.Email());
    }
}