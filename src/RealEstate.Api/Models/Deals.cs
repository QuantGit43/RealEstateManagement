using System.ComponentModel.DataAnnotations;

namespace RealEstate.Api.Models;

public sealed record DealRequest(
    [Required] PropertyRequest Property,
    [Required] ListingRequest Listing,
    [Required] ContractRequest Contract,
    [Required] PaymentRequest Payment);

public sealed record DealResult(int PropertyId, int ListingId, int ContractId, int PaymentId);
