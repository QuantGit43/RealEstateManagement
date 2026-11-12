# NoSQL for the Real Estate Management System

## 1. Types of NoSQL databases

| Type | Data model | Examples | Strengths | Weaknesses |
|---|---|---|---|---|
| Key-value | key → value (string, hash, list, set, sorted set) | Redis, Memcached, DynamoDB | O(1) access by key, TTL, atomic counters, very low latency (in memory) | No queries by value, size limited by RAM, weak relationship model |
| Document | JSON/BSON documents with nested objects and arrays | MongoDB, Couchbase, CosmosDB | Flexible schema, the whole aggregate in one document (no JOINs), indexes on nested fields and arrays | Data duplication, shared data has to be updated in many documents, weak cross-collection relationships |
| Wide-column | row → set of columns, partitioned by key | Cassandra, ScyllaDB, HBase | Massive write throughput, horizontal scaling, time series | Queries only by partition key, "query-first" modelling, no JOINs |
| Graph | nodes + relationships with properties | Neo4j, ArangoDB, Neptune | Traversing relationships of any depth (recommendations, paths) | Aggregations over large sets, horizontal scaling |
| Time series | measurements with a timestamp | InfluxDB, TimescaleDB | Compression, downsampling, time windows | Time series only |
| Search engine | inverted index | Elasticsearch, OpenSearch | Full-text search, facets, geo search | Not a primary store, eventual consistency |

Common NoSQL traits compared with relational databases:
- the schema is defined by the application (schema-on-read), not by the database;
- denormalization instead of JOINs: data is modelled around queries;
- horizontal scaling (sharding) is part of the architecture;
- CAP/BASE trade-offs: often eventual consistency, although MongoDB has ACID transactions at the document level (and multi-document since 4.0).

## 2. Should part of the data move to NoSQL?

### What stays in SQL
Contracts, payments, debts, commissions and audit are **money and legal obligations**: they need FKs, CHECK constraints, transactions across several entities, triggers and reporting with JOINs. The relational model (Lab 1) is the right choice here.

### What moves to MongoDB: the listing catalog (property card)
New application requirement: a **public catalog** — site visitors search and browse listings, filtering by city, type, price, amenities and **type-specific attributes**:
- apartment: ceiling height, balcony, heating type;
- house: land area (in sotky, 1 sotka = 100 m²), garage, number of floors;
- office: meeting rooms, open space, parking spots;
- commercial premises: storefront, electrical capacity (kW).

Arguments **for** a document database:
1. **Workload profile**: the catalog is ~99% reads; writes are rare (new listing, price change). It pays to store the card ready-made instead of assembling it from tables on every view.
2. **Aggregate**: one card in SQL = `Listings` + `Properties` + `PropertyTypes` + `Addresses` + `Districts` + `Cities` + `Owners` + `Agents` + `Users` + `PropertyAmenities` + `Amenities` + `PropertyPhotos` + `PriceHistory` + `PropertyAttributes` — **14 tables**. In MongoDB it is 1 document with nested `address`, `agent`, `photos[]`, `amenities[]`, `priceHistory[]`, `attributes{}`.
3. **Flexible schema**: attributes differ per type and new ones keep appearing. In SQL this is either an EAV table (`PropertyAttributes(PropertyId, AttrKey, NumValue, TextValue)`) with a JOIN per filter, or an `ALTER TABLE` per new attribute. In MongoDB it is just a new field plus the wildcard index `attributes.$**`.
4. **Read scaling**: the catalog can be replicated / sharded by city independently of the transactional database.

Arguments **against** (honest trade-offs):
1. **Duplication**: the agent's name and phone and the city name are copied into every document. Renaming an agent means `updateMany` over thousands of documents instead of updating one row (a separate scenario in the benchmark).
2. **No FKs**: integrity is enforced by the application; orphaned references are possible.
3. **Aggregations** (reports by city/type) are supported by MongoDB (aggregation pipeline), but SQL with indexes is at least as good here.
4. **Synchronization**: SQL is the source of truth, the catalog is a derived projection (updated when a listing changes: outbox / event / CDC). Eventual consistency is acceptable for the catalog: a visitor sees a new price within seconds.

**Conclusion**: the "listing card" projection for the catalog moves to MongoDB (read model, CQRS approach). Transactional data stays in SQL.

### Redis (key-value) — auxiliary catalog data
Redis replaces neither database; it covers tasks for which both SQL and MongoDB are too heavy:
- listing view counters (`INCR`) — thousands of increments per second without row locks;
- ranking of "hot" listings (sorted set);
- cache of ready-made cards with TTL;
- "recently viewed" per visitor (trimmed list);
- temporary booking of a viewing slot (`SET NX EX`) — a lock that expires by itself;
- notifications about new listings (pub/sub).
