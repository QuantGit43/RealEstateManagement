using System.Diagnostics;
using MongoDB.Bson;
using RealEstate.Benchmarks;
using StackExchange.Redis;

var listings = args.Length > 0 ? int.Parse(args[0]) : 10_000;

var sqlCs = Environment.GetEnvironmentVariable("BENCH_SQL")
    ?? "Server=localhost,14333;Database=RealEstateDb;User Id=sa;Password=RealEstate_Bench1;TrustServerCertificate=True;Max Pool Size=200";
var mongoCs = Environment.GetEnvironmentVariable("BENCH_MONGO") ?? "mongodb://localhost:27018";
var redisCs = Environment.GetEnvironmentVariable("BENCH_REDIS") ?? "localhost:6379,allowAdmin=true";

var sql = new SqlStore(sqlCs);
var mongo = new MongoStore(mongoCs);
var redis = (await ConnectionMultiplexer.ConnectAsync(redisCs)).GetDatabase();

Console.WriteLine($"== Завантаження {listings:N0} оголошень");
var gen = new DataGenerator(listings);
var sqlLoad = new Stopwatch();
var mongoLoad = new Stopwatch();

await mongo.ResetAsync();
sqlLoad.Start();
await sql.LoadDimensionsAsync(gen);
sqlLoad.Stop();

const int batch = 20_000;
for (var from = 1; from <= listings; from += batch)
{
    var cards = gen.Cards(from, Math.Min(batch, listings - from + 1)).ToList();
    sqlLoad.Start(); await sql.LoadCardsAsync(cards); sqlLoad.Stop();
    mongoLoad.Start(); await mongo.LoadCardsAsync(cards); mongoLoad.Stop();
    Console.Write($"\r   {from + cards.Count - 1:N0} / {listings:N0}");
}
Console.WriteLine();

sqlLoad.Start(); await sql.FinishLoadAsync(); sqlLoad.Stop();
mongoLoad.Start(); await mongo.CreateIndexesAsync(); mongoLoad.Stop();

var (sqlData, sqlIndex) = await sql.SizeAsync();
var (mongoData, mongoIndex) = await mongo.SizeAsync();
Console.WriteLine($"   SQL: {sqlLoad.Elapsed.TotalSeconds:F1} с, {sqlData / 1024.0:F1} МБ даних + {sqlIndex / 1024.0:F1} МБ індексів");
Console.WriteLine($"   Mongo: {mongoLoad.Elapsed.TotalSeconds:F1} с, {mongoData / 1024.0:F1} МБ даних + {mongoIndex / 1024.0:F1} МБ індексів");

var sample = (await mongo.GetCardAsync(2))!;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var bench = new Bench(sql, mongo, redis, listings, Path.Combine(repoRoot, "sql", "bench_indexed_view.sql"));
await bench.RunAsync();

var header = $"""
    # Результати: {listings:N0} оголошень

    Дата: {DateTime.Now:yyyy-MM-dd HH:mm}. Машина: {Environment.ProcessorCount} логічних ядер, .NET {Environment.Version}.
    БД у Docker (WSL2), однакові ліміти: 4 CPU, 2 ГБ RAM; кеш MongoDB 1 ГБ, пам'ять SQL Server 1,5 ГБ.

    ## Обсяг і завантаження

    | БД | Дані, МБ | Індекси, МБ | Час завантаження, с |
    |---|---:|---:|---:|
    | SQL Server (14 таблиць) | {sqlData / 1024.0:F1} | {sqlIndex / 1024.0:F1} | {sqlLoad.Elapsed.TotalSeconds:F1} |
    | MongoDB (1 колекція) | {mongoData / 1024.0:F1} | {mongoIndex / 1024.0:F1} | {mongoLoad.Elapsed.TotalSeconds:F1} |

    Час завантаження — довідково: SQL Server завантажується з уже створеними індексами (bulk copy + перевірка FK + статистика),
    MongoDB — insertMany без індексів, індекси будуються після.

    """;

var outDir = Path.Combine(repoRoot, "docs", "benchmarks");
Directory.CreateDirectory(outDir);
await File.WriteAllTextAsync(Path.Combine(outDir, $"results-{listings}.md"), bench.ToMarkdown(header));
await File.WriteAllTextAsync(Path.Combine(outDir, $"results-{listings}.csv"),
    "listings;scenario;engine;mode;count;median_ms;p95_ms;mean_ms;ops_per_sec\n" + bench.ToCsv());
await File.WriteAllTextAsync(Path.Combine(outDir, "sample-card.json"),
    sample.ToBsonDocument().ToJson(new MongoDB.Bson.IO.JsonWriterSettings { Indent = true }));
Console.WriteLine($"== Збережено в {Path.GetFullPath(outDir)}");
