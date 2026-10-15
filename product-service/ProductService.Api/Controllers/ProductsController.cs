using Microsoft.AspNetCore.Mvc;
using ProductService.Api.Dtos;
using ProductService.Api.Services;

namespace ProductService.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductResponse>>> GetAll()
    {
        return Ok(await _productService.GetAllAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductResponse>> GetById(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product is null)
        {
            return NotFound(new ErrorResponse($"Product with id {id} was not found."));
        }

        return Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(ProductCreateRequest request)
    {
        var created = await _productService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductResponse>> Update(int id, ProductUpdateRequest request)
    {
        try
        {
            return Ok(await _productService.UpdateAsync(id, request));
        }
        catch (ProductNotFoundException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _productService.DeleteAsync(id);
            return NoContent();
        }
        catch (ProductNotFoundException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
    }

    [HttpPost("{id:int}/reduce-stock")]
    public async Task<ActionResult<ProductResponse>> ReduceStock(int id, ReduceStockRequest request)
    {
        try
        {
            return Ok(await _productService.ReduceStockAsync(id, request.Quantity));
        }
        catch (ProductNotFoundException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (InsufficientStockException ex)
        {
            return Conflict(new ErrorResponse(ex.Message));
        }
    }
}
