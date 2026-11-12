# RealEstateManagement

Система управління нерухомістю: MS SQL Server (LocalDB) + ASP.NET Core Web API (.NET 10, Dapper, Repository + Unit of Work).

## Запуск
```powershell
.\deploy.ps1 -Test                                   # створити БД, дані, прогнати sql\99_tests.sql
dotnet test                                          # інтеграційні тести UoW (потрібна розгорнута БД)
dotnet run --project src\RealEstate.Api --launch-profile http
```
Swagger UI: http://localhost:5118/swagger. Запити на запис потребують заголовок `X-User-Id` (1–5 з seed-даних).

## Що де лежить
| Вимога | Де |
|---|---|
| 1. ER-схема | `docs/schema.dbml` → вставити на dbdiagram.io, експорт PNG |
| 2–3. 20 сутностей, soft delete, аудит | `docs/requirements.md`, `sql/01_schema.sql` |
| 4. Реалізація в СУБД | `sql/00`–`07`, `deploy.ps1` |
| 5. SP / функції / тригери / View (28 об'єктів) | `sql/02_functions.sql`, `03_views.sql`, `04_procedures.sql`, `05_triggers.sql` |
| 6. Індекси: unique, filtered, composite+INCLUDE, columnstore | `sql/06_indexes.sql` |
| 7. Repository + Unit of Work | `src/RealEstate.Api/Data`, демо — `POST /api/deals` |

Серверні об'єкти:
- Функції: `fn_CalcCommission`, `fn_GetContractDebt`, `fn_PropertiesByCity` (TVF), `fn_ListingsByAgent` (TVF).
- View: `vw_ActiveProperties`, `vw_ActiveListings`, `vw_ContractDetails`, `vw_PaymentsByContract`, `vw_OverduePayments`, `vw_AgentPerformance`.
- Процедури: `sp_Property_Create/Update/SoftDelete`, `sp_Listing_Create/UpdatePrice/SoftDelete`, `sp_Client_Create/SoftDelete`, `sp_Contract_Create`, `sp_Payment_Register/MarkPaid`.
- Тригери: `trg_Listings_PriceHistory`, `trg_Properties_InsteadOfDelete`, `trg_Listings_InsteadOfDelete`, `trg_Contracts_SetUpdatedAt`, `trg_Payments_CompleteContract`.

## ЛР2: NoSQL (MongoDB + Redis)

```powershell
docker compose up -d                 # MongoDB :27018, SQL Server :14333, Redis :6379 (однакові ліміти CPU/RAM)
.\benchmarks\run.ps1                 # тест швидкодії на 10k / 100k / 1M оголошень (довго; -Scales 10000 для швидкої перевірки)
.\redis\run-demo.ps1                 # базові запити Redis
```

| Пункт | Де |
|---|---|
| 1–2. Види NoSQL, що і чому виносимо | `docs/nosql-overview.md` |
| 3. MongoDB: колекція `listingCards` | `benchmarks/RealEstate.Benchmarks/MongoStore.cs`, приклад документа `docs/benchmarks/sample-card.json` |
| 4. Те саме в SQL (14 таблиць) | `sql/08_catalog.sql` (EAV `PropertyAttributes`, `vw_ListingCards`, `sp_ListingCard_Create`), `SqlStore.cs` |
| 5. Тест швидкодії | `docs/benchmarks/report.md`, сирі результати `docs/benchmarks/results-*.md/csv` |
| 6. Redis (key-value) | `docs/redis.md`, `redis/demo.redis`, вивід `redis/demo-output.txt` |
