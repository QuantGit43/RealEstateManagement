using System.ComponentModel.DataAnnotations;

namespace RealEstate.Api.Models;

public sealed class PaymentView
{
    public int PaymentId { get; init; }
    public int ContractId { get; init; }
    public int ClientId { get; init; }
    public string ClientName { get; init; } = "";
    public decimal Amount { get; init; }
    public DateTime DueDate { get; init; }
    public DateTime? PaidAt { get; init; }
    public string Method { get; init; } = "";
    public bool IsPaid { get; init; }
    public DateTime CreatedAt { get; init; }
    public int CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int? UpdatedBy { get; init; }
}

public sealed class OverduePaymentView
{
    public int PaymentId { get; init; }
    public int ContractId { get; init; }
    public string ClientName { get; init; } = "";
    public string ClientPhone { get; init; } = "";
    public decimal Amount { get; init; }
    public DateTime DueDate { get; init; }
    public int DaysOverdue { get; init; }
}

public sealed record PaymentRequest(
    [Range(0.01, 1e12)] decimal Amount,
    DateTime DueDate,
    DateTime? PaidAt,
    [Required, RegularExpression("Cash|Card|Transfer")] string Method);
