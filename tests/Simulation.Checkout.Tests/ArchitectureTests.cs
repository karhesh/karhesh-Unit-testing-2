using FluentAssertions;
using NetArchTest.Rules;
using Simulation.Checkout.Application;
using Simulation.Checkout.Application.Abstractions;
using Simulation.Checkout.Domain;
using Simulation.Checkout.Infrastructure;

namespace Simulation.Checkout.Tests;

public class ArchitectureTests
{
    [Fact]
    public void DomainTypes_DomainDoesNotDependOnApplicationOrInfrastructe_ReturnsTrue()
    {
        var result = Types.InAssembly(typeof(Order).Assembly)
        .ShouldNot().HaveDependencyOnAny("Simulation.Checkout.Application", "Simulation.Checkout.Infrastructure")
        .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
    [Fact]
    public void ApplicationTypes_ApplicationDoesNotDependOnInfrastructure_ReturnsTrue()
    {
        var result = Types.InAssembly(typeof(CheckoutService).Assembly)
        .Should().NotHaveDependencyOn("Simulation.Checkout.Infrastructure").GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
    [Fact]
    public void AllInterfaces_WhenCheckingIfAllStartsWithI_ReturnsTrue()
    {
        var result = Types.InAssemblies(new[]
        {
            typeof(Order).Assembly,
            typeof(InMemoryOrderRepository).Assembly,
            typeof(CheckoutService).Assembly
        }).That()
        .AreInterfaces().Should().HaveNameStartingWith("I")
        .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
    [Fact]
    public void IOrderRepository_WhenImplementedByItShouldHaveNameRepository_ReturnsTrue()
    {
        var result = Types.InAssembly(typeof(InMemoryOrderRepository).Assembly)
        .That()
        .ImplementInterface(typeof(IOrderRepository))
        .Should()
        .HaveNameEndingWith("Repository")
        .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}