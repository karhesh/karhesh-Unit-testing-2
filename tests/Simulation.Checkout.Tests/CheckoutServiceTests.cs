using AutoFixture;
using Bogus.DataSets;
using FluentAssertions;
using Moq;
using Simulation.Checkout.Application;
using Simulation.Checkout.Application.Abstractions;
using Simulation.Checkout.Domain;
using Simulation.Checkout.Domain.Exceptions;
using Simulation.Checkout.Tests.TestData;

namespace Simulation.Checkout.Tests;

public class CheckoutServiceTests
{
    [Fact]
    public async Task CheckoutAsync_WhenOrderIsEmpty_ShouldThrowEmptyOrderException()
    {
        var orderRepository = new Mock<IOrderRepository>();
        var inventoryService = new Mock<IInventoryService>();
        var notificationService = new Mock<INotificationService>();
        var checkoutService = new CheckoutService(
            orderRepository.Object,
            inventoryService.Object,
            notificationService.Object);
        var order = new Order
        {
            CustomerName = "Kareem",
            CustomerEmail = "kareem@gmail.com",
            Items = new List<OrderItem>()
        };

        Func<Task> action = async () => await checkoutService.CheckoutAsync(order);

        await action.Should().ThrowAsync<EmptyOrderException>().WithMessage("An order must contain at least one item.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CheckoutAsync_InvalidQuantity_ThrowsInvalidQuantityException(int quantity)
    {
        var orderRepository = new Mock<IOrderRepository>();
        var inventoryService = new Mock<IInventoryService>();
        var notificationService = new Mock<INotificationService>();

        var checkoutService = new CheckoutService(
            orderRepository.Object,
            inventoryService.Object,
            notificationService.Object);

        var orderItem = new OrderItem
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Product A",
            UnitPrice = 100,
            Quantity = quantity
        };
        var order = new Order
        {
            CustomerName = "Kareem",
            CustomerEmail = "kareem@gmail.com",
            Items = [orderItem]
        };

        Func<Task> action = () => checkoutService.CheckoutAsync(order);

        await action.Should().ThrowAsync<InvalidQuantityException>()
        .WithMessage($"Quantity for '{orderItem.ProductName}' must be greater than zero.");
    }

    [Fact]
    public async Task CheckoutAsync_InvalidPrice_ThrowsInvalidPriceException()
    {
        var orderRepository = new Mock<IOrderRepository>();
        var inventoryService = new Mock<IInventoryService>();
        var notificationService = new Mock<INotificationService>();

        var checkoutService = new CheckoutService(
            orderRepository.Object,
            inventoryService.Object,
            notificationService.Object);

        var orderItem = new OrderItem
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Product A",
            UnitPrice = -0.01m,
            Quantity = 2
        };
        var order = new Order
        {
            CustomerName = "Kareem",
            CustomerEmail = "kareem@gmail.com",
            Items = [orderItem]
        };

        Func<Task> action = () => checkoutService.CheckoutAsync(order);

        await action.Should().ThrowAsync<InvalidPriceException>()
        .WithMessage($"Unit price for '{orderItem.ProductName}' cannot be negative.");
    }

    [Fact]
    public async Task CheckoutAsync_PriceIsZero_CheckoutSucceeds()
    {
        var orderRepository = new Mock<IOrderRepository>();
        var inventoryService = new Mock<IInventoryService>();
        var notificationService = new Mock<INotificationService>();

        var checkoutService = new CheckoutService(
            orderRepository.Object,
            inventoryService.Object,
            notificationService.Object);

        var orderItem = new OrderItem
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Product A",
            UnitPrice = 0m,
            Quantity = 2
        };
        var order = new Order
        {
            CustomerName = "Kareem",
            CustomerEmail = "kareem@gmail.com",
            Items = [orderItem]
        };

