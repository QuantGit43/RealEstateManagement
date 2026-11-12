using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace RealEstate.Benchmarks;

public sealed class MongoStore
{
    private readonly IMongoDatabase _db;
    private readonly IMongoCollection<ListingCard> _cards;
    private readonly IMongoCollection<ListingCard> _cardsJournaled;

    static MongoStore()
    {
        BsonSerializer.RegisterSerializer(new DecimalSerializer(BsonType.Decimal128));
        ConventionRegistry.Register("camel",
            new ConventionPack { new CamelCaseElementNameConvention(), new IgnoreExtraElementsConvention(true) }, _ => true);
    }

    public MongoStore(string connectionString, string database = "realestate")
    {
        _db = new MongoClient(connectionString).GetDatabase(database);
        _cards = _db.GetCollection<ListingCard>("listingCards");
        _cardsJournaled = _cards.WithWriteConcern(new WriteConcern(1, journal: true));
    }

    public IMongoCollection<ListingCard> Cards => _cards;

    public Task ResetAsync() => _db.DropCollectionAsync("listingCards");

    public Task LoadCardsAsync(IReadOnlyList<ListingCard> cards) =>
        _cards.InsertManyAsync(cards, new InsertManyOptions { IsOrdered = false });

    public Task CreateIndexesAsync()
    {
        var k = Builders<ListingCard>.IndexKeys;
        return _cards.Indexes.CreateManyAsync(
        [
            new(k.Ascending("address.cityId").Ascending(x => x.TypeId).Ascending(x => x.ListingType).Ascending(x => x.Status)
                 .Ascending(x => x.Price).Ascending(x => x.Id), new CreateIndexOptions { Name = "ix_search" }),
            new(k.Ascending(x => x.Amenities).Ascending("address.cityId").Ascending(x => x.Status)
                 .Ascending(x => x.Price).Ascending(x => x.Id), new CreateIndexOptions { Name = "ix_amenities" }),
            new(k.Wildcard("attributes"), new CreateIndexOptions { Name = "ix_attributes" }),
            new(k.Ascending(x => x.TypeId).Ascending(x => x.Status).Ascending(x => x.Price).Ascending(x => x.Id),
                new CreateIndexOptions { Name = "ix_type_price" }),
            new(k.Ascending(x => x.ListingType).Ascending(x => x.Status).Ascending(x => x.TypeId).Ascending("address.cityId")
                 .Ascending("address.city").Ascending(x => x.Price), new CreateIndexOptions { Name = "ix_report" }),
            new(k.Ascending(x => x.Agent.Id), new CreateIndexOptions { Name = "ix_agent" })
        ]);
    }

    private static readonly FilterDefinitionBuilder<ListingCard> F = Builders<ListingCard>.Filter;
    private static readonly SortDefinition<ListingCard> ByPrice = Builders<ListingCard>.Sort.Ascending(x => x.Price).Ascending(x => x.Id);
    private static readonly ProjectionDefinition<ListingCard> Summary = Builders<ListingCard>.Projection
        .Include(x => x.Title).Include(x => x.Price).Include("address.city").Include("address.district")
        .Include(x => x.AreaSqm).Include(x => x.Rooms).Slice(x => x.Photos, 1);
    private static readonly FilterDefinition<ListingCard> Active = F.Eq(x => x.Status, "Active");

    public async Task<ListingCard?> GetCardAsync(int id) =>
        await _cards.Find(F.Eq(x => x.Id, id)).FirstOrDefaultAsync();

    public Task<long> SearchAsync(Args a) => SummaryAsync(
        F.Eq("address.cityId", a.CityId) & F.Eq(x => x.TypeId, a.TypeId) & F.Eq(x => x.ListingType, a.ListingType) & Active
        & F.Gte(x => x.Price, a.MinPrice) & F.Lte(x => x.Price, a.MaxPrice), skip: 0);

    public Task<long> DeepPageAsync(Args a) => SummaryAsync(
        F.Eq("address.cityId", a.CityId) & F.Eq(x => x.TypeId, a.TypeId) & F.Eq(x => x.ListingType, a.ListingType) & Active,
        skip: Args.DeepPageOffset);

    public Task<long> AmenitiesAsync(Args a) => SummaryAsync(
        F.Eq("address.cityId", a.CityId) & Active & F.All(x => x.Amenities, [a.Amenity1, a.Amenity2]), skip: 0);

    public Task<long> AttributesAsync(Args a) => SummaryAsync(
        F.Eq(x => x.TypeId, 2) & Active & F.Gte("attributes.landAreaSotky", (double)a.MinLand) & F.Eq("attributes.hasGarage", true),
        skip: 0);

    public async Task<long> AggregateAsync(Args a)
    {
        var rows = await _cards.Aggregate()
            .Match(F.Eq(x => x.ListingType, a.ListingType) & Active & F.Eq(x => x.TypeId, a.TypeId))
            .Group(new BsonDocument
            {
                { "_id", "$address.cityId" },
                { "city", new BsonDocument("$first", "$address.city") },
                { "cnt", new BsonDocument("$sum", 1) },
                { "avgPrice", new BsonDocument("$avg", "$price") }
            })
            .ToListAsync();
        return rows.Sum(r => (long)r["_id"].AsInt32 * r["cnt"].AsInt32);
    }

    public async Task<long> InsertCardAsync(ListingCard card)
    {
        await _cardsJournaled.InsertOneAsync(card);
        return card.Id;
    }

    public async Task<long> UpdatePriceAsync(Args a)
    {
        var update = new BsonDocument("$set", new BsonDocument
        {
            { "priceHistory", new BsonDocument("$concatArrays", new BsonArray
                {
                    "$priceHistory",
                    new BsonArray { new BsonDocument { { "oldPrice", "$price" }, { "newPrice", new Decimal128(a.NewPrice) }, { "changedAt", DateTime.UtcNow } } }
                }) },
            { "price", new Decimal128(a.NewPrice) }
        });
        await _cardsJournaled.UpdateOneAsync(F.Eq(x => x.Id, a.ActiveListingId) & Active,
            new PipelineUpdateDefinition<ListingCard>(new[] { update }));
        return a.ActiveListingId;
    }

    public async Task<long> RenameAgentAsync(Args a)
    {
        var r = await _cardsJournaled.UpdateManyAsync(F.Eq(x => x.Agent.Id, a.AgentId),
            Builders<ListingCard>.Update.Set(x => x.Agent.Name, a.AgentNewName));
        return r.ModifiedCount;
    }

    private async Task<long> SummaryAsync(FilterDefinition<ListingCard> filter, int skip)
    {
        var docs = await _cards.Find(filter).Sort(ByPrice).Skip(skip).Limit(20).Project<ListingCard>(Summary).ToListAsync();
        return Args.ListChecksum(docs.Select(d => d.Id));
    }

    public async Task<(long DataKb, long IndexKb)> SizeAsync()
    {
        await _db.Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("fsync", 1));
        var s = await _db.RunCommandAsync<BsonDocument>(new BsonDocument("collStats", "listingCards"));
        return (s["storageSize"].ToInt64() / 1024, s["totalIndexSize"].ToInt64() / 1024);
    }
}
