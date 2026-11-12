using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;

namespace RealEstate.Benchmarks;

public sealed class SqlStore(string connectionString)
{
    private async Task<SqlConnection> OpenAsync()
    {
        var c = new SqlConnection(connectionString);
        await c.OpenAsync();
        return c;
    }

    public async Task LoadDimensionsAsync(DataGenerator gen)
    {
        await using (var c = await OpenAsync())
            await c.ExecuteAsync("INSERT dbo.Roles (Name) VALUES (N'Admin'), (N'Manager'), (N'Agent');");

        var users = new List<object?[]> { new object?[] { 1, 1, "admin@bench.example", "Адміністратор", null },
                                          new object?[] { 2, 2, "manager@bench.example", "Менеджер", null } };
        for (var a = 1; a <= DataGenerator.AgentCount; a++)
            users.Add([a + 2, 3, $"agent{a}@bench.example", DataGenerator.AgentName(a), DataGenerator.AgentPhone(a)]);
        await BulkAsync("dbo.Users", [("UserId", typeof(int)), ("RoleId", typeof(int)), ("Email", typeof(string)), ("FullName", typeof(string)), ("Phone", typeof(string))], users);

        await BulkAsync("dbo.Agents",
            [("AgentId", typeof(int)), ("UserId", typeof(int)), ("LicenseNo", typeof(string)), ("CommissionRate", typeof(decimal)), ("HireDate", typeof(DateTime))],
            Enumerable.Range(1, DataGenerator.AgentCount).Select(a => new object?[] { a, a + 2, $"LIC-B-{a:D5}", 0.03m, new DateTime(2022, 1, 1) }));

        await BulkAsync("dbo.Owners", [("OwnerId", typeof(int)), ("FullName", typeof(string)), ("Phone", typeof(string))],
            Enumerable.Range(1, gen.OwnerCount).Select(o => new object?[] { o, DataGenerator.OwnerName(o), DataGenerator.OwnerPhone(o) }));

        await BulkAsync("dbo.Cities", [("CityId", typeof(int)), ("Name", typeof(string)), ("Region", typeof(string))],
            DataGenerator.Cities.Select((c, i) => new object?[] { i + 1, c.Name, c.Region }));

        await BulkAsync("dbo.Districts", [("DistrictId", typeof(int)), ("CityId", typeof(int)), ("Name", typeof(string))],
            from city in Enumerable.Range(1, DataGenerator.CityCount)
            from k in Enumerable.Range(0, DataGenerator.DistrictsPerCity)
            select new object?[] { DataGenerator.DistrictId(city, k), city, DataGenerator.Districts[k] });

        await BulkAsync("dbo.PropertyTypes", [("PropertyTypeId", typeof(int)), ("Name", typeof(string))],
            DataGenerator.Types.Select((t, i) => new object?[] { i + 1, t }));

        await BulkAsync("dbo.Amenities", [("AmenityId", typeof(int)), ("Name", typeof(string))],
            DataGenerator.Amenities.Select((a, i) => new object?[] { i + 1, a }));
    }

