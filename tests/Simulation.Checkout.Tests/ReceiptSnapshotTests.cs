using Moq;
using Simulation.Checkout.Application;
using Simulation.Checkout.Application.Abstractions;
using Simulation.Checkout.Domain;
using Simulation.Checkout.Tests.TestData;

namespace Simulation.Checkout.Tests;

public class ReceiptSnapshotTests
{
    [Fact]
    public async Task CheckoutAsync_WithExampleC_ReturnsExpectedReceiptSnapshot()
    {
        //Arrange
        var orderRepository = new Mock<IOrderRepository>();
        var inventoryService = new Mock<IInventoryService>();
        var notificationService = new Mock<INotificationService>();
        var checkoutService = new CheckoutService(
            orderRepository.Object,
            inventoryService.Object,
            notificationService.Object);
        var orderItem = new OrderItem
        {
            ProductId = new Guid("d85b1407-351d-4694-9392-03acc5870eb1"),
            ProductName = "Product A",
            UnitPrice = 500m,
            Quantity = 3
        };
        var order = new Order
        {
            Id = new Guid("d85e1407-351d-3694-9399-03acc5870ea4"),
            CustomerName = "Kareem",
            CustomerEmail = "kareem@gmail.com",
            Items = [orderItem]
        };
        inventoryService.Setup(service => service.IsAvailableAsync
        (orderItem.ProductId, orderItem.Quantity, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        //Act
        var receipt = await checkoutService.CheckoutAsync(order);
        //Assert
        await Verifier.Verify(receipt);

    }
}