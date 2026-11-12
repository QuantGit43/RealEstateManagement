using System.Diagnostics;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;

namespace RealEstate.Benchmarks;

public sealed record Args(
    int ListingId, int ActiveListingId, int CityId, int TypeId, string ListingType,
    decimal MinPrice, decimal MaxPrice, string Amenity1, string Amenity2, int MinLand,
    int AgentId, string AgentNewName, decimal NewPrice, int HotListingId)
{
    public const int DeepPageOffset = 500;

    public static Args Next(Random r, int listings)
    {
        var hotCount = Math.Max(1, listings / 100);
        var hot = r.NextDouble() < 0.9 ? (int)((long)r.Next(hotCount) * 7919 % listings) + 1 : r.Next(1, listings + 1);

        var cityId = (int)(DataGenerator.CityCount * Math.Pow(r.NextDouble(), 2)) + 1;
        var typeId = r.Next(1, 5);
        var listingType = r.NextDouble() < 0.6 ? "Sale" : "Rent";
        var typicalArea = typeId switch { 1 => 70m, 2 => 180m, 3 => 150m, _ => 200m };
        var center = DataGenerator.PricePerSqm(r, typeId, listingType, cityId) * typicalArea;
        var a1 = r.Next(DataGenerator.Amenities.Length);
        var a2 = (a1 + 1 + r.Next(DataGenerator.Amenities.Length - 1)) % DataGenerator.Amenities.Length;

        int active;
        do active = r.Next(1, listings + 1); while (DataGenerator.StatusOf(active) != "Active");

        return new Args(r.Next(1, listings + 1), active, cityId, typeId, listingType,
            Math.Round(center * 0.8m), Math.Round(center * 1.2m),
            DataGenerator.Amenities[a1], DataGenerator.Amenities[a2], r.Next(5, 41),
            r.Next(1, DataGenerator.AgentCount + 1), $"Агент {r.Next(1_000_000)}", r.Next(100_000, 5_000_000), hot);
    }

    public static long ListChecksum(IEnumerable<int> ids)
    {
        long sum = 0, pos = 1;
        foreach (var id in ids) sum += id * pos++;
        return sum;
    }
}

public sealed record Result(string Scenario, string Engine, string Mode, int Count, double MedianMs, double P95Ms, double MeanMs, double OpsPerSec);

public sealed class Bench(SqlStore sql, MongoStore mongo, IDatabase redis, int listings, string indexedViewScript)
{
    private const int Seed = 2026;
    private readonly List<Result> _results = [];
    private readonly List<string> _checks = [];

    public IReadOnlyList<Result> Results => _results;
    public IReadOnlyList<string> Checks => _checks;

    private sealed record Scenario(string Id, string Title, int Warmup, int Iterations,
        Func<Args, Task<long>> Sql, Func<Args, Task<long>> Mongo, bool Verify = true);

