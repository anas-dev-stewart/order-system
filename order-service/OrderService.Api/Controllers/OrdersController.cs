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

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(OrderCreateRequest request)
    {
        try
        {
            var created = await _orderService.PlaceOrderAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ProductNotFoundException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (InsufficientStockException ex)
        {
            return Conflict(new ErrorResponse(ex.Message));
        }
        catch (ProductServiceUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse(ex.Message));
        }
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