    public async Task LoadCardsAsync(IReadOnlyList<ListingCard> cards)
    {
        await BulkAsync("dbo.Addresses",
            [("AddressId", typeof(int)), ("DistrictId", typeof(int)), ("Street", typeof(string)), ("Building", typeof(string)), ("Apartment", typeof(string))],
            cards.Select(x => new object?[] { x.Id, x.Address.DistrictId, x.Address.Street, x.Address.Building, x.Address.Apartment }));

        await BulkAsync("dbo.Properties",
            [("PropertyId", typeof(int)), ("OwnerId", typeof(int)), ("PropertyTypeId", typeof(int)), ("AddressId", typeof(int)), ("Title", typeof(string)),
             ("Description", typeof(string)), ("AreaSqm", typeof(decimal)), ("Rooms", typeof(byte)), ("Floor", typeof(short)), ("YearBuilt", typeof(short)), ("CreatedBy", typeof(int))],
            cards.Select(x => new object?[] { x.Id, x.Owner.Id, x.TypeId, x.Id, x.Title, x.Description, x.AreaSqm, (byte)x.Rooms,
                                              (short?)x.Floor, (short?)x.YearBuilt, 2 }));

        await BulkAsync("dbo.Listings",
            [("ListingId", typeof(int)), ("PropertyId", typeof(int)), ("AgentId", typeof(int)), ("ListingType", typeof(string)), ("Price", typeof(decimal)),
             ("Currency", typeof(string)), ("Status", typeof(string)), ("ListedAt", typeof(DateTime)), ("CreatedBy", typeof(int))],
            cards.Select(x => new object?[] { x.Id, x.Id, x.Agent.Id, x.ListingType, x.Price, x.Currency, x.Status, x.ListedAt, 2 }));

        await BulkAsync("dbo.PropertyAmenities", [("PropertyId", typeof(int)), ("AmenityId", typeof(int))],
            cards.SelectMany(x => x.Amenities.Select(a => new object?[] { x.Id, AmenityId(a) })));

        await BulkAsync("dbo.PropertyPhotos", [("PropertyId", typeof(int)), ("Url", typeof(string)), ("IsMain", typeof(bool)), ("SortOrder", typeof(int))],
            cards.SelectMany(x => x.Photos.Select((p, i) => new object?[] { x.Id, p.Url, p.IsMain, i })), keepIdentity: false);

        await BulkAsync("dbo.PriceHistory",
            [("ListingId", typeof(int)), ("OldPrice", typeof(decimal)), ("NewPrice", typeof(decimal)), ("ChangedAt", typeof(DateTime)), ("ChangedBy", typeof(int))],
            cards.SelectMany(x => x.PriceHistory.Select(h => new object?[] { x.Id, h.OldPrice, h.NewPrice, h.ChangedAt, 2 })), keepIdentity: false);

        await BulkAsync("dbo.PropertyAttributes",
            [("PropertyId", typeof(int)), ("AttrKey", typeof(string)), ("NumValue", typeof(decimal)), ("TextValue", typeof(string))],
            cards.SelectMany(x => x.Attributes.Select(kv => AttributeRow(x.Id, kv.Key, kv.Value))));
    }

    public async Task FinishLoadAsync()
    {
        await using var c = await OpenAsync();
        const string sql = """
            DECLARE @t sysname, @s nvarchar(max) = N'';
            SELECT @s += N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N' WITH CHECK CHECK CONSTRAINT ALL;'
            FROM sys.tables;
            EXEC (@s);
            EXEC sp_updatestats;
            CHECKPOINT;
            """;
        await c.ExecuteAsync(sql, commandTimeout: 0);
    }

    private async Task BulkAsync(string table, (string Name, Type Type)[] columns, IEnumerable<object?[]> rows, bool keepIdentity = true)
    {
        using var dt = new DataTable();
        foreach (var (name, type) in columns) dt.Columns.Add(name, type);
        foreach (var row in rows) dt.Rows.Add(row.Select(v => v ?? DBNull.Value).ToArray());

        var options = SqlBulkCopyOptions.TableLock | (keepIdentity ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default);
        using var bulk = new SqlBulkCopy(connectionString, options) { DestinationTableName = table, BulkCopyTimeout = 0 };
        foreach (var (name, _) in columns) bulk.ColumnMappings.Add(name, name);
        await bulk.WriteToServerAsync(dt);
    }

    private static int AmenityId(string name) => Array.IndexOf(DataGenerator.Amenities, name) + 1;

    private static object?[] AttributeRow(int propertyId, string key, object value) => value switch
    {
        bool b => [propertyId, key, b ? 1m : 0m, null],
        double d => [propertyId, key, (decimal)d, null],
        _ => [propertyId, key, null, value.ToString()]
    };

    private const string SummarySelect = """
        SELECT TOP (@Take) l.ListingId, p.Title, l.Price, c.Name AS City, d.Name AS District, p.AreaSqm, p.Rooms, ph.Url AS MainPhoto
        FROM dbo.Listings l
        JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
        JOIN dbo.Addresses a  ON a.AddressId = p.AddressId
        JOIN dbo.Districts d  ON d.DistrictId = a.DistrictId
        JOIN dbo.Cities c     ON c.CityId = d.CityId
        OUTER APPLY (SELECT TOP (1) x.Url FROM dbo.PropertyPhotos x WHERE x.PropertyId = p.PropertyId AND x.IsMain = 1) ph
        WHERE l.IsDeleted = 0 AND p.IsDeleted = 0 AND l.Status = N'Active'
        """;

