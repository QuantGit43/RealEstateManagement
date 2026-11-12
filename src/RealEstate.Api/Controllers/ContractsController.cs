using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RealEstate.Api.Data;
using RealEstate.Api.Models;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/contracts")]
public sealed class ContractsController(IUnitOfWork uow) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ContractView>> Get(int id) =>
        await uow.Contracts.GetByIdAsync(id) is { } c ? c : NotFound();

    [HttpPost("~/api/listings/{listingId:int}/contract")]
    public async Task<ActionResult<ContractView>> Create(int listingId, ContractRequest request,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        var id = await uow.Contracts.CreateAsync(listingId, request, userId);
        return CreatedAtAction(nameof(Get), new { id }, await uow.Contracts.GetByIdAsync(id));
    }

    [HttpGet("{id:int}/payments")]
    public Task<IEnumerable<PaymentView>> Payments(int id) => uow.Payments.GetByContractAsync(id);

    [HttpPost("{id:int}/payments")]
    public async Task<IActionResult> RegisterPayment(int id, PaymentRequest request,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        var paymentId = await uow.Payments.RegisterAsync(id, request, userId);
        return Created($"/api/contracts/{id}/payments", new { paymentId });
    }

    [HttpPut("~/api/payments/{paymentId:int}/paid")]
    public async Task<IActionResult> MarkPaid(int paymentId, [FromQuery] DateTime? paidAt,
        [FromHeader(Name = "X-User-Id"), BindRequired] int userId)
    {
        await uow.Payments.MarkPaidAsync(paymentId, paidAt, userId);
        return NoContent();
    }

    [HttpGet("~/api/payments/overdue")]
    public Task<IEnumerable<OverduePaymentView>> Overdue() => uow.Payments.GetOverdueAsync();
}
