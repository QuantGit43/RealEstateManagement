using System.ComponentModel.DataAnnotations;

namespace RealEstate.Api.Models;

public sealed class ListingView
{
    public int ListingId { get; init; }
    public int PropertyId { get; init; }
    public string PropertyTitle { get; init; } = "";
    public string City { get; init; } = "";
    public string District { get; init; } = "";
    public int AgentId { get; init; }
    public string AgentName { get; init; } = "";
    public string ListingType { get; init; } = "";
    public decimal Price { get; init; }
    public string Currency { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTime ListedAt { get; init; }
    public byte[] RowVersion { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public int CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int? UpdatedBy { get; init; }
}

public sealed record ListingRequest(
    int AgentId,
    [Required, RegularExpression("Sale|Rent")] string ListingType,
    [Range(0.01, 1e12)] decimal Price,
    [StringLength(3, MinimumLength = 3)] string Currency = "UAH");

public sealed record UpdatePriceRequest([Range(0.01, 1e12)] decimal NewPrice, byte[]? RowVersion);
