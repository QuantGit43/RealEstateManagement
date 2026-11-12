# RealEstateManagement

Real estate management system: property records, sale and rent listings, viewings, contracts, payments.

- **Lab 1** — relational database on MS SQL Server (20 tables, procedures, functions, triggers, views, indexes) and an ASP.NET Core Web API with the Repository + Unit of Work patterns. Report: [docs/lab1-report.md](docs/lab1-report.md).
- **Lab 2** — listing catalog in MongoDB, the same catalog in SQL, performance comparison, basic Redis queries. Report: [docs/lab2-report.md](docs/lab2-report.md).

![ER diagram](docs/er-diagram.webp)

## Tech stack

| | |
|---|---|
| DBMS | Microsoft SQL Server 2025 (LocalDB for Lab 1, Docker container for the Lab 2 benchmarks) |
| Application | .NET 10, ASP.NET Core Web API, Dapper, Swagger UI |
| Tests | xUnit (integration, on LocalDB), SQL checks in `sql/99_tests.sql` |
| NoSQL (Lab 2) | MongoDB 8.0, Redis 7.4 in Docker Compose |

## Quick start (Lab 1)

Requirements: Windows, .NET SDK 10, SQL Server LocalDB, `sqlcmd`.

```powershell
.\deploy.ps1 -Test                                           # create the DB, load data, run 9 SQL checks
dotnet test                                                  # 4 Unit of Work integration tests
dotnet run --project src\RealEstate.Api --launch-profile http
```

Swagger UI: http://localhost:5118/swagger. Write requests need the `X-User-Id` header (1–5, users from the test data).

## Structure

```
docs/
  lab1-report.md         Lab 1 report
  lab2-report.md         Lab 2 report
  requirements.md        system requirements
  schema.dbml            ER schema for dbdiagram.io
  er-diagram.webp        ER diagram
  nosql-overview.md      Lab 2: NoSQL types, what moves to NoSQL and why
  redis.md               Lab 2: basic Redis queries
  benchmarks/            Lab 2: raw benchmark results
sql/
  00–07                  database, schema, functions, views, procedures, triggers, indexes, data
  08_catalog.sql         Lab 2: SQL implementation of the catalog (EAV attributes, vw_ListingCards, sp_ListingCard_Create)
  99_tests.sql           business logic checks
  bench_indexed_view.sql Lab 2: indexed view for the performance experiment
src/RealEstate.Api/      Web API: Controllers, Data (Unit of Work, Repositories), Models
tests/RealEstate.Tests/  integration tests
benchmarks/              Lab 2: data generator and SQL Server vs MongoDB vs Redis benchmark
redis/                   Lab 2: Redis demo commands
deploy.ps1               database deployment
docker-compose.yml       Lab 2: MongoDB, SQL Server, Redis
```

## Lab 1: assignment checklist

| Item | Implementation |
|---|---|
| 1. Database schema | [docs/er-diagram.webp](docs/er-diagram.webp), [docs/schema.dbml](docs/schema.dbml) |
| 2. ≥ 15 entities | 20 tables — [sql/01_schema.sql](sql/01_schema.sql) |
| 3. Requirements, soft delete, audit | [docs/requirements.md](docs/requirements.md); soft delete on 5 tables, `UpdatedAt/UpdatedBy` on 5 tables |
| 4. Implementation in a DBMS | MS SQL Server, scripts in [sql/](sql), [deploy.ps1](deploy.ps1) |
| 5. ≥ 10 server-side objects | 26: 4 functions, 6 views, 11 procedures, 5 triggers |
| 6. ≥ 2 index types | clustered, unique, filtered, composite + INCLUDE, columnstore — [sql/06_indexes.sql](sql/06_indexes.sql) |
| 7. Repository + Unit of Work | [src/RealEstate.Api/Data](src/RealEstate.Api/Data); demo — `POST /api/deals` |

Details — in the [report](docs/lab1-report.md).

## Lab 2: NoSQL

Additional requirement: Docker Desktop.

```powershell
docker compose up -d                 # MongoDB :27018, SQL Server :14333, Redis :6379
.\benchmarks\run.ps1                 # benchmark on 10k / 100k / 1M listings (-Scales 10000 for a quick run)
.\redis\run-demo.ps1                 # basic Redis queries
```

| Item | Where |
|---|---|
| 1–2. NoSQL types, what moves and why | [docs/nosql-overview.md](docs/nosql-overview.md) |
| 3. MongoDB: `listingCards` collection | [MongoStore.cs](benchmarks/RealEstate.Benchmarks/MongoStore.cs), sample document [sample-card.json](docs/benchmarks/sample-card.json) |
| 4. The same in SQL (14 tables) | [sql/08_catalog.sql](sql/08_catalog.sql), [SqlStore.cs](benchmarks/RealEstate.Benchmarks/SqlStore.cs) |
| 5. Performance testing | [docs/lab2-report.md](docs/lab2-report.md), raw results in [docs/benchmarks/](docs/benchmarks) |
| 6. Redis (key-value) | [docs/redis.md](docs/redis.md), [redis/demo.redis](redis/demo.redis) |
