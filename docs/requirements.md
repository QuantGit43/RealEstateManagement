# Real Estate Management System — Requirements

## Roles
- **Admin** — manages reference data (cities, districts, property types, amenities) and users.
- **Manager** — oversees deals, payments and agent reports.
- **Agent** — maintains listings and viewings, signs contracts.
- **Owner** (external) — owner of a property.
- **Client** (external) — buyer or tenant.

## Functional requirements
1. Property records: type, address (city → district → address), area, rooms, floor, year built, amenities, photos.
2. Sale or rent listings for a property: price, currency, responsible agent, status (Active / Reserved / Closed).
3. Every listing price change is recorded in the price history automatically.
4. Clients can book property viewings.
5. A sale or lease contract is signed for a listing. After signing, the listing is closed and the agent's commission is calculated automatically.
6. Contract payments (schedule + actual payment), debt and overdue payment tracking. A sale contract is completed automatically once fully paid.
7. Maintenance / repair requests for a property.
8. Client reviews of agents (rating 1–5).
9. Reports: active properties and listings, agent performance, overdue payments.

## Non-functional requirements
- **Soft delete**: properties, listings, clients, owners and agents are never physically deleted — `IsDeleted = 1`, `DeletedAt`, `DeletedBy` are set instead. Even a plain `DELETE` on Properties/Listings is turned into a soft delete by a trigger.
- **Audit**: Properties, Listings, Contracts, Clients and Payments store `CreatedAt/CreatedBy` and `UpdatedAt/UpdatedBy`.
- **Optimistic locking** of the listing price via `rowversion`.
- The application accesses the database **only** through stored procedures (writes) and views / table-valued functions (reads).
- Several related changes (property → listing → contract → payment) run atomically in one transaction (Unit of Work).