    public async Task<ListingCard?> GetCardAsync(int id)
    {
        const string sql = """
            DECLARE @pid int = (SELECT PropertyId FROM dbo.Listings WHERE ListingId = @id);
            SELECT * FROM dbo.vw_ListingCards WHERE ListingId = @id;
            SELECT a.Name FROM dbo.PropertyAmenities pa JOIN dbo.Amenities a ON a.AmenityId = pa.AmenityId WHERE pa.PropertyId = @pid ORDER BY a.AmenityId;
            SELECT Url, IsMain FROM dbo.PropertyPhotos WHERE PropertyId = @pid ORDER BY SortOrder;
            SELECT OldPrice, NewPrice, ChangedAt FROM dbo.PriceHistory WHERE ListingId = @id ORDER BY ChangedAt;
            SELECT AttrKey, NumValue, TextValue FROM dbo.PropertyAttributes WHERE PropertyId = @pid;
            """;
        await using var c = await OpenAsync();
        using var grid = await c.QueryMultipleAsync(sql, new { id });
        var h = await grid.ReadSingleOrDefaultAsync<CardRow>();
        if (h is null) return null;

        var card = h.ToCard();
        card.Amenities = (await grid.ReadAsync<string>()).ToList();
        card.Photos = (await grid.ReadAsync<CardPhoto>()).ToList();
        card.PriceHistory = (await grid.ReadAsync<CardPriceChange>()).ToList();
        card.Attributes = (await grid.ReadAsync<(string Key, decimal? Num, string? Text)>())
            .ToDictionary(a => a.Key, a => (object)(a.Text ?? (object)(double)a.Num!.Value));
        return card;
    }

    public Task<long> SearchAsync(Args a) => SummaryAsync(SummarySelect + """
         AND d.CityId = @CityId AND p.PropertyTypeId = @TypeId AND l.ListingType = @ListingType
         AND l.Price BETWEEN @MinPrice AND @MaxPrice
        ORDER BY l.Price, l.ListingId
        """, new { Take = 20, a.CityId, a.TypeId, a.ListingType, a.MinPrice, a.MaxPrice });

    public Task<long> DeepPageAsync(Args a) => SummaryAsync(SummarySelect.Replace("TOP (@Take) ", "") + """
         AND d.CityId = @CityId AND p.PropertyTypeId = @TypeId AND l.ListingType = @ListingType
        ORDER BY l.Price, l.ListingId
        OFFSET @Skip ROWS FETCH NEXT 20 ROWS ONLY
        """, new { a.CityId, a.TypeId, a.ListingType, Skip = Args.DeepPageOffset });

    public Task<long> AmenitiesAsync(Args a) => SummaryAsync(SummarySelect + """
         AND d.CityId = @CityId
         AND EXISTS (SELECT 1 FROM dbo.PropertyAmenities x WHERE x.PropertyId = p.PropertyId AND x.AmenityId = @A1)
         AND EXISTS (SELECT 1 FROM dbo.PropertyAmenities x WHERE x.PropertyId = p.PropertyId AND x.AmenityId = @A2)
        ORDER BY l.Price, l.ListingId
        """, new { Take = 20, a.CityId, A1 = AmenityId(a.Amenity1), A2 = AmenityId(a.Amenity2) });

    public Task<long> AttributesAsync(Args a) => SummaryAsync(SummarySelect + """
         AND p.PropertyTypeId = 2
         AND EXISTS (SELECT 1 FROM dbo.PropertyAttributes x WHERE x.PropertyId = p.PropertyId AND x.AttrKey = 'landAreaSotky' AND x.NumValue >= @MinLand)
         AND EXISTS (SELECT 1 FROM dbo.PropertyAttributes x WHERE x.PropertyId = p.PropertyId AND x.AttrKey = 'hasGarage' AND x.NumValue = 1)
        ORDER BY l.Price, l.ListingId
        """, new { Take = 20, MinLand = (decimal)a.MinLand });

