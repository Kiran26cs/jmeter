using Microsoft.AspNetCore.Mvc;
using ProductApi.Models;
using ProductApi.Services;

namespace ProductApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController(ProductStore store) : ControllerBase
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;

    // GET /api/products?limit=20&after=<cursor>  -> { items, nextCursor }
    // Pass the returned nextCursor as `after` to get the next page; nextCursor is null on the last page.
    [HttpGet]
    public IActionResult GetAll([FromQuery] int? after, [FromQuery] int? limit)
    {
        if (after is < 0) return BadRequest("after must be >= 0.");
        if (limit is < 1) return BadRequest("limit must be >= 1.");
        var (items, next) = store.Page(after ?? 0, Math.Min(limit ?? DefaultLimit, MaxLimit));
        return Ok(new { items, nextCursor = next });
    }

    [HttpGet("{id:int}")]
    public IActionResult Get(int id) => store.Get(id) is { } p ? Ok(p) : NotFound();

    [HttpPost]
    public IActionResult Create(CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Price <= 0)
            return BadRequest("Name is required and Price must be > 0.");
        var p = store.Add(request);
        return CreatedAtAction(nameof(Get), new { id = p.Id }, p);
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id) => store.Delete(id) ? NoContent() : NotFound();

    // Simulates a slow downstream call (DB / third party) — useful to see latency percentiles diverge.
    [HttpGet("slow")]
    public async Task<IActionResult> Slow()
    {
        await Task.Delay(Random.Shared.Next(200, 800));
        return Ok(new { message = "slow response" });
    }
}
