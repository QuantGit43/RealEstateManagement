namespace RealEstate.Benchmarks;

public sealed class DataGenerator(int listings, int seed = 42)
{
    public const int AgentCount = 200;
    public const int DistrictsPerCity = 6;

    public static readonly (string Name, string Region)[] Cities =
    [
        ("Київ", "Київська"), ("Харків", "Харківська"), ("Одеса", "Одеська"), ("Дніпро", "Дніпропетровська"),
        ("Львів", "Львівська"), ("Запоріжжя", "Запорізька"), ("Вінниця", "Вінницька"), ("Полтава", "Полтавська"),
        ("Чернігів", "Чернігівська"), ("Черкаси", "Черкаська"), ("Житомир", "Житомирська"), ("Суми", "Сумська"),
        ("Хмельницький", "Хмельницька"), ("Чернівці", "Чернівецька"), ("Рівне", "Рівненська"), ("Івано-Франківськ", "Івано-Франківська"),
        ("Тернопіль", "Тернопільська"), ("Луцьк", "Волинська"), ("Ужгород", "Закарпатська"), ("Кропивницький", "Кіровоградська"),
        ("Миколаїв", "Миколаївська"), ("Херсон", "Херсонська"), ("Біла Церква", "Київська"), ("Кременчук", "Полтавська"),
        ("Кам'янець-Подільський", "Хмельницька")
    ];

    public static readonly string[] Districts = ["Центральний", "Північний", "Південний", "Східний", "Західний", "Приміський"];

    public static readonly string[] Types = ["Квартира", "Будинок", "Офіс", "Комерційне приміщення"];

    public static readonly string[] Amenities =
        ["Паркінг", "Ліфт", "Балкон", "Кондиціонер", "Охорона", "Меблі", "Інтернет", "Тераса", "Басейн", "Генератор"];

    private static readonly string[] Streets =
        ["вул. Шевченка", "вул. Франка", "просп. Миру", "вул. Грушевського", "вул. Січових Стрільців", "вул. Садова",
         "вул. Соборна", "просп. Незалежності", "вул. Лесі Українки", "вул. Набережна", "вул. Зелена", "вул. Шкільна",
         "вул. Козацька", "вул. Вишнева", "вул. Польова", "бульв. Дружби", "вул. Гетьмана Мазепи", "вул. Котляревського"];

    private static readonly string[] FirstNames = ["Андрій", "Олена", "Іван", "Марія", "Тарас", "Ірина", "Петро", "Наталія", "Сергій", "Оксана"];
    private static readonly string[] LastNames = ["Мельник", "Шевченко", "Бондар", "Коваль", "Ткаченко", "Кравець", "Лисенко", "Мороз", "Савченко", "Поліщук"];
    private static readonly string[] Heating = ["Центральне", "Індивідуальне", "Автономне"];

    private readonly Random _rng = new(seed);
    private static readonly DateTime Now = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    public int Listings => listings;
    public int OwnerCount => Math.Max(10, listings / 3);

    public static int CityCount => Cities.Length;
    public static int DistrictId(int cityId, int k) => (cityId - 1) * DistrictsPerCity + k + 1;

    public static string StatusOf(int id) => (id % 10) switch { 8 => "Reserved", 9 => "Closed", _ => "Active" };

    public static string AgentName(int agentId) => $"{FirstNames[agentId % 10]} {LastNames[agentId / 10 % 10]}";
    public static string AgentPhone(int agentId) => $"+38050{agentId:D7}";
    public static string OwnerName(int ownerId) => $"{FirstNames[ownerId % 10]} {LastNames[ownerId / 10 % 10]} ({ownerId})";
    public static string OwnerPhone(int ownerId) => $"+38067{ownerId:D7}";

    public IEnumerable<ListingCard> Cards(int from, int count)
    {
        for (var id = from; id < from + count; id++)
            yield return CreateCard(id);
    }

