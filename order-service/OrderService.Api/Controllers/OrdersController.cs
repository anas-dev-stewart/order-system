using Microsoft.AspNetCore.Mvc;
using OrderService.Api.Dtos;
using OrderService.Api.Services;

namespace OrderService.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Accepts an order and returns immediately with Status = Submitted.
    /// Stock validation and pricing happen asynchronously; poll
    /// GET /api/orders/{id} to see the order move to Approved or Rejected.
    ///
    /// Pass an "Idempotency-Key" header to make retries of the same logical
    /// request safe — resubmitting the same key returns the original order
    /// instead of creating a second one.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(
        OrderCreateRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        var created = await _orderService.PlaceOrderAsync(request, idempotencyKey);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderResponse>>> GetAll()
    {
        return Ok(await _orderService.GetAllAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponse>> GetById(int id)
    {
        var order = await _orderService.GetByIdAsync(id);
        if (order is null)
        {
            return NotFound(new ErrorResponse($"Order with id {id} was not found."));
        }

        return Ok(order);
    }
}
