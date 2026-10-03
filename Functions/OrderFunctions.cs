using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PageTurn.Functions.Helpers;
using PageTurn.Functions.Models;
using PageTurn.Functions.Models.Dtos;
using PageTurn.Functions.Services;
using System.Globalization;

namespace PageTurn.Functions.Functions;

/// <summary>Queue producer plus read endpoints used by Postman to verify the Orders table.</summary>
public class OrderFunctions
{
    private readonly ILogger<OrderFunctions> _logger;
    private readonly IOrderQueueService _queue;
    private readonly IOrderService _orders;

    public OrderFunctions(ILogger<OrderFunctions> logger, IOrderQueueService queue, IOrderService orders)
    {
        _logger = logger;
        _queue = queue;
        _orders = orders;
    }

    /// <summary>Non-blocking: validates, queues, and returns 202 Accepted immediately.</summary>
    [Function("PlaceOrderInQueue")]
    public async Task<IActionResult> PlaceOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders/queue")] HttpRequest req,
        CancellationToken ct)
    {
        try
        {
            var parsed = await RequestReader.ReadJsonAsync<PlaceOrderRequest>(req, ct);
            if (!parsed.IsValid)
                return ApiResults.BadRequest("Validation failed.", parsed.Errors);

            var message = parsed.Value!.ToMessage();
            await _queue.EnqueueAsync(message, ct);

            _logger.LogInformation("Order {OrderId} queued", message.OrderId);

            string date = Models.Entities.OrderEntity.ToPartitionKey(message.OrderTimestamp);
            return new ObjectResult(new OrderQueuedResponse(
                message.OrderId, "Queued",
                "Order accepted and waiting to be processed.",
                $"/api/orders/{date}/{message.OrderId}"))
            { StatusCode = StatusCodes.Status202Accepted };
        }
        catch (Exception ex) { return ApiResults.FromException(_logger, ex); }
    }

    [Function("GetOrderStatus")]
    public async Task<IActionResult> GetOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{orderDate}/{orderId}")] HttpRequest req,
        string orderDate,
        string orderId,
        CancellationToken ct)
    {
        try
        {
            if (!IsValidDate(orderDate))
                return ApiResults.BadRequest("orderDate must use the format yyyy-MM-dd.");

            var order = await _orders.GetAsync(orderDate, orderId, ct);
            return order is null
                ? ApiResults.NotFound($"Order '{orderId}' was not found for {orderDate}. It may still be waiting in the queue.")
                : new OkObjectResult(OrderResponse.FromEntity(order));
        }
        catch (Exception ex) { return ApiResults.FromException(_logger, ex); }
    }

    [Function("GetOrders")]
    public async Task<IActionResult> GetOrders(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders")] HttpRequest req,
        CancellationToken ct)
    {
        try
        {
            string? date = req.Query["date"];
            if (date is not null && !IsValidDate(date))
                return ApiResults.BadRequest("Query parameter 'date' must use the format yyyy-MM-dd.");

            var orders = await _orders.GetAllAsync(date, ct);
            return new OkObjectResult(orders.Select(OrderResponse.FromEntity));
        }
        catch (Exception ex) { return ApiResults.FromException(_logger, ex); }
    }

    private static bool IsValidDate(string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}