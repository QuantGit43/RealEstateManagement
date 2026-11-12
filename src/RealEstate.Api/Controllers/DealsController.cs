using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RealEstate.Api.Data;
using RealEstate.Api.Models;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/deals")]
public sealed class DealsController(IUnitOfWork uow) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<DealResult>> Create(DealRequest request,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        uow.BeginTransaction();
        try
        {
            var propertyId = await uow.Properties.CreateAsync(request.Property, userId);
            var listingId = await uow.Listings.CreateAsync(propertyId, request.Listing, userId);
            var contractId = await uow.Contracts.CreateAsync(listingId, request.Contract, userId);
            var paymentId = await uow.Payments.RegisterAsync(contractId, request.Payment, userId);
            uow.Commit();

            return Created($"/api/contracts/{contractId}",
                new DealResult(propertyId, listingId, contractId, paymentId));
        }
        catch
        {
            uow.Rollback();
            throw;
        }
    }
}
