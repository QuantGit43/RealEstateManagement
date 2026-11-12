using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RealEstate.Api.Data;
using RealEstate.Api.Models;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/properties")]
public sealed class PropertiesController(IUnitOfWork uow) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PropertyView>> Get(int id) =>
        await uow.Properties.GetByIdAsync(id) is { } p ? p : NotFound();

    [HttpGet]
    public Task<IEnumerable<PropertyView>> GetByCity([FromQuery, BindRequired] int cityId) =>
        uow.Properties.GetByCityAsync(cityId);

    [HttpPost]
    public async Task<ActionResult<PropertyView>> Create(PropertyRequest request,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        var id = await uow.Properties.CreateAsync(request, userId);
        return CreatedAtAction(nameof(Get), new { id }, await uow.Properties.GetByIdAsync(id));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, PropertyRequest request,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        await uow.Properties.UpdateAsync(id, request, userId);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        await uow.Properties.SoftDeleteAsync(id, userId);
        return NoContent();
    }
}