    public async Task RunAsync()
    {
        Scenario[] reads =
        [
            new("S1", "Картка за Id", 50, 500,
                async a => (await sql.GetCardAsync(a.ListingId))?.Checksum() ?? 0,
                async a => (await mongo.GetCardAsync(a.ListingId))?.Checksum() ?? 0),
            new("S2", "Пошук: місто+тип+ціна, топ-20", 50, 500, sql.SearchAsync, mongo.SearchAsync),
            new("S3", $"Глибока сторінка (skip {Args.DeepPageOffset})", 20, 200, sql.DeepPageAsync, mongo.DeepPageAsync),
            new("S4", "Пошук за 2 зручностями", 20, 200, sql.AmenitiesAsync, mongo.AmenitiesAsync),
            new("S5", "Фільтр за атрибутами (EAV / wildcard)", 20, 200, sql.AttributesAsync, mongo.AttributesAsync),
            new("S6", "Звіт: середня ціна по містах", 3, 30, sql.AggregateAsync, mongo.AggregateAsync)
        ];
        foreach (var s in reads) await RunScenarioAsync(s);

        await RunRedisCacheAsync();

        _results.Add(await ThroughputAsync("S1", "SQL Server", reads[0].Sql));
        _results.Add(await ThroughputAsync("S1", "MongoDB", reads[0].Mongo));
        await RedisThroughputAsync("S1", a => CacheAsideAsync(a.ListingId));
        _results.Add(await ThroughputAsync("S1hot", "SQL Server", async a => (await sql.GetCardAsync(a.HotListingId))?.Checksum() ?? 0));
        _results.Add(await ThroughputAsync("S1hot", "MongoDB", async a => (await mongo.GetCardAsync(a.HotListingId))?.Checksum() ?? 0));
        await RedisThroughputAsync("S1hot", a => CacheAsideAsync(a.HotListingId));
        _results.Add(await ThroughputAsync("S2", "SQL Server", reads[1].Sql));
        _results.Add(await ThroughputAsync("S2", "MongoDB", reads[1].Mongo));

        var sqlGen = new DataGenerator(listings, seed: 777);
        var mongoGen = new DataGenerator(listings, seed: 777);
        var sqlNext = listings + 1;
        var mongoNext = listings + 1;
        long renamedDocs = 0, renameCalls = 0;
        Scenario[] writes =
        [
            new("S7", "Вставка нової картки", 30, 300,
                _ => sql.InsertCardAsync(sqlGen.CreateCard(sqlNext++)),
                _ => mongo.InsertCardAsync(mongoGen.CreateCard(mongoNext++)), Verify: false),
            new("S8", "Зміна ціни + історія", 30, 300, sql.UpdatePriceAsync, mongo.UpdatePriceAsync, Verify: false),
            new("S9", "Перейменування агента", 2, 20, sql.RenameAgentAsync,
                async a => { var n = await mongo.RenameAgentAsync(a); renamedDocs += n; renameCalls++; return n; }, Verify: false)
        ];
        foreach (var s in writes) await RunScenarioAsync(s);
        _checks.Add($"S9: MongoDB оновлює в середньому {renamedDocs / Math.Max(1, renameCalls):N0} документів за виклик, SQL Server — 1 рядок Users");

        Console.WriteLine("-- Створення indexed view vw_ListingSearch");
        var started = Stopwatch.StartNew();
        await sql.CreateSearchViewAsync(indexedViewScript);
        _checks.Add($"Побудова indexed view: {started.Elapsed.TotalSeconds:F1} с");
        Scenario[] withView =
        [
            new("S2v", "Пошук: місто+тип+ціна, SQL через indexed view", 50, 500, sql.SearchViewAsync, mongo.SearchAsync),
            new("S3v", $"Глибока сторінка, SQL через indexed view", 20, 200, sql.DeepPageViewAsync, mongo.DeepPageAsync),
            writes[0] with { Id = "S7v", Title = "Вставка картки, SQL з indexed view" },
            writes[1] with { Id = "S8v", Title = "Зміна ціни, SQL з indexed view" }
        ];
        foreach (var s in withView) await RunScenarioAsync(s);
        _results.Add(await ThroughputAsync("S2v", "SQL Server (indexed view)", sql.SearchViewAsync));
        _results.Add(await ThroughputAsync("S2v", "MongoDB", mongo.SearchAsync));
    }

    private async Task RunScenarioAsync(Scenario s)
    {
        var name = $"{s.Id} {s.Title}";
        Console.WriteLine($"-- {name}");
        if (s.Verify) await VerifyAsync(s, name);
        _results.Add(await MeasureAsync(name, "SQL Server", s.Warmup, s.Iterations, s.Sql));
        _results.Add(await MeasureAsync(name, "MongoDB", s.Warmup, s.Iterations, s.Mongo));
    }

    private async Task VerifyAsync(Scenario s, string name)
    {
        var r = new Random(Seed + 1);
        int ok = 0, total = Math.Min(30, s.Iterations);
        for (var i = 0; i < total; i++)
        {
            var a = Args.Next(r, listings);
            if (await s.Sql(a) == await s.Mongo(a)) ok++;
        }
        _checks.Add($"{name}: {ok}/{total} збігів результатів SQL і MongoDB");
        Console.WriteLine($"   перевірка результатів: {ok}/{total}");
    }

    private async Task<Result> MeasureAsync(string scenario, string engine, int warmup, int iterations, Func<Args, Task<long>> op)
    {
        var r = new Random(Seed);
        for (var i = 0; i < warmup; i++) await op(Args.Next(r, listings));

        var times = new double[iterations];
        var total = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var a = Args.Next(r, listings);
            var start = Stopwatch.GetTimestamp();
            await op(a);
            times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        total.Stop();
        var result = Summarize(scenario, engine, "послідовно", times, iterations / total.Elapsed.TotalSeconds);
        Console.WriteLine($"   {engine,-22} median {result.MedianMs,8:F2} ms   p95 {result.P95Ms,8:F2} ms");
        return result;
    }

