# Lab 2 Report: NoSQL

**Topic:** Moving part of the Real Estate Management System data to NoSQL, implementing the same data in SQL, comparing performance, and basic queries in a NoSQL database of another type.

**Author:** Yurii Saplyukov
**Databases:** MongoDB 8.0 (document), Redis 7.4 (key-value), Microsoft SQL Server 2025 (relational)
**Code:** C# / .NET 10, MongoDB.Driver, Dapper, StackExchange.Redis

## Contents
1. [Types of NoSQL databases](#1-types-of-nosql-databases)
2. [What moves to NoSQL and why](#2-what-moves-to-nosql-and-why)
3. [MongoDB implementation](#3-mongodb-implementation)
4. [The same data in SQL](#4-the-same-data-in-sql)
5. [Performance testing](#5-performance-testing)
6. [Redis: a NoSQL database of another type](#6-redis-a-nosql-database-of-another-type)
7. [Conclusions](#7-conclusions)
8. [Anticipated questions](#8-anticipated-questions)
9. [How to reproduce](#9-how-to-reproduce)

---

## 1. Types of NoSQL databases

| Type | Model | Examples | Good for |
|---|---|---|---|
| Key-value | key → value / data structure | Redis, DynamoDB | caches, counters, sessions, locks, rankings |
| Document | JSON/BSON documents with nesting | MongoDB, Couchbase | aggregates read as a whole, flexible schema |
| Wide-column | partition key → columns | Cassandra, ScyllaDB | massive writes, time series, query-first models |
| Graph | nodes + relationships | Neo4j, ArangoDB | traversals: recommendations, paths |
| Time series | timestamped measurements | InfluxDB, TimescaleDB | metrics, telemetry |
| Search engine | inverted index | Elasticsearch | full-text search, facets |

Strengths, weaknesses and common traits (schema-on-read, denormalization, sharding, CAP/BASE) — [nosql-overview.md](nosql-overview.md).

## 2. What moves to NoSQL and why

**Stays in SQL:** contracts, payments, debts, commissions, audit — money and legal obligations that need FKs, CHECK constraints, multi-entity transactions and triggers (Lab 1).

**New requirement that makes NoSQL reasonable:** a **public listing catalog**. Site visitors search listings by city, type, price, amenities and **type-specific attributes** (apartment: ceiling height, balcony, heating; house: land area, garage, floors; office: meeting rooms, open space, parking; commercial: storefront, power in kW).

**Why a document database fits the catalog:**
1. **~99% reads.** The card is read far more often than it changes, so it pays to store it ready-made.
2. **The card is an aggregate of 14 SQL tables** (`Listings`, `Properties`, `PropertyTypes`, `Addresses`, `Districts`, `Cities`, `Owners`, `Agents`, `Users`, `PropertyAmenities`, `Amenities`, `PropertyPhotos`, `PriceHistory`, `PropertyAttributes`) and **one document** in MongoDB.
3. **Attributes differ per type** and new ones keep appearing: EAV with a JOIN per filter in SQL vs a nested object plus indexes in MongoDB.
4. The catalog can be scaled (replicas, sharding by city) independently of the transactional database.

**Trade-offs accepted:** duplicated data (agent name, city name in every document), no FKs, the catalog is a derived projection of SQL (CQRS read model, eventual consistency of seconds).

**Redis** covers auxiliary tasks: card cache, view counters, hot listing rankings, temporary viewing slot locks (section 6).

## 3. MongoDB implementation

Collection `listingCards`, one document per listing. `_id` = `ListingId`. Full sample — [benchmarks/sample-card.json](benchmarks/sample-card.json).

```json
{
  "_id": 2,
  "title": "Квартира, 3 кімн., 25,5 м², Північний р-н",
  "typeId": 1, "type": "Квартира",
  "listingType": "Rent", "price": { "$numberDecimal": "10000" }, "currency": "UAH", "status": "Active",
  "listedAt": { "$date": "2025-07-23T03:26:00Z" },
  "address": { "cityId": 1, "city": "Київ", "districtId": 2, "district": "Північний",
               "street": "просп. Незалежності", "building": "141", "apartment": "188" },
  "areaSqm": { "$numberDecimal": "25.5" }, "rooms": 3, "floor": 6, "yearBuilt": 1951,
  "owner": { "_id": 201520, "name": "Андрій Бондар (201520)", "phone": "+380670201520" },
  "agent": { "_id": 104, "name": "Тарас Мельник", "phone": "+380500000104", "licenseNo": "LIC-B-00104" },
  "amenities": ["Балкон", "Кондиціонер", "Охорона", "Тераса", "Басейн"],
  "photos": [{ "url": "https://cdn.example/p/2/0.jpg", "isMain": true },
             { "url": "https://cdn.example/p/2/1.jpg", "isMain": false }],
  "priceHistory": [],
  "attributes": { "ceilingHeight": 2.87, "hasBalcony": true, "heating": "Центральне" }
}
```

(Values are generated test data in Ukrainian; field names are English.)

**Indexes** ([MongoStore.cs](../benchmarks/RealEstate.Benchmarks/MongoStore.cs)) — field order follows the ESR rule (Equality, Sort, Range); `_id` is appended so that the `(price, _id)` sort comes from the index:

| Index | Keys | Scenario |
|---|---|---|
| `_id` | `_id` | S1 card by Id |
| `ix_search` | `address.cityId, typeId, listingType, status, price, _id` | S2, S3 search |
| `ix_amenities` | `amenities, address.cityId, status, price, _id` (multikey) | S4 amenities |
| `ix_attributes` | `attributes.$**` (wildcard) | arbitrary attribute filters |
| `ix_type_price` | `typeId, status, price, _id` | S5 attributes + sort by price |
| `ix_report` | `listingType, status, typeId, address.cityId, address.city, price` | S6 report (covered) |
| `ix_agent` | `agent._id` | S9 rename agent |

**Write durability:** benchmark writes use `WriteConcern(w: 1, journal: true)` — the write is acknowledged only after it reaches the journal, the same guarantee as a `COMMIT` in SQL Server. Without it MongoDB would acknowledge writes before they are durable and the write comparison would be unfair.

## 4. The same data in SQL

The card is assembled from the Lab 1 tables plus one new table ([sql/08_catalog.sql](../sql/08_catalog.sql)):

| Document field | SQL |
|---|---|
| `_id`, `price`, `status`, `listingType`, `listedAt` | `Listings` |
| `title`, `areaSqm`, `rooms`, `floor`, `yearBuilt` | `Properties` |
| `type` | `PropertyTypes` |
| `address.*` | `Addresses` → `Districts` → `Cities` |
| `owner.*` | `Owners` |
| `agent.*` | `Agents` → `Users` |
| `amenities[]` | `PropertyAmenities` ↔ `Amenities` (M:N) |
| `photos[]` | `PropertyPhotos` |
| `priceHistory[]` | `PriceHistory` |
| `attributes{}` | **`PropertyAttributes(PropertyId, AttrKey, NumValue, TextValue)`** — new EAV table |

So **one document maps to 14 tables**.

- `vw_ListingCards` — card header (9-table join); collections are read with 4 more queries in the same batch.
- `sp_ListingCard_Create` — writes a card into 7 tables in one transaction; collections are passed as JSON (`OPENJSON`), so the statement text is constant and its plan is cached.
- Indexes for the same scenarios: `IX_Listings_Search (ListingType, Status, Price)`, `IX_Listings_Property_Search`, `IX_Properties_Address_Active`, `IX_PropertyAmenities_Amenity`, `IX_PropertyPhotos_Property`, `IX_PropertyAttributes_Num/Text (AttrKey, value)`, plus all Lab 1 indexes.
- Experiment: `vw_ListingSearch` — an **indexed (materialized) view** that brings the search fields from 4 tables into one index ([sql/bench_indexed_view.sql](../sql/bench_indexed_view.sql)). It is the SQL counterpart of denormalization and is measured separately (S2v, S3v, S7v, S8v).

## 5. Performance testing

### 5.1 Environment

| | |
|---|---|
| Host | AMD Ryzen 7 7840HS (8 cores / 16 threads), 27.8 GB RAM, NVMe SSD, Windows 11 |
| Containers | Docker Desktop (WSL2). **SQL Server and MongoDB get identical limits: 4 CPU, 2 GB RAM** |
| SQL Server | 2025 RTM-CU10 (17.0.5035.5), `MSSQL_MEMORY_LIMIT_MB = 1536` |
| MongoDB | 8.0.34, WiredTiger cache 1 GB |
| Redis | 7.4, `maxmemory 200mb`, `allkeys-lru` |
| Client | .NET 10 console app on the host, connection pooling for all three databases |

Both databases run in containers on the same VM. Comparing native LocalDB with a containerized MongoDB would mix the database difference with the virtualization overhead.

### 5.2 Data

A deterministic generator ([DataGenerator.cs](../benchmarks/RealEstate.Benchmarks/DataGenerator.cs), seed 42) produces cards that are written **identically** to both databases. Distributions are deliberately skewed, like a real catalog:
- 25 cities, quadratic skew: Kyiv ~20% of listings, the smallest cities ~1%; 6 districts per city;
- types: apartments 60%, houses 20%, offices 12%, commercial 8%; sale 60% / rent 40%;
- status by Id: 80% Active, 10% Reserved, 10% Closed;
- per card: 1–5 photos, ~3.5 amenities out of 10, 0–3 price changes, 3 type-specific attributes;
- 200 agents (so one agent has ~5,000 listings at 1M), owners = listings / 3.

**Three scales — 10k, 100k, 1M listings** — to show the trend instead of a single point. At 1M the SQL side holds ~14M rows (1M each of addresses/properties/listings, ~3M photos, ~3.5M amenity links, ~1.5M price changes, 3M attributes).

| Scale | SQL Server data + indexes | MongoDB data + indexes |
|---|---:|---:|
| 10k | 9.2 + 8.8 MB | 3.2 + 1.6 MB |
| 100k | 90.8 + 105.3 MB | 32.3 + 15.1 MB |
| 1M | **912.3 + 956.4 MB** | **324.2 + 154.2 MB** |

At 1M the SQL working set (1.87 GB) no longer fits into its 1.5 GB buffer pool, while MongoDB (0.48 GB compressed on disk) fits into its 1 GB cache. This is the scale where the storage model starts to matter.

### 5.3 Methodology

- **Same queries, same answers.** Each read scenario is first run with 30 random parameter sets on both databases and the results are compared by a checksum over the returned Ids and their order (for S1 — over the whole card: amenities, photos, history, attributes, price). **All read scenarios match 30/30 on all scales.**
- **Same parameters.** Parameters come from a seeded random generator (seed 2026), so SQL Server and MongoDB get exactly the same sequence. The city is picked with the same skew as the data: popular cities are searched more often.
- **Warm-up** (10% of iterations, not measured), then N measured calls (500 for fast reads, 200 for searches, 30 for the report, 300 for writes, 20 for renames).
- **Median and p95**, not just the mean: the median is robust to isolated spikes (GC, checkpoints), p95 shows the tail a real user would notice.
- **Reads before writes**, so writes do not change the data the reads are verified against.
- **Concurrent load:** 16 clients for 10 seconds — throughput (ops/s) and latency under contention.
- **Indexes on both sides** are designed per query; plans were checked with `SET STATISTICS IO` and `explain('executionStats')` (section 5.6).
- **Not measured:** cold cache (the OS page cache inside the VM cannot be dropped for MongoDB in a controlled way, and a production catalog is warm anyway); replication and sharding (single node); bulk load time is reported for reference only (SQL loads with indexes in place, MongoDB builds indexes afterwards).
- **Run validity:** the first 1M run was discarded — the host was at its memory commit limit (51.7 of 53.4 GB) and both databases slowed down 2–3×. It was rerun after freeing memory; the 1M results below are from the clean run.

### 5.4 Scenarios

| # | Scenario | Why it is tested |
|---|---|---|
| S1 | Full card by Id | the most frequent catalog operation: 1 document vs 14 tables |
| S2 | Search: city + type + sale/rent + price range, sorted by price, top 20 | the main search; filter fields live in 4 different SQL tables |
| S3 | Same search, page at offset 500 | cost of deep pagination |
| S4 | City + two required amenities | array (multikey index) vs M:N table |
| S5 | Houses with land ≥ N sotky and a garage | flexible attributes: nested object vs EAV |
| S6 | Report: count and average price by city | heavy aggregation, the classic SQL strength |
| S7 | Insert a new card | one document vs 7 tables in a transaction |
| S8 | Price change + history entry | atomic update in both (SQL: procedure + trigger) |
| S9 | Rename an agent | **the cost of denormalization**: 1 row vs thousands of documents |
| S1 + Redis | Card through a Redis cache | does a cache in front of MongoDB help |
| S2v, S3v, S7v, S8v | S2, S3, S7, S8 with the SQL indexed view | can SQL catch up by materializing, and at what cost |

### 5.5 Results

Raw results: [10k](benchmarks/results-10000.md), [100k](benchmarks/results-100000.md), [1M](benchmarks/results-1000000.md) (CSV next to them).

**Median latency, ms (1 client).** "×" = SQL time / MongoDB time.

| Scenario | 10k SQL | 10k Mongo | 100k SQL | 100k Mongo | 1M SQL | 1M Mongo | 1M × |
|---|---:|---:|---:|---:|---:|---:|---:|
| S1 card by Id | 1.57 | 1.06 | 1.21 | 0.93 | 3.14 | 1.04 | 3.0 |
| S2 search | 4.12 | 1.72 | 15.56 | 1.24 | 28.94 | 2.82 | 10.3 |
| S3 deep page | 7.86 | 0.93 | 31.96 | 1.11 | 44.89 | 2.41 | 18.6 |
| S4 amenities | 6.32 | 1.21 | 53.79 | 1.44 | 39.87 | 1.65 | 24.2 |
| S5 attributes | 5.60 | 1.24 | 32.42 | 1.21 | 24.96 | 2.02 | 12.4 |
| S6 report | 10.30 | 2.03 | 34.66 | 7.55 | **53.36** | **70.15** | **0.8** |
| S7 insert card | 3.95 | 1.88 | 4.49 | 2.39 | 6.10 | 2.90 | 2.1 |
| S8 price change | 2.81 | 2.33 | 3.25 | 2.79 | 4.49 | 3.45 | 1.3 |
| S9 rename agent | 2.83 | 4.18 | 3.43 | 8.78 | **3.26** | **304.01** | **0.01** |
| S2v search, SQL indexed view | 1.50 | 1.37 | 1.58 | 1.74 | 2.63 | 2.30 | 1.1 |
| S3v deep page, SQL indexed view | 2.69 | 1.21 | 8.64 | 1.52 | 10.37 | 2.04 | 5.1 |
| S7v insert, SQL with indexed view | 4.76 | 2.51 | 6.04 | 2.05 | 5.67 | 4.12 | 1.4 |
| S8v price change, SQL with indexed view | 3.21 | 2.03 | 3.23 | 2.38 | 3.29 | 2.40 | 1.4 |
| S1 through Redis (cache hit) | 0.54 | | 0.47 | | 0.58 | | |

Tail latency at 1M (p95, ms): S2 SQL **140.05** vs MongoDB 4.05; S6 MongoDB **443.23** vs SQL 95.72; S9 MongoDB 384.80 vs SQL 3.69.

S9: MongoDB updates on average 51 / 500 / 4,993 documents per call (10k / 100k / 1M); SQL Server updates 1 row.

**Throughput under load, ops/s (16 clients, 10 s).**

| Scenario | Scale | SQL Server | MongoDB | Redis (cache-aside) | Redis hit rate |
|---|---|---:|---:|---:|---:|
| S1 uniform Ids | 10k | 6,146 | 11,399 | 17,907 | 94% |
| | 100k | 5,603 | 12,198 | 7,138 | 28% |
| | 1M | 3,613 | 10,455 | 4,875 | 2% |
| S1 hot Ids (90% of requests to 1% of listings) | 10k | 5,660 | 11,056 | 16,454 | 95% |
| | 100k | 5,806 | 11,677 | 13,861 | 90% |
| | 1M | 4,893 | 9,267 | **12,823** | 82% |
| S2 search | 10k | 744 | 6,568 | | |
| | 100k | 192 | 5,583 | | |
| | 1M | **75** | **3,082** | | |
| S2v search, SQL indexed view | 10k | 6,619 | 7,778 | | |
| | 100k | 5,222 | 5,947 | | |
| | 1M | **4,078** | **3,470** | | |

### 5.6 Analysis

**S1 — card by Id.** MongoDB reads one document through the `_id` index. SQL Server runs a 9-table join for the header plus 4 queries for collections. At 10k–100k the gap is small (data in memory, every lookup is an index seek); at 1M it grows to 3× because the SQL working set exceeds the buffer pool.

**S2 / S3 / S4 — search.** The filter fields (city, type, sale/rent, status, price) live in four SQL tables, so no single index can serve the whole filter and the sort. The optimizer walks `IX_Listings_Search` in price order and checks the city through joins until it finds 20 matches. For a popular combination this is cheap, for a rare one it is not (1M, `SET STATISTICS IO`):

| Query | SQL Server | MongoDB |
|---|---|---|
| Kyiv, apartments, sale | 1,327 logical reads, 3 ms | `IXSCAN ix_search`: 20 keys, 20 documents, 4 ms |
| Small city, commercial, sale | **138,551 logical reads (135,007 in `Properties`), 162 ms** | `IXSCAN ix_search`: 20 keys, 20 documents, 3 ms |
| Same, SQL through indexed view | 371 logical reads, < 1 ms | |

This explains the small median and the large p95 of S2 in SQL, and the collapse under load (75 ops/s at 1M): concurrent rare searches each read hundreds of thousands of pages. MongoDB has all filter fields in one document, so one compound index answers the query with exactly 20 index entries. S4 is the same story with amenities: an array in the document with a multikey index vs two `EXISTS` over the M:N table.

**S2v — the key result.** With the indexed view, SQL Server also has all search fields in one index: 71 reads of the view, median 2.63 vs 2.30 ms, and under load it even exceeds MongoDB (4,078 vs 3,470 ops/s). **The search gap is caused by the data layout (normalized vs denormalized), not by "SQL vs NoSQL" as such.** The price of the view: it is maintained on every write to the 4 underlying tables (2.8 s to build at 1M; S7v insert has a similar median but a higher mean, 9.13 vs 6.20 ms, at 1M and +20–35% median at 10k/100k).

S3v remains 5× slower than MongoDB: after skipping 500 index entries SQL still has to join the 520 rows through 5 tables to return title, city and photo.

**S5 — attributes.** EAV needs two `EXISTS` per row; MongoDB uses `ix_type_price` (`LIMIT > FETCH > IXSCAN`, 76 keys, 76 documents at 1M). Lesson learned during the work: with only the wildcard index `attributes.$**`, MongoDB had to sort ~100k matching documents in memory (2.3 s per query at 1M); adding an index that provides the sort order brought it to 6–10 ms. Index design matters at least as much in MongoDB as in SQL.

**S6 — report.** The only read scenario where SQL Server wins at 1M (53 vs 70 ms median, 96 vs 443 ms p95). SQL Server runs a parallel plan (427 ms CPU in 146 ms elapsed on 4 cores) with hash aggregation. MongoDB scans 288k index keys of the covered `ix_report` in one thread (`GROUP > IXSCAN`, 0 documents fetched). Before `address.city` was added to the index (so it became covering), MongoDB fetched 288k documents and needed 1.6 s.

**S7 — insert.** One document vs 7 tables with FK checks and dozens of index entries (~16 rows per card, each table with a PK and 1–5 secondary indexes): MongoDB is about 2× faster at every scale. During the work, the first SQL version built a different statement text for each card (the number of photos/amenities varied) and spent ~16 ms recompiling; moving it to `sp_ListingCard_Create` with JSON parameters made the plan reusable (16 → 3.3 ms at 10k).

**S8 — price change.** Both are one atomic operation (SQL: procedure + trigger writing the history; MongoDB: one pipeline update that appends the old price to `priceHistory`). Results are close (1.3×).

**S9 — rename agent: where denormalization hurts.** SQL updates one row in `Users` (~3 ms on every scale). MongoDB has to rewrite the agent's name in every document: 51 → 500 → 4,993 documents, 4 → 9 → **304 ms**, i.e. it grows linearly with the data and is **~100× slower** than SQL at 1M.

**Storage.** MongoDB takes 2.8× less space for data and 6× less for indexes: WiredTiger compresses documents, there are no join keys and no per-row overhead of 14 tables, and SQL keeps a PK plus FK and search indexes on every table.

**Redis cache.** A cache hit costs ~0.5 ms (2× faster than reading the card from MongoDB). But cache-aside only pays off when access is skewed. With uniformly random Ids over 1M listings the hit rate is 2% and throughput drops below plain MongoDB (4,875 vs 10,455 ops/s) because every miss costs a MongoDB read plus a Redis `SET`. With realistic skew (90% of requests to 1% of listings) the hit rate is 82% and Redis gives the best throughput: 12,823 ops/s vs 9,267 for MongoDB and 4,893 for SQL Server.

## 6. Redis: a NoSQL database of another type

Redis (key-value, in memory) was used for basic queries on all main data structures, applied to the catalog: string with TTL (card cache), atomic counters (views), hash (agent profile), sorted set (hot listings ranking), list (recently viewed), set (shared favourites), `SET NX EX` (viewing slot booking that expires by itself), `MULTI/EXEC`, geo search within 3 km, stream (event log), pub/sub, key management (`SCAN`, `EXPIRE`, `PERSIST`).

Commands — [redis/demo.redis](../redis/demo.redis), output — [redis/demo-output.txt](../redis/demo-output.txt), explanation of every query — [redis.md](redis.md). In the benchmark Redis is a cache-aside layer in front of MongoDB (section 5.6).

## 7. Conclusions

1. **The catalog read model belongs in MongoDB.** On normalized SQL, search is 10–24× slower at 1M (median) with a heavy tail (p95 140 ms vs 4 ms), and under load SQL drops to 75 ops/s vs 3,082. The card is read 3× faster, inserted 2× faster and stored in 2.8× less space.
2. **The difference comes from data layout.** When SQL gets the same denormalized layout through an indexed view, its search equals MongoDB (2.63 vs 2.30 ms) and even exceeds it under load. NoSQL wins here because the document matches the query, not because of the engine.
3. **Denormalization has a price.** Renaming an agent is ~100× slower in MongoDB at 1M and grows with the data; SQL handles heavy aggregation better (parallel plans). Integrity (FKs, CHECK, transactions over money) stays in SQL.
4. **A cache helps only with skewed access.** Redis gives the best throughput for hot listings (82% hit rate) and is useless for uniformly random access.
5. **Resulting architecture:** SQL Server is the source of truth for transactional data (Lab 1); MongoDB is a read model for the public catalog, updated from SQL events; Redis caches hot cards and keeps counters, rankings and short-lived locks.

## 8. Anticipated questions

**Why is the data size like this?** Three scales up to 1M listings (~14M rows in SQL, 1.87 GB). 1M is where the SQL working set stops fitting into its memory limit, so the effect of the storage model becomes visible; 10k and 100k show the trend.

**Why do SQL and MongoDB have different indexes?** Each database got the best indexes for the same queries; both index sets are listed in sections 3–4, and plans were checked to make sure they are used. Identical index definitions are impossible: in normalized SQL the filter fields are in different tables.

**How do you know both databases return the same thing?** Every read scenario is checked on 30 parameter sets with a checksum of the returned Ids and their order — 30/30 on all scales.

**Why median and p95, not the average?** The average is distorted by single spikes (GC, checkpoints); the median shows the typical call and p95 the tail. Means are in the raw results.

**Why Docker for both databases?** Identical CPU/RAM limits and the same virtualization overhead for both; native SQL vs containerized MongoDB would not be a fair comparison.

**Isn't MongoDB faster at writes just because it does not wait for the disk?** No: writes use `journal: true`, the same durability as a SQL `COMMIT`.

**Why is there a warm-up and no cold-cache test?** A production catalog is warm; a cold cache cannot be reproduced reliably for MongoDB inside the VM (the OS page cache is shared). The memory effect is visible anyway at 1M, where SQL data exceeds its buffer pool.

**Why 16 concurrent clients?** The host has 16 logical cores and each database is limited to 4 CPUs, so 16 clients are enough to saturate the database and show contention.

**Where does MongoDB lose?** S9 (renaming shared data, ~100×), S6 (heavy aggregation at 1M), and anything that needs cross-entity integrity.

## 9. How to reproduce

```powershell
docker compose up -d                       # MongoDB :27018, SQL Server :14333, Redis :6379
.\benchmarks\run.ps1                       # 10k, 100k, 1M (about 40 min); -Scales 10000 for a quick run
.\redis\run-demo.ps1                       # Redis basic queries
```

`run.ps1` recreates the SQL schema in the container (`deploy.ps1 -NoSeed`), the benchmark loads identical data into both databases, runs all scenarios and writes `docs/benchmarks/results-<scale>.md/.csv`. Close heavy applications before the 1M run: memory pressure on the host slows both databases down.
