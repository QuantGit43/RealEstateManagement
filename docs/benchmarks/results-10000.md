# Results: 10,000 listings

Date: 2026-11-08 17:39. Machine: 16 logical cores, .NET 10.0.12.
Databases in Docker (WSL2) with equal limits: 4 CPU, 2 GB RAM; MongoDB cache 1 GB, SQL Server memory 1.5 GB.

## Size and load

| DB | Data, MB | Indexes, MB | Load time, s |
|---|---:|---:|---:|
| SQL Server (14 tables) | 9.2 | 8.8 | 1.8 |
| MongoDB (1 collection) | 3.2 | 1.6 | 0.8 |

Load time is for reference only: SQL Server loads with indexes already in place (bulk copy + FK validation + statistics),
MongoDB uses insertMany without indexes and builds them afterwards.

## Result equivalence check

- S1 Card by Id: 30/30 SQL and MongoDB results match
- S2 Search: city+type+price, top 20: 30/30 SQL and MongoDB results match
- S3 Deep page (skip 500): 30/30 SQL and MongoDB results match
- S4 Search by 2 amenities: 30/30 SQL and MongoDB results match
- S5 Filter by attributes (EAV / wildcard): 30/30 SQL and MongoDB results match
- S6 Report: average price by city: 30/30 SQL and MongoDB results match
- Redis, sequential: 500 hits, 0 misses
- Redis S1, 16 threads from an empty cache: 169,055 hits / 10,018 misses (94% hit)
- Redis S1hot, 16 threads from an empty cache: 156,399 hits / 8,146 misses (95% hit)
- S9: MongoDB updates on average 51 documents per call, SQL Server — 1 Users row
- Indexed view build: 0.1 s
- S2v Search: city+type+price, SQL via indexed view: 30/30 SQL and MongoDB results match
- S3v Deep page, SQL via indexed view: 30/30 SQL and MongoDB results match

## Sequential calls (1 client)

| Scenario | DB | N | Median, ms | p95, ms | Mean, ms | ops/s |
|---|---|---:|---:|---:|---:|---:|
| S1 Card by Id | SQL Server | 500 | 1.57 | 2.89 | 1.77 | 561 |
| S1 Card by Id | MongoDB | 500 | 1.06 | 1.44 | 1.11 | 897 |
| S2 Search: city+type+price, top 20 | SQL Server | 500 | 4.12 | 9.39 | 5.22 | 191 |
| S2 Search: city+type+price, top 20 | MongoDB | 500 | 1.72 | 2.89 | 1.79 | 557 |
| S3 Deep page (skip 500) | SQL Server | 200 | 7.86 | 10.75 | 8.07 | 124 |
| S3 Deep page (skip 500) | MongoDB | 200 | 0.93 | 1.21 | 0.97 | 1027 |
| S4 Search by 2 amenities | SQL Server | 200 | 6.32 | 8.34 | 6.58 | 152 |
| S4 Search by 2 amenities | MongoDB | 200 | 1.21 | 1.45 | 1.23 | 811 |
| S5 Filter by attributes (EAV / wildcard) | SQL Server | 200 | 5.60 | 7.57 | 5.72 | 175 |
| S5 Filter by attributes (EAV / wildcard) | MongoDB | 200 | 1.24 | 1.52 | 1.25 | 798 |
| S6 Report: average price by city | SQL Server | 30 | 10.30 | 16.78 | 10.97 | 91 |
| S6 Report: average price by city | MongoDB | 30 | 2.03 | 3.88 | 2.18 | 459 |
| S1 Card by Id | Redis (cache hit) | 500 | 0.54 | 0.88 | 0.59 | 1689 |
| S7 Insert new card | SQL Server | 300 | 3.95 | 5.25 | 3.98 | 251 |
| S7 Insert new card | MongoDB | 300 | 1.88 | 2.83 | 2.02 | 494 |
| S8 Price change + history | SQL Server | 300 | 2.81 | 4.05 | 2.85 | 351 |
| S8 Price change + history | MongoDB | 300 | 2.33 | 3.48 | 2.44 | 408 |
| S9 Rename agent | SQL Server | 20 | 2.83 | 3.02 | 2.60 | 384 |
| S9 Rename agent | MongoDB | 20 | 4.18 | 4.88 | 4.31 | 232 |
| S2v Search: city+type+price, SQL via indexed view | SQL Server | 500 | 1.50 | 2.25 | 1.55 | 645 |
| S2v Search: city+type+price, SQL via indexed view | MongoDB | 500 | 1.37 | 2.03 | 1.43 | 695 |
| S3v Deep page, SQL via indexed view | SQL Server | 200 | 2.69 | 5.58 | 3.10 | 322 |
| S3v Deep page, SQL via indexed view | MongoDB | 200 | 1.21 | 1.96 | 1.29 | 771 |
| S7v Insert card, SQL with indexed view | SQL Server | 300 | 4.76 | 7.29 | 5.11 | 196 |
| S7v Insert card, SQL with indexed view | MongoDB | 300 | 2.51 | 3.77 | 2.61 | 383 |
| S8v Price change, SQL with indexed view | SQL Server | 300 | 3.21 | 4.31 | 3.19 | 313 |
| S8v Price change, SQL with indexed view | MongoDB | 300 | 2.03 | 3.34 | 2.21 | 453 |

## Concurrent load (10 s)

| Scenario | DB | Mode | Operations | ops/s | Median, ms | p95, ms |
|---|---|---|---:|---:|---:|---:|
| S1 | SQL Server | 16 threads | 61456 | 6146 | 2.27 | 3.81 |
| S1 | MongoDB | 16 threads | 113987 | 11399 | 1.22 | 2.60 |
| S1 | Redis (cache-aside) | 16 threads | 179073 | 17907 | 0.67 | 2.28 |
| S1hot | SQL Server | 16 threads | 56605 | 5660 | 2.65 | 4.41 |
| S1hot | MongoDB | 16 threads | 110558 | 11056 | 1.28 | 2.60 |
| S1hot | Redis (cache-aside) | 16 threads | 164545 | 16454 | 0.73 | 2.23 |
| S2 | SQL Server | 16 threads | 7437 | 744 | 14.20 | 57.79 |
| S2 | MongoDB | 16 threads | 65681 | 6568 | 2.11 | 4.71 |
| S2v | SQL Server (indexed view) | 16 threads | 66191 | 6619 | 2.27 | 3.76 |
| S2v | MongoDB | 16 threads | 77776 | 7778 | 1.80 | 3.89 |
