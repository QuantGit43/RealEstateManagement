using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RealEstate.Api.Data;
using RealEstate.Api.Models;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/listings")]
public sealed class ListingsController(IUnitOfWork uow) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ListingView>> Get(int id) =>
        await uow.Listings.GetByIdAsync(id) is { } l ? l : NotFound();

    [HttpGet]
    public Task<IEnumerable<ListingView>> List([FromQuery] int? agentId) =>
        agentId is { } id ? uow.Listings.GetByAgentAsync(id) : uow.Listings.GetActiveAsync();

    [HttpPost("~/api/properties/{propertyId:int}/listings")]
    public async Task<ActionResult<ListingView>> Create(int propertyId, ListingRequest request,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        var id = await uow.Listings.CreateAsync(propertyId, request, userId);
        return CreatedAtAction(nameof(Get), new { id }, await uow.Listings.GetByIdAsync(id));
    }

    [HttpPut("{id:int}/price")]
    public async Task<IActionResult> UpdatePrice(int id, UpdatePriceRequest request,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        await uow.Listings.UpdatePriceAsync(id, request.NewPrice, request.RowVersion, userId);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        await uow.Listings.SoftDeleteAsync(id, userId);
        return NoContent();
    }
}