        inventoryService
            .Setup(service => service.IsAvailableAsync(
                orderItem.ProductId,
                orderItem.Quantity,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var receipt = await checkoutService.CheckoutAsync(order);

        receipt.Should().NotBeNull();
        receipt.Subtotal.Should().Be(0m);
        receipt.Discount.Should().Be(0m);
        receipt.Total.Should().Be(0m);
    }

    public static TheoryData<decimal, decimal> PriceDiscountRows => new()
    {
        { 999.99m,   0m },
        { 1000m,   100m },
        { 1500m,   100m }
    };

    [Theory]
    [MemberData(nameof(PriceDiscountRows))]
    public async Task CheckoutAsync_IsDiscountCapped_DiscountCapped(decimal price, decimal expected)
    {
        // Arrange
        var orderRepository = new Mock<IOrderRepository>();
        var inventoryService = new Mock<IInventoryService>();
        var notificationService = new Mock<INotificationService>();

        var checkoutService = new CheckoutService(
            orderRepository.Object,
            inventoryService.Object,
            notificationService.Object);

        var orderItem = new OrderItem
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Product A",
            UnitPrice = price,
            Quantity = 1
        };
        var order = new Order
        {
            CustomerName = "Kareem",
            CustomerEmail = "kareem@gmail.com",
            Items = [orderItem]
        };

        inventoryService
            .Setup(service => service.IsAvailableAsync(
                orderItem.ProductId,
                orderItem.Quantity,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var receipt = await checkoutService.CheckoutAsync(order);


        receipt.Should().NotBeNull();
        receipt.Discount.Should().Be(expected);
    }

    public static TheoryData<List<OrderItem>, decimal, decimal, decimal> ExampleOrderRows => new()
    {
        {
        new List<OrderItem>
        {
            new OrderItem
            {
                ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ProductName = "Product A",
                UnitPrice = 150m,
                Quantity = 2
            },
            new OrderItem
            {
                ProductId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                ProductName = "Product B",
                UnitPrice = 400m,
                Quantity = 1
            }
        },
        700m, 0m, 700m},
        {new List<OrderItem>
        {
            new OrderItem
            {
                ProductId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                ProductName = "Product C",
                UnitPrice = 500m,
                Quantity = 3
            }
        },
        1500m, 100m, 1400m}
    };

    [Theory]
    [MemberData(nameof(ExampleOrderRows))]
    public async Task CheckoutAsync_WithExampleOrders_ReturnsCorrectSubtotalDiscountAndTotal(List<OrderItem> orderItem, decimal subtotal, decimal discount, decimal total)
    {
        var orderRepository = new Mock<IOrderRepository>();
        var inventoryService = new Mock<IInventoryService>();
        var notificationService = new Mock<INotificationService>();

        var checkoutService = new CheckoutService(
            orderRepository.Object,
            inventoryService.Object,
            notificationService.Object);

        var order = new Order
        {
            CustomerName = "Kareem",
            CustomerEmail = "kareem@gmail.com",
            Items = orderItem
        };

        foreach (var item in orderItem)
        {
            inventoryService.Setup(service => service.IsAvailableAsync(item.ProductId
            , item.Quantity, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        }

        var receipt = await checkoutService.CheckoutAsync(order);

        receipt.Should().NotBeNull();
        receipt.Subtotal.Should().Be(subtotal);
        receipt.Discount.Should().Be(discount);
        receipt.Total.Should().Be(total);
    }

    [Fact]
    public async Task CheckoutAsync_ValidOrderItemsInStock_CheckoutSucceeds()
    {
        var orderRepository = new Mock<IOrderRepository>();
        var inventoryService = new Mock<IInventoryService>();
        var notificationService = new Mock<INotificationService>();

        var checkoutService = new CheckoutService(
            orderRepository.Object,
            inventoryService.Object,
            notificationService.Object);

        var orderItem = new OrderItem
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Product A",
            UnitPrice = 100m,
            Quantity = 2
        };
        var order = new Order
        {
            CustomerName = "Kareem",
            CustomerEmail = "kareem@gmail.com",
            Items = [orderItem]
        };

        inventoryService.Setup(service => service.IsAvailableAsync(orderItem.ProductId,
        orderItem.Quantity, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var receipt = await checkoutService.CheckoutAsync(order);


        orderRepository.Verify(x => x.SaveAsync(order, It.IsAny<CancellationToken>()), Times.Once());
        inventoryService.Verify(x => x.IsAvailableAsync(orderItem.ProductId, orderItem.Quantity
        , It.IsAny<CancellationToken>()), Times.Once());

        notificationService.Verify(x => x.SendOrderConfirmationAsync(receipt, It.IsAny<CancellationToken>()), Times.Once());

        receipt.Should().NotBeNull();
        receipt.Total.Should().Be(200m);
        receipt.Subtotal.Should().Be(200m);
        receipt.Discount.Should().Be(0m);
    }

    [Fact]
    public async Task CheckoutAsync_OneItemOutOfStock_ThrowsWithoutSavingOrSendingConfirmation()
    {
        // Arrange
        var orderRepository = new Mock<IOrderRepository>();
        var inventoryService = new Mock<IInventoryService>();
        var notificationService = new Mock<INotificationService>();

        var checkoutService = new CheckoutService(
            orderRepository.Object,
            inventoryService.Object,
            notificationService.Object);

        var orderItem = new OrderItem
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Product A",
            UnitPrice = 100m,
            Quantity = 1
        };
        var order = new Order
        {
            CustomerName = "Kareem",
            CustomerEmail = "kareem@gmail.com",
            Items = [orderItem]
        };

        inventoryService.Setup(service => service.IsAvailableAsync(orderItem.ProductId,
        orderItem.Quantity, It.IsAny<CancellationToken>())).ReturnsAsync(false);


        Func<Task> action = () => checkoutService.CheckoutAsync(order);


        await action.Should().ThrowAsync<InsufficientInventoryException>()
            .WithMessage($"Insufficient inventory for '{orderItem.ProductName}'.");

        inventoryService.Verify(service => service.IsAvailableAsync(
            orderItem.ProductId,
            orderItem.Quantity,
            It.IsAny<CancellationToken>()), Times.Once());

        orderRepository.Verify(repository => repository.SaveAsync(
            It.IsAny<Order>(),
            It.IsAny<CancellationToken>()), Times.Never());

        notificationService.Verify(service => service.SendOrderConfirmationAsync(
            It.IsAny<CheckoutReceipt>(),
            It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task CheckoutAsync_OrderFailsValidation_CheckoutFails()
    {
        var orderRepository = new Mock<IOrderRepository>();
        var inventoryService = new Mock<IInventoryService>();
        var notificationService = new Mock<INotificationService>();

        var checkoutService = new CheckoutService(
            orderRepository.Object,
            inventoryService.Object,
            notificationService.Object);

        var orderItem = new OrderItem
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Product A",
            UnitPrice = 100m,
            Quantity = 0
        };
        var order = new Order
        {
            CustomerName = "Kareem",
            CustomerEmail = "kareem@gmail.com",
            Items = [orderItem]
        };
        Func<Task> action = () => checkoutService.CheckoutAsync(order);

        await action.Should().ThrowAsync<InvalidQuantityException>()
        .WithMessage($"Quantity for '{orderItem.ProductName}' must be greater than zero.");

        inventoryService.Verify(x => x.IsAvailableAsync(orderItem.ProductId, orderItem.Quantity
        , It.IsAny<CancellationToken>()), Times.Never());
        orderRepository.Verify(x => x.SaveAsync(order, It.IsAny<CancellationToken>()), Times.Never());
        notificationService.Verify(x => x.SendOrderConfirmationAsync(It.IsAny<CheckoutReceipt>(), It.IsAny<CancellationToken>()), Times.Never());

    }

    [Fact]
    public async Task CheckoutAsync_ValidOrder_CheckoutSucceeds()
    {
        var orderTestData = new OrderTestData();
        var createOrder = orderTestData.CreateOrder();

        var orderRepository = new Mock<IOrderRepository>();
        var inventoryService = new Mock<IInventoryService>();
        var notificationService = new Mock<INotificationService>();
        var checkoutService = new CheckoutService(
            orderRepository.Object,
            inventoryService.Object,
            notificationService.Object);

        foreach (var item in createOrder.Items)
        {
            inventoryService
                .Setup(service => service.IsAvailableAsync(
                    item.ProductId,
                    item.Quantity,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        var receipt = await checkoutService.CheckoutAsync(createOrder);

        receipt.Should().NotBeNull();
        receipt.Total.Should().Be(100m);
        receipt.Subtotal.Should().Be(100m);
        receipt.Discount.Should().Be(0m);
    }

    [Fact]
    public async Task CheckoutAsync_WithSeededCustomer_ReturnsSameNameAndEmail()
    {
        var orderRepository = new Mock<IOrderRepository>();
        var inventoryService = new Mock<IInventoryService>();
        var notificationService = new Mock<INotificationService>();
        var checkoutService = new CheckoutService(
            orderRepository.Object,
            inventoryService.Object,
            notificationService.Object);
        var orderItem = new OrderItem
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Product A",
            UnitPrice = 100m,
            Quantity = 1
        };
        var order = CustomerTestData.CreateOrderFaker()
            .RuleFor(o => o.Items, _ => new List<OrderItem> { orderItem })
            .Generate();

        foreach (var item in order.Items)
        {
            inventoryService
                .Setup(service => service.IsAvailableAsync(
                    item.ProductId,
                    item.Quantity,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        var receipt = await checkoutService.CheckoutAsync(order);

        receipt.Should().NotBeNull();
        receipt.CustomerName.Should().Be(order.CustomerName);
        receipt.CustomerEmail.Should().Be(order.CustomerEmail);
    }

}
