using MongoDB.Bson.Serialization.Attributes;

namespace RealEstate.Benchmarks;

public sealed class ListingCard
{
    [BsonId] public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int TypeId { get; set; }
    public string Type { get; set; } = "";
    public string ListingType { get; set; } = "";
    public decimal Price { get; set; }
    public string Currency { get; set; } = "UAH";
    public string Status { get; set; } = "";
    public DateTime ListedAt { get; set; }
    public CardAddress Address { get; set; } = new();
    public decimal AreaSqm { get; set; }
    public int Rooms { get; set; }
    public int? Floor { get; set; }
    public int? YearBuilt { get; set; }
    public CardPerson Owner { get; set; } = new();
    public CardAgent Agent { get; set; } = new();
    public List<string> Amenities { get; set; } = [];
    public List<CardPhoto> Photos { get; set; } = [];
    public List<CardPriceChange> PriceHistory { get; set; } = [];
    public Dictionary<string, object> Attributes { get; set; } = [];

    public long Checksum() =>
        Id + Amenities.Count * 10L + Photos.Count * 100L + PriceHistory.Count * 1_000L + Attributes.Count * 10_000L
        + (long)Price;
}

public sealed class CardAddress
{
    public int CityId { get; set; }
    public string City { get; set; } = "";
    public int DistrictId { get; set; }
    public string District { get; set; } = "";
    public string Street { get; set; } = "";
    public string Building { get; set; } = "";
    public string? Apartment { get; set; }
}

public sealed class CardPerson
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
}

public sealed class CardAgent
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string LicenseNo { get; set; } = "";
}

public sealed class CardPhoto
{
    public string Url { get; set; } = "";
    public bool IsMain { get; set; }
}

public sealed class CardPriceChange
{
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public DateTime ChangedAt { get; set; }
}