    private async Task<Result> ThroughputAsync(string scenario, string engine, Func<Args, Task<long>> op,
        int workers = 16, int seconds = 10)
    {
        var stopAt = Stopwatch.GetTimestamp() + seconds * Stopwatch.Frequency;
        var lists = await Task.WhenAll(Enumerable.Range(0, workers).Select(async w =>
        {
            var r = new Random(Seed + 100 + w);
            var list = new List<double>();
            while (Stopwatch.GetTimestamp() < stopAt)
            {
                var a = Args.Next(r, listings);
                var start = Stopwatch.GetTimestamp();
                await op(a);
                list.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            }
            return list;
        }));
        var times = lists.SelectMany(x => x).ToArray();
        var result = Summarize(scenario, engine, $"{workers} потоків", times, times.Length / (double)seconds);
        Console.WriteLine($"   {scenario} {engine,-22} {workers} потоків: {result.OpsPerSec,8:F0} оп/с, p95 {result.P95Ms:F2} ms");
        return result;
    }

    private static Result Summarize(string scenario, string engine, string mode, double[] times, double opsPerSec)
    {
        Array.Sort(times);
        return new Result(scenario, engine, mode, times.Length,
            times[times.Length / 2], times[(int)Math.Ceiling(times.Length * 0.95) - 1], times.Average(), opsPerSec);
    }

    private long _cacheHits, _cacheMisses;

    private async Task<long> CacheAsideAsync(int id)
    {
        var key = $"card:{id}";
        var cached = await redis.StringGetAsync(key);
        if (cached.HasValue)
        {
            Interlocked.Increment(ref _cacheHits);
            return JsonSerializer.Deserialize<ListingCard>((string)cached!)!.Checksum();
        }
        Interlocked.Increment(ref _cacheMisses);
        var card = await mongo.GetCardAsync(id);
        if (card is null) return 0;
        await redis.StringSetAsync(key, JsonSerializer.Serialize(card), TimeSpan.FromMinutes(10));
        return card.Checksum();
    }

    private async Task RunRedisCacheAsync()
    {
        const string name = "S1 Картка за Id";
        await redis.ExecuteAsync("FLUSHDB");
        var r = new Random(Seed);
        for (var i = 0; i < 550; i++) await CacheAsideAsync(Args.Next(r, listings).ListingId);
        _cacheHits = _cacheMisses = 0;
        _results.Add(await MeasureAsync(name, "Redis (кеш-хіт)", 0, 500, a => CacheAsideAsync(a.ListingId)));
        _checks.Add($"Redis, послідовно: {_cacheHits} попадань, {_cacheMisses} промахів");
    }

    private async Task RedisThroughputAsync(string scenario, Func<Args, Task<long>> op)
    {
        await redis.ExecuteAsync("FLUSHDB");
        _cacheHits = _cacheMisses = 0;
        _results.Add(await ThroughputAsync(scenario, "Redis (cache-aside)", op));
        var total = Math.Max(1, _cacheHits + _cacheMisses);
        _checks.Add($"Redis {scenario}, 16 потоків з порожнього кешу: {_cacheHits:N0} попадань / {_cacheMisses:N0} промахів ({100.0 * _cacheHits / total:F0}% hit)");
    }

    public string ToMarkdown(string header)
    {
        var sb = new StringBuilder(header);
        sb.AppendLine("\n## Перевірка еквівалентності результатів\n");
        foreach (var c in _checks) sb.AppendLine($"- {c}");

        sb.AppendLine("\n## Послідовні виклики (1 клієнт)\n");
        sb.AppendLine("| Сценарій | БД | N | Медіана, мс | p95, мс | Середнє, мс | оп/с |");
        sb.AppendLine("|---|---|---:|---:|---:|---:|---:|");
        foreach (var x in _results.Where(x => x.Mode == "послідовно"))
            sb.AppendLine($"| {x.Scenario} | {x.Engine} | {x.Count} | {x.MedianMs:F2} | {x.P95Ms:F2} | {x.MeanMs:F2} | {x.OpsPerSec:F0} |");

        sb.AppendLine("\n## Паралельне навантаження (10 с)\n");
        sb.AppendLine("| Сценарій | БД | Режим | Операцій | оп/с | Медіана, мс | p95, мс |");
        sb.AppendLine("|---|---|---|---:|---:|---:|---:|");
        foreach (var x in _results.Where(x => x.Mode != "послідовно"))
            sb.AppendLine($"| {x.Scenario} | {x.Engine} | {x.Mode} | {x.Count} | {x.OpsPerSec:F0} | {x.MedianMs:F2} | {x.P95Ms:F2} |");
        return sb.ToString();
    }

    public string ToCsv() => string.Join("\n",
        _results.Select(x => $"{listings};{x.Scenario};{x.Engine};{x.Mode};{x.Count};{x.MedianMs:F3};{x.P95Ms:F3};{x.MeanMs:F3};{x.OpsPerSec:F1}"));
}