    public async Task<long> AggregateAsync(Args a)
    {
        const string sql = """
            SELECT c.CityId, c.Name AS City, COUNT(*) AS Cnt, AVG(l.Price) AS AvgPrice
            FROM dbo.Listings l
            JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
            JOIN dbo.Addresses a  ON a.AddressId = p.AddressId
            JOIN dbo.Districts d  ON d.DistrictId = a.DistrictId
            JOIN dbo.Cities c     ON c.CityId = d.CityId
            WHERE l.IsDeleted = 0 AND p.IsDeleted = 0 AND l.Status = N'Active'
              AND l.ListingType = @ListingType AND p.PropertyTypeId = @TypeId
            GROUP BY c.CityId, c.Name
            """;
        await using var c = await OpenAsync();
        var rows = await c.QueryAsync<(int CityId, string City, int Cnt, decimal AvgPrice)>(sql, new { a.ListingType, a.TypeId });
        return rows.Sum(r => (long)r.CityId * r.Cnt);
    }

    public async Task<long> InsertCardAsync(ListingCard x)
    {
        await using var c = await OpenAsync();
        return await c.ExecuteScalarAsync<int>("dbo.sp_ListingCard_Create", new
        {
            x.Address.DistrictId, x.Address.Street, x.Address.Building, x.Address.Apartment,
            OwnerId = x.Owner.Id, PropertyTypeId = x.TypeId, x.Title, x.Description, x.AreaSqm, Rooms = (byte)x.Rooms,
            Floor = (short?)x.Floor, YearBuilt = (short?)x.YearBuilt,
            AgentId = x.Agent.Id, x.ListingType, x.Price, x.Currency, x.Status, x.ListedAt,
            Photos = JsonSerializer.Serialize(x.Photos.Select((p, i) => new { p.Url, p.IsMain, SortOrder = i })),
            Amenities = JsonSerializer.Serialize(x.Amenities),
            PriceHistory = JsonSerializer.Serialize(x.PriceHistory),
            Attributes = JsonSerializer.Serialize(x.Attributes),
            UserId = 2
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task<long> UpdatePriceAsync(Args a)
    {
        await using var c = await OpenAsync();
        await c.ExecuteAsync("dbo.sp_Listing_UpdatePrice",
            new { ListingId = a.ActiveListingId, a.NewPrice, RowVersion = (byte[]?)null, UserId = 2 },
            commandType: CommandType.StoredProcedure);
        return a.ActiveListingId;
    }

    public async Task<long> RenameAgentAsync(Args a)
    {
        await using var c = await OpenAsync();
        return await c.ExecuteAsync("""
            UPDATE u SET FullName = @Name
            FROM dbo.Users u JOIN dbo.Agents ag ON ag.UserId = u.UserId
            WHERE ag.AgentId = @AgentId
            """, new { a.AgentId, Name = a.AgentNewName });
    }

    public async Task CreateSearchViewAsync(string scriptPath)
    {
        var script = await File.ReadAllTextAsync(scriptPath);
        await using var c = await OpenAsync();
        foreach (var batch in System.Text.RegularExpressions.Regex.Split(script, @"^\s*GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline))
            if (!string.IsNullOrWhiteSpace(batch))
                await c.ExecuteAsync(batch, commandTimeout: 0);
    }

    private const string ViewSummarySelect = """
        SELECT TOP (@Take) s.ListingId, p.Title, s.Price, c.Name AS City, d.Name AS District, p.AreaSqm, p.Rooms, ph.Url AS MainPhoto
        FROM dbo.vw_ListingSearch s WITH (NOEXPAND)
        JOIN dbo.Properties p ON p.PropertyId = s.PropertyId
        JOIN dbo.Addresses a  ON a.AddressId = p.AddressId
        JOIN dbo.Districts d  ON d.DistrictId = a.DistrictId
        JOIN dbo.Cities c     ON c.CityId = d.CityId
        OUTER APPLY (SELECT TOP (1) x.Url FROM dbo.PropertyPhotos x WHERE x.PropertyId = p.PropertyId AND x.IsMain = 1) ph
        WHERE s.Status = N'Active' AND s.CityId = @CityId AND s.PropertyTypeId = @TypeId AND s.ListingType = @ListingType
        """;

    public Task<long> SearchViewAsync(Args a) => SummaryAsync(ViewSummarySelect + """
         AND s.Price BETWEEN @MinPrice AND @MaxPrice
        ORDER BY s.Price, s.ListingId
        """, new { Take = 20, a.CityId, a.TypeId, a.ListingType, a.MinPrice, a.MaxPrice });

    public Task<long> DeepPageViewAsync(Args a) => SummaryAsync(ViewSummarySelect.Replace("TOP (@Take) ", "") + """
         ORDER BY s.Price, s.ListingId
        OFFSET @Skip ROWS FETCH NEXT 20 ROWS ONLY
        """, new { a.CityId, a.TypeId, a.ListingType, Skip = Args.DeepPageOffset });

    private async Task<long> SummaryAsync(string sql, object args)
    {
        await using var c = await OpenAsync();
        var ids = await c.QueryAsync<int>(sql, args);
        return Args.ListChecksum(ids);
    }

    public async Task<(long DataKb, long IndexKb)> SizeAsync()
    {
        const string sql = """
            SELECT SUM(CASE WHEN i.index_id <= 1 THEN a.used_pages ELSE 0 END) * 8 AS DataKb,
                   SUM(CASE WHEN i.index_id > 1  THEN a.used_pages ELSE 0 END) * 8 AS IndexKb
            FROM sys.indexes i
            JOIN sys.partitions p       ON p.object_id = i.object_id AND p.index_id = i.index_id
            JOIN sys.allocation_units a ON a.container_id = p.partition_id
            WHERE OBJECT_NAME(i.object_id) IN ('Listings','Properties','PropertyTypes','Addresses','Districts','Cities','Owners',
                  'Agents','Users','PropertyAmenities','Amenities','PropertyPhotos','PriceHistory','PropertyAttributes')
            """;
        await using var c = await OpenAsync();
        return await c.QuerySingleAsync<(long, long)>(sql);
    }

    private sealed class CardRow
    {
        public int ListingId { get; init; }
        public string Title { get; init; } = "";
        public string? Description { get; init; }
        public int PropertyTypeId { get; init; }
        public string PropertyType { get; init; } = "";
        public string ListingType { get; init; } = "";
        public decimal Price { get; init; }
        public string Currency { get; init; } = "";
        public string Status { get; init; } = "";
        public DateTime ListedAt { get; init; }
        public int CityId { get; init; }
        public string City { get; init; } = "";
        public int DistrictId { get; init; }
        public string District { get; init; } = "";
        public string Street { get; init; } = "";
        public string Building { get; init; } = "";
        public string? Apartment { get; init; }
        public decimal AreaSqm { get; init; }
        public byte Rooms { get; init; }
        public short? Floor { get; init; }
        public short? YearBuilt { get; init; }
        public int OwnerId { get; init; }
        public string OwnerName { get; init; } = "";
        public string OwnerPhone { get; init; } = "";
        public int AgentId { get; init; }
        public string AgentName { get; init; } = "";
        public string? AgentPhone { get; init; }
        public string LicenseNo { get; init; } = "";

        public ListingCard ToCard() => new()
        {
            Id = ListingId, Title = Title, Description = Description, TypeId = PropertyTypeId, Type = PropertyType,
            ListingType = ListingType, Price = Price, Currency = Currency, Status = Status, ListedAt = ListedAt,
            Address = new CardAddress { CityId = CityId, City = City, DistrictId = DistrictId, District = District, Street = Street, Building = Building, Apartment = Apartment },
            AreaSqm = AreaSqm, Rooms = Rooms, Floor = Floor, YearBuilt = YearBuilt,
            Owner = new CardPerson { Id = OwnerId, Name = OwnerName, Phone = OwnerPhone },
            Agent = new CardAgent { Id = AgentId, Name = AgentName, Phone = AgentPhone ?? "", LicenseNo = LicenseNo }
        };
    }
}