    public ListingCard CreateCard(int id)
    {
        var r = _rng;
        var cityId = (int)(CityCount * Math.Pow(r.NextDouble(), 2)) + 1;
        var k = r.Next(DistrictsPerCity);
        var typeId = Pick(r, [0.60, 0.20, 0.12, 0.08]) + 1;
        var listingType = r.NextDouble() < 0.6 ? "Sale" : "Rent";

        var (minArea, maxArea, maxRooms) = typeId switch
        {
            1 => (25, 150, 5), 2 => (60, 400, 8), 3 => (30, 500, 15), _ => (20, 800, 4)
        };
        var area = Math.Round((decimal)(minArea + r.NextDouble() * (maxArea - minArea)), 1);
        var rooms = r.Next(1, maxRooms + 1);
        var price = PricePerSqm(r, typeId, listingType, cityId) * area;
        price = Math.Round(price / 100m) * 100m;

        var amenities = Amenities.Where(_ => r.NextDouble() < 0.35).ToList();
        var photos = Enumerable.Range(0, r.Next(1, 6))
            .Select(n => new CardPhoto { Url = $"https://cdn.example/p/{id}/{n}.jpg", IsMain = n == 0 }).ToList();

        var listedAt = Now.AddDays(-r.Next(720)).AddMinutes(r.Next(1440));
        var history = new List<CardPriceChange>();
        var current = price;
        var changes = r.Next(4);
        for (var n = changes; n > 0; n--)
        {
            var old = Math.Round(current * (1.03m + (decimal)r.NextDouble() * 0.07m) / 100m) * 100m;
            history.Insert(0, new CardPriceChange { OldPrice = old, NewPrice = current, ChangedAt = listedAt.AddDays(n * 10) });
            current = old;
        }

        var ownerId = r.Next(1, OwnerCount + 1);
        var agentId = r.Next(1, AgentCount + 1);
        var district = Districts[k];

        return new ListingCard
        {
            Id = id,
            Title = $"{Types[typeId - 1]}, {rooms} кімн., {area} м², {district} р-н",
            Description = r.NextDouble() < 0.3 ? "Власник, без комісії. Терміновий продаж." : null,
            TypeId = typeId,
            Type = Types[typeId - 1],
            ListingType = listingType,
            Price = price,
            Status = StatusOf(id),
            ListedAt = listedAt,
            Address = new CardAddress
            {
                CityId = cityId, City = Cities[cityId - 1].Name,
                DistrictId = DistrictId(cityId, k), District = district,
                Street = Streets[r.Next(Streets.Length)], Building = r.Next(1, 200).ToString(),
                Apartment = typeId == 1 ? r.Next(1, 300).ToString() : null
            },
            AreaSqm = area,
            Rooms = rooms,
            Floor = typeId == 2 ? null : r.Next(1, typeId == 1 ? 26 : 31),
            YearBuilt = r.Next(1950, 2026),
            Owner = new CardPerson { Id = ownerId, Name = OwnerName(ownerId), Phone = OwnerPhone(ownerId) },
            Agent = new CardAgent { Id = agentId, Name = AgentName(agentId), Phone = AgentPhone(agentId), LicenseNo = $"LIC-B-{agentId:D5}" },
            Amenities = amenities,
            Photos = photos,
            PriceHistory = history,
            Attributes = Attributes(r, typeId)
        };
    }

    private static Dictionary<string, object> Attributes(Random r, int typeId) => typeId switch
    {
        1 => new() { ["ceilingHeight"] = Round(2.5 + r.NextDouble()), ["hasBalcony"] = r.NextDouble() < 0.6, ["heating"] = Heating[r.Next(3)] },
        2 => new() { ["landAreaSotky"] = (double)r.Next(2, 51), ["hasGarage"] = r.NextDouble() < 0.5, ["floorsCount"] = (double)r.Next(1, 4) },
        3 => new() { ["meetingRooms"] = (double)r.Next(0, 9), ["openSpace"] = r.NextDouble() < 0.5, ["parkingSpots"] = (double)r.Next(0, 51) },
        _ => new() { ["storefront"] = r.NextDouble() < 0.5, ["ceilingHeight"] = Round(2.7 + r.NextDouble() * 3.3), ["powerKw"] = (double)r.Next(5, 201) }
    };

    public static decimal PricePerSqm(Random r, int typeId, string listingType, int cityId)
    {
        var baseSale = typeId switch { 1 => 40_000m, 2 => 25_000m, 3 => 45_000m, _ => 50_000m };
        var cityFactor = 1.6m - cityId * 0.03m;
        var perSqm = baseSale * cityFactor * (0.6m + (decimal)r.NextDouble() * 0.8m);
        return listingType == "Sale" ? perSqm : perSqm / 200m;
    }

    private static int Pick(Random r, double[] weights)
    {
        var x = r.NextDouble();
        for (var i = 0; i < weights.Length; i++)
        {
            if (x < weights[i]) return i;
            x -= weights[i];
        }
        return weights.Length - 1;
    }

    private static double Round(double v) => Math.Round(v, 2);
}
