# Redis (key-value): basic queries

Redis keeps data in memory as `key -> value`, where the value is one of several data structures. There is no schema and no query language: each structure has its own atomic commands. Commands run on a single thread, so every command is atomic without locks.

Run: `docker compose up -d`, then `.\redis\run-demo.ps1` (commands — [redis/demo.redis](../redis/demo.redis), output — [redis/demo-output.txt](../redis/demo-output.txt)). Interactive: `docker compose exec redis redis-cli -n 1 --raw`.

Keys are named with an entity prefix and colons (`card:42`, `views:listing:42`). This is a convention that Redis does not enforce, but `SCAN MATCH views:*` finds keys by pattern.

| # | Structure | Task in the real estate catalog | Commands | Why not SQL/MongoDB |
|---|---|---|---|---|
| 1 | String + TTL | Cache a ready-made card for 5 min | `SET k v EX 300`, `GET`, `TTL` | In-memory read ~0.5 ms; the entry expires by itself |
| 2 | String counter | Listing views | `INCR`, `INCRBY` | Thousands of increments/s without locking a `Listings` row |
| 3 | Hash | Agent profile (individual fields) | `HSET`, `HGET`, `HINCRBY`, `HGETALL` | Change one field without rewriting the whole value |
| 4 | Sorted set | Ranking of "hot" listings | `ZINCRBY`, `ZREVRANGE ... WITHSCORES`, `ZREVRANK`, `ZRANGEBYSCORE` | Top-N in O(log N), no `ORDER BY` over the whole table |
| 5 | List | Last 5 listings viewed by a client | `LPUSH`, `LTRIM 0 4`, `LRANGE` | Bounded queue: old items are trimmed |
| 6 | Set | Favourite listings shared by two clients | `SADD`, `SINTER`, `SUNION`, `SISMEMBER`, `SCARD` | Set operations on the server |
| 7 | String `NX EX` | Temporary booking of a viewing slot | `SET slot:42:... client:7 NX EX 600` | A second `SET NX` returns `nil` — a distributed lock that expires after 10 min |
| 8 | `MULTI/EXEC` | A view = counter + ranking + history together | `MULTI`, ..., `EXEC` | Commands run in sequence with no other client in between |
| 9 | Geo | Listings within 3 km | `GEOADD`, `GEOSEARCH ... BYRADIUS 3 km WITHDIST`, `GEODIST` | Geo index on top of a sorted set |
| 10 | Stream | Catalog event log (created, priceChanged) | `XADD * ...`, `XLEN`, `XRANGE` | Append-only log with time-based Ids, readable by consumer groups |
| 11 | Pub/Sub | Notification about a new listing | `PUBLISH channel msg` (returns the number of subscribers) | Delivery to subscribers without storage |
| 12 | Keys | Lookup and lifecycle | `SCAN MATCH`, `TYPE`, `EXPIRE`, `TTL`, `PERSIST`, `DEL`, `EXISTS`, `DBSIZE` | `SCAN` iterates without blocking the server (unlike `KEYS *`) |

## What the output shows

- `TTL card:42` -> `300`: the key expires in 5 min.
- `INCR` ×2 + `INCRBY 10` -> `12`.
- `ZREVRANGE hot:listings 0 2 WITHSCORES` -> `99 (22), 42 (18), 17 (7)`: listing 42 scored 15 + 3.
- `LTRIM 0 4` after 6 `LPUSH` -> the last 5 remain: `13 8 5 99 17`.
- `SINTER` -> `17 99`.
- The second `SET ... NX` -> empty line (`nil`): the slot is already taken by `client:7`.
- `GEOSEARCH` within 3 km of central Kyiv -> 42 (0 km) and 17 (1.47 km); 5 (7 km) and 99 (Lviv, 467 km) are not included.
- `PUBLISH` -> `0` subscribers (nobody is subscribed in the demo; `SUBSCRIBE channel:new-listings` in a second `redis-cli` would receive the message).
- `PERSIST` -> `TTL` becomes `-1` (the key no longer expires).

## Redis in the benchmark
In [Bench.cs](../benchmarks/RealEstate.Benchmarks/Bench.cs) Redis is a card cache using the **cache-aside** pattern on top of MongoDB (`StackExchange.Redis`):
1. `GET card:{id}` -> found: deserialize the JSON and return it;
2. not found: read from MongoDB, `SET card:{id} json EX 600`.

Results — the "Redis" rows in the [Lab 2 report](lab2-report.md).

## Limitations
- Data lives in RAM: a catalog of 1M cards (~1.5 GB of JSON) does not fit into a 256 MB container — only "hot" cards are cached (LRU eviction, `maxmemory-policy allkeys-lru`).
- Persistence (RDB/AOF) exists, but Redis is not the primary store: the latest writes can be lost on a crash.
- No queries by value: "find all cards cheaper than 1M" is not a Redis task.
