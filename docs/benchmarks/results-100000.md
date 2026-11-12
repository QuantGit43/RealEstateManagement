# Results: 100,000 listings

Date: 2026-11-08 17:43. Machine: 16 logical cores, .NET 10.0.12.
Databases in Docker (WSL2) with equal limits: 4 CPU, 2 GB RAM; MongoDB cache 1 GB, SQL Server memory 1.5 GB.

## Size and load

| DB | Data, MB | Indexes, MB | Load time, s |
|---|---:|---:|---:|
| SQL Server (14 tables) | 90.8 | 105.3 | 13.5 |
| MongoDB (1 collection) | 32.3 | 15.1 | 5.5 |

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
- Redis S1, 16 threads from an empty cache: 20,255 hits / 51,130 misses (28% hit)
- Redis S1hot, 16 threads from an empty cache: 124,845 hits / 13,767 misses (90% hit)
- S9: MongoDB updates on average 500 documents per call, SQL Server — 1 Users row
- Indexed view build: 0.3 s
- S2v Search: city+type+price, SQL via indexed view: 30/30 SQL and MongoDB results match
- S3v Deep page, SQL via indexed view: 30/30 SQL and MongoDB results match

## Sequential calls (1 client)

| Scenario | DB | N | Median, ms | p95, ms | Mean, ms | ops/s |
|---|---|---:|---:|---:|---:|---:|
| S1 Card by Id | SQL Server | 500 | 1.21 | 1.46 | 1.23 | 810 |
| S1 Card by Id | MongoDB | 500 | 0.93 | 1.08 | 0.95 | 1048 |
| S2 Search: city+type+price, top 20 | SQL Server | 500 | 15.56 | 42.96 | 20.99 | 48 |
| S2 Search: city+type+price, top 20 | MongoDB | 500 | 1.24 | 1.63 | 1.27 | 784 |
| S3 Deep page (skip 500) | SQL Server | 200 | 31.96 | 52.43 | 37.81 | 26 |
| S3 Deep page (skip 500) | MongoDB | 200 | 1.11 | 1.65 | 1.18 | 844 |
| S4 Search by 2 amenities | SQL Server | 200 | 53.79 | 72.41 | 67.52 | 15 |
| S4 Search by 2 amenities | MongoDB | 200 | 1.44 | 2.17 | 1.53 | 651 |
| S5 Filter by attributes (EAV / wildcard) | SQL Server | 200 | 32.42 | 52.40 | 37.74 | 26 |
| S5 Filter by attributes (EAV / wildcard) | MongoDB | 200 | 1.21 | 1.58 | 1.27 | 784 |
| S6 Report: average price by city | SQL Server | 30 | 34.66 | 48.30 | 36.48 | 27 |
| S6 Report: average price by city | MongoDB | 30 | 7.55 | 31.61 | 13.42 | 75 |
| S1 Card by Id | Redis (cache hit) | 500 | 0.47 | 0.55 | 0.48 | 2091 |
| S7 Insert new card | SQL Server | 300 | 4.49 | 6.06 | 4.56 | 219 |
| S7 Insert new card | MongoDB | 300 | 2.39 | 3.97 | 2.59 | 385 |
| S8 Price change + history | SQL Server | 300 | 3.25 | 4.90 | 3.30 | 303 |
| S8 Price change + history | MongoDB | 300 | 2.79 | 4.11 | 2.90 | 345 |
| S9 Rename agent | SQL Server | 20 | 3.43 | 4.17 | 3.77 | 265 |
| S9 Rename agent | MongoDB | 20 | 8.78 | 10.41 | 8.85 | 113 |
| S2v Search: city+type+price, SQL via indexed view | SQL Server | 500 | 1.58 | 3.17 | 1.82 | 548 |
| S2v Search: city+type+price, SQL via indexed view | MongoDB | 500 | 1.74 | 2.80 | 1.85 | 541 |
| S3v Deep page, SQL via indexed view | SQL Server | 200 | 8.64 | 13.10 | 8.72 | 115 |
| S3v Deep page, SQL via indexed view | MongoDB | 200 | 1.52 | 2.51 | 1.60 | 624 |
| S7v Insert card, SQL with indexed view | SQL Server | 300 | 6.04 | 32.22 | 8.79 | 114 |
| S7v Insert card, SQL with indexed view | MongoDB | 300 | 2.05 | 3.12 | 2.18 | 458 |
| S8v Price change, SQL with indexed view | SQL Server | 300 | 3.23 | 4.45 | 3.26 | 306 |
| S8v Price change, SQL with indexed view | MongoDB | 300 | 2.38 | 3.85 | 2.52 | 397 |

## Concurrent load (10 s)

| Scenario | DB | Mode | Operations | ops/s | Median, ms | p95, ms |
|---|---|---|---:|---:|---:|---:|
| S1 | SQL Server | 16 threads | 56034 | 5603 | 2.14 | 3.59 |
| S1 | MongoDB | 16 threads | 121977 | 12198 | 1.16 | 2.38 |
| S1 | Redis (cache-aside) | 16 threads | 71385 | 7138 | 2.28 | 3.86 |
| S1hot | SQL Server | 16 threads | 58062 | 5806 | 2.58 | 4.30 |
| S1hot | MongoDB | 16 threads | 116766 | 11677 | 1.20 | 2.49 |
| S1hot | Redis (cache-aside) | 16 threads | 138612 | 13861 | 0.82 | 2.98 |
| S2 | SQL Server | 16 threads | 1916 | 192 | 88.21 | 173.21 |
| S2 | MongoDB | 16 threads | 55830 | 5583 | 2.55 | 5.26 |
| S2v | SQL Server (indexed view) | 16 threads | 52221 | 5222 | 2.90 | 4.65 |
| S2v | MongoDB | 16 threads | 59466 | 5947 | 2.36 | 5.04 |
