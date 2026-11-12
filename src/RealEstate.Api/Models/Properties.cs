using System.ComponentModel.DataAnnotations;

namespace RealEstate.Api.Models;

public sealed class PropertyView
{
    public int PropertyId { get; init; }
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public int PropertyTypeId { get; init; }
    public string PropertyType { get; init; } = "";
    public int OwnerId { get; init; }
    public string OwnerName { get; init; } = "";
    public int AddressId { get; init; }
    public int CityId { get; init; }
    public string City { get; init; } = "";
    public string District { get; init; } = "";
    public string Street { get; init; } = "";
    public string Building { get; init; } = "";
    public string? Apartment { get; init; }
    public decimal AreaSqm { get; init; }
    public byte Rooms { get; init; }
    public short? Floor { get; init; }
    public short? YearBuilt { get; init; }
    public DateTime CreatedAt { get; init; }
    public int CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int? UpdatedBy { get; init; }
}

public sealed record PropertyRequest(
    int OwnerId,
    int PropertyTypeId,
    int AddressId,
    [Required, StringLength(200)] string Title,
    string? Description,
    [Range(0.01, 100000)] decimal AreaSqm,
    byte Rooms,
    short? Floor,
    short? YearBuilt);
