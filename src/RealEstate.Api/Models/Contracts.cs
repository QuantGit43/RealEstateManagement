using System.ComponentModel.DataAnnotations;

namespace RealEstate.Api.Models;

public sealed class ContractView
{
    public int ContractId { get; init; }
    public int ListingId { get; init; }
    public int PropertyId { get; init; }
    public string PropertyTitle { get; init; } = "";
    public int ClientId { get; init; }
    public string ClientName { get; init; } = "";
    public int AgentId { get; init; }
    public string AgentName { get; init; } = "";
    public string ContractType { get; init; } = "";
    public decimal TotalAmount { get; init; }
    public decimal CommissionAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal Debt { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string Status { get; init; } = "";
    public DateTime SignedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public int CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int? UpdatedBy { get; init; }
}

public sealed record ContractRequest(
    int ClientId,
    [Range(0.01, 1e12)] decimal TotalAmount,
    DateTime StartDate,
    DateTime? EndDate);
