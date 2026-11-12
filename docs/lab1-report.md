# Звіт до лабораторної роботи №1

**Тема:** Система управління нерухомістю — проєктування й реалізація реляційної БД, серверна логіка, доступ з коду через Repository + Unit of Work.

**Автор:** Юрій Саплюков
**СУБД:** Microsoft SQL Server 2025 (LocalDB)
**Код:** C# / .NET 10, ASP.NET Core Web API, Dapper

## Зміст
1. [Предметна область і вимоги](#1-предметна-область-і-вимоги)
2. [Схема бази даних](#2-схема-бази-даних)
3. [Сутності, soft delete та аудит](#3-сутності-soft-delete-та-аудит)
4. [Реалізація в СУБД](#4-реалізація-в-субд)
5. [Збережені процедури, функції, тригери, View](#5-збережені-процедури-функції-тригери-view)
6. [Індекси](#6-індекси)
7. [Repository + Unit of Work](#7-repository--unit-of-work)
8. [Тестування](#8-тестування)
9. [Висновки](#9-висновки)

---

## 1. Предметна область і вимоги

Система обслуговує агентство нерухомості: облік об'єктів, оголошення про продаж та оренду, перегляди, укладання договорів, облік платежів, заявки на ремонт і відгуки клієнтів.

**Ролі:**
- **Admin** — керує довідниками й користувачами;
- **Manager** — контролює угоди, платежі, звіти;
- **Agent** — веде оголошення, перегляди, укладає договори;
- **Owner**, **Client** — зовнішні особи (власник, покупець/орендар).

**Ключові вимоги:**
1. Об'єкт має тип, адресу (місто → район → адреса), площу, кімнати, зручності, фото.
2. Оголошення: продаж або оренда, ціна, відповідальний агент, статус `Active / Reserved / Closed`.
3. Кожна зміна ціни автоматично потрапляє в історію цін.
4. Договір укладається за активним оголошенням; оголошення закривається, комісія агента рахується автоматично.
5. Платежі за договором: графік і факт оплати, контроль боргу та прострочень; договір купівлі автоматично завершується після повної оплати.
6. Дані об'єктів, оголошень, клієнтів, власників і агентів не видаляються фізично (soft delete).
7. Для ключових сутностей зберігаються дата та автор останньої зміни.
8. Застосунок працює з БД **тільки** через збережені процедури (запис) і View / табличні функції (читання).

Повний опис — [requirements.md](requirements.md).

## 2. Схема бази даних

ER-діаграма побудована в [dbdiagram.io](https://dbdiagram.io) з опису [schema.dbml](schema.dbml) (його можна вставити на dbdiagram.io для інтерактивного перегляду).

![ER-діаграма](er-diagram.webp)

**Типи зв'язків:**

| Зв'язок | Тип | Приклад |
|---|---|---|
| Users → Agents | 1:1 | агент — це користувач з профілем (ліцензія, ставка комісії); `UQ_Agents_User` |
| Listings → Contracts | 1:1 | одне оголошення — щонайбільше один договір; `UQ_Contracts_Listing` |
| Properties ↔ Amenities | M:N | через таблицю `PropertyAmenities` зі складеним PK |
| Cities → Districts → Addresses → Properties | 1:N (ланцюжок) | нормалізація адреси |
| Properties → Listings, Listings → PriceHistory, Contracts → Payments | 1:N | |
| Users → поля `CreatedBy / UpdatedBy / DeletedBy` | 1:N | автор зміни в аудит-полях |

Усі зовнішні ключі — без каскадного видалення (`NO ACTION`): фізично видаляти пов'язані дані не можна, використовується soft delete.

## 3. Сутності, soft delete та аудит

**20 таблиць** (мінімум за завданням — 15):

| Група | Таблиці |
|---|---|
| Користувачі | `Roles`, `Users`, `Agents` |
| Сторони угод | `Owners`, `Clients` |
| Географія | `Cities`, `Districts`, `Addresses` |
| Об'єкти | `PropertyTypes`, `Properties`, `Amenities`, `PropertyAmenities`, `PropertyPhotos` |
| Оголошення | `Listings`, `PriceHistory`, `Viewings` |
| Угоди | `Contracts`, `Payments` |
| Обслуговування | `MaintenanceRequests`, `Reviews` |

**Soft delete** (`IsDeleted bit`, `DeletedAt`, `DeletedBy` → `Users`): `Agents`, `Owners`, `Clients`, `Properties`, `Listings`.

**Аудит змін** (`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` → `Users`): `Clients`, `Properties`, `Listings`, `Contracts`, `Payments`.

**Оптимістичне блокування:** `Listings.RowVersion` (`rowversion`) — процедура зміни ціни відхиляє зміну, якщо запис встиг змінити інший користувач.

**Обмеження цілісності:** `CHECK` (ціна > 0, площа > 0, рейтинг 1–5, `EndDate >= StartDate`, перелік допустимих статусів і типів), `UNIQUE`, `DEFAULT`.

## 4. Реалізація в СУБД

Скрипти в папці [`sql/`](../sql) виконуються по порядку скриптом [`deploy.ps1`](../deploy.ps1), який перестворює БД з нуля:

| Скрипт | Вміст |
|---|---|
| `00_create_db.sql` | створення БД `RealEstateDb` |
| `01_schema.sql` | 20 таблиць, PK, FK, CHECK, DEFAULT, UNIQUE |
| `02_functions.sql` | скалярні функції |
| `03_views.sql` | View і табличні функції |
| `04_procedures.sql` | збережені процедури |
| `05_triggers.sql` | тригери |
| `06_indexes.sql` | індекси |
| `07_seed.sql` | тестові дані (договори й платежі — через процедури, щоб спрацювали тригери) |
| `99_tests.sql` | автоматичні перевірки бізнес-логіки |

```powershell
.\deploy.ps1 -Test
```

## 5. Збережені процедури, функції, тригери, View

Усього **26 об'єктів** (мінімум — 10).

### Функції (4)

| Функція | Тип | Призначення |
|---|---|---|
| `fn_CalcCommission(@Amount, @Rate)` | скалярна | комісія агента від суми угоди |
| `fn_GetContractDebt(@ContractId)` | скалярна | залишок боргу: сума договору мінус сплачені платежі |
| `fn_PropertiesByCity(@CityId)` | таблична (inline) | активні об'єкти міста |
| `fn_ListingsByAgent(@AgentId)` | таблична (inline) | оголошення агента |

### View (6)

| View | Що показує |
|---|---|
| `vw_ActiveProperties` | об'єкти без soft-deleted з типом, адресою і власником |
| `vw_ActiveListings` | оголошення без soft-deleted з об'єктом, містом і агентом |
| `vw_ContractDetails` | договори з учасниками, сплаченою сумою і боргом |
| `vw_PaymentsByContract` | платежі з клієнтом і ознакою оплати |
| `vw_OverduePayments` | неоплачені платежі з минулим терміном |
| `vw_AgentPerformance` | показники агентів: оголошення, угоди, комісія, рейтинг |

### Збережені процедури (11)

| Процедура | Що робить |
|---|---|
| `sp_Property_Create / Update` | створення й зміна об'єкта з аудитом |
| `sp_Property_SoftDelete` | soft delete об'єкта разом з оголошеннями; заборонено при активному договорі |
| `sp_Listing_Create` | нове оголошення; заборонено два активних оголошення одного типу на об'єкт |
| `sp_Listing_UpdatePrice` | зміна ціни з перевіркою `RowVersion` |
| `sp_Listing_SoftDelete` | soft delete оголошення |
| `sp_Client_Create / SoftDelete` | клієнти |
| `sp_Contract_Create` | договір за активним оголошенням: тип і агент з оголошення, комісія через `fn_CalcCommission`, оголошення закривається |
| `sp_Payment_Register` | платіж або запланований платіж; переплата понад борг заборонена |
| `sp_Payment_MarkPaid` | позначити запланований платіж сплаченим |

Помилки бізнес-правил повертаються через `THROW` з кодами: `50400` — невалідні дані, `50404` — не знайдено, `50409` — конфлікт стану. Web API перетворює їх на HTTP 400 / 404 / 409.

Процедури з кількома змінами (`sp_Property_SoftDelete`, `sp_Contract_Create`) працюють і самостійно, і всередині транзакції застосунку: якщо транзакція вже відкрита, використовується `SAVE TRANSACTION`, тож помилка в процедурі не руйнує зовнішню транзакцію Unit of Work.

### Тригери (5)

| Тригер | Подія | Логіка |
|---|---|---|
| `trg_Listings_PriceHistory` | AFTER UPDATE на `Listings` | при зміні ціни пише рядок у `PriceHistory` (стара/нова ціна, автор) |
| `trg_Properties_InsteadOfDelete` | INSTEAD OF DELETE на `Properties` | `DELETE` перетворюється на soft delete об'єкта та його оголошень |
| `trg_Listings_InsteadOfDelete` | INSTEAD OF DELETE на `Listings` | `DELETE` перетворюється на soft delete |
| `trg_Contracts_SetUpdatedAt` | AFTER UPDATE на `Contracts` | оновлює `UpdatedAt` при будь-якій зміні договору |
| `trg_Payments_CompleteContract` | AFTER INSERT, UPDATE на `Payments` | договір купівлі стає `Completed`, коли борг дорівнює нулю |

## 6. Індекси

| Тип | Індекс | Для чого |
|---|---|---|
| Clustered | PK кожної таблиці | фізичний порядок рядків за ключем |
| Unique nonclustered | `UX_Users_Email`, `UX_Agents_LicenseNo` | унікальність логіна й ліцензії |
| Filtered unique | `UX_Clients_Phone_Active … WHERE IsDeleted = 0` | телефон унікальний лише серед не видалених клієнтів |
| Filtered nonclustered | `IX_Properties_Owner_Active`, `IX_Listings_Property_Active` (`WHERE IsDeleted = 0`) | запити працюють лише з активними рядками, індекс менший |
| Composite + INCLUDE (покриваючий) | `IX_Listings_Status_Price (Status, Price) INCLUDE (PropertyId, AgentId, ListingType)` | пошук активних оголошень у діапазоні цін без звернення до таблиці |
| Nonclustered по FK | 8 індексів (`IX_Contracts_Client`, `IX_Payments_Contract` тощо) | швидкі JOIN і перевірки FK |
| Nonclustered columnstore | `NCCI_Payments (ContractId, Amount, DueDate, PaidAt)` | агрегатні звіти по платежах |

Використання індексів демонструється в кінці `99_tests.sql` через `SET STATISTICS IO ON`: кожен із трьох контрольних запитів читає 2 сторінки.

## 7. Repository + Unit of Work

Логічно пов'язаний ланцюжок сутностей: **Property → Listing → Contract → Payment**.

```
Controller ──> IUnitOfWork ──┬── IPropertyRepository ──> vw_ActiveProperties, fn_PropertiesByCity / sp_Property_*
                             ├── IListingRepository  ──> vw_ActiveListings, fn_ListingsByAgent / sp_Listing_*
                             ├── IContractRepository ──> vw_ContractDetails / sp_Contract_Create
                             └── IPaymentRepository  ──> vw_PaymentsByContract, vw_OverduePayments / sp_Payment_*
                  одне SqlConnection + одна SqlTransaction на всі репозиторії
```

- [`UnitOfWork.cs`](../src/RealEstate.Api/Data/UnitOfWork.cs) — відкриває одне з'єднання, `BeginTransaction / Commit / Rollback`; незакомічена транзакція відкочується при `Dispose`.
- Репозиторії ([`Data/Repositories`](../src/RealEstate.Api/Data/Repositories)) отримують спільні з'єднання й транзакцію від UoW. **Читання** — тільки `SELECT` з View / табличних функцій, **запис** — тільки `CommandType.StoredProcedure`. Прямих запитів до таблиць немає.
- Користувач для аудиту передається в кожну процедуру параметром `@UserId` (в API — із заголовка `X-User-Id`).

Демонстрація — `POST /api/deals` ([`DealsController.cs`](../src/RealEstate.Api/Controllers/DealsController.cs)): чотири процедури в одній транзакції.

```csharp
uow.BeginTransaction();
try
{
    var propertyId = await uow.Properties.CreateAsync(request.Property, userId);
    var listingId  = await uow.Listings.CreateAsync(propertyId, request.Listing, userId);
    var contractId = await uow.Contracts.CreateAsync(listingId, request.Contract, userId);
    var paymentId  = await uow.Payments.RegisterAsync(contractId, request.Payment, userId);
    uow.Commit();
    ...
}
catch
{
    uow.Rollback();
    throw;
}
```

Якщо будь-який крок падає (наприклад, `sp_Payment_Register` відхиляє переплату), відкочуються всі чотири записи.

**Ендпоінти API** (Swagger: `http://localhost:5118/swagger`):

| Метод | Шлях | Дія |
|---|---|---|
| GET | `/api/properties/{id}`, `/api/properties?cityId=` | об'єкт / об'єкти міста |
| POST, PUT, DELETE | `/api/properties`, `/api/properties/{id}` | створення, зміна, soft delete |
| GET | `/api/listings`, `/api/listings?agentId=`, `/api/listings/{id}` | оголошення |
| POST | `/api/properties/{id}/listings` | нове оголошення |
| PUT | `/api/listings/{id}/price` | зміна ціни (з `RowVersion`) |
| DELETE | `/api/listings/{id}` | soft delete |
| POST | `/api/listings/{id}/contract` | договір |
| GET | `/api/contracts/{id}`, `/api/contracts/{id}/payments`, `/api/payments/overdue` | договір, платежі, прострочені |
| POST, PUT | `/api/contracts/{id}/payments`, `/api/payments/{id}/paid` | платіж, оплата |
| POST | `/api/deals` | угода "під ключ" в одній транзакції |

## 8. Тестування

**SQL-перевірки** (`sql/99_tests.sql`, виконуються в транзакції з `ROLLBACK`, дані не змінюються) — 9/9 пройдено:

| # | Що перевіряється |
|---|---|
| 1 | `DELETE FROM Properties` не видаляє рядок, а робить soft delete (з `DeletedBy` із `SESSION_CONTEXT`) |
| 2 | `trg_Listings_PriceHistory` записує історію цін з автором |
| 3 | процедура заповнює `UpdatedAt / UpdatedBy` |
| 4 | `sp_Contract_Create` закриває оголошення, комісія = 3% від суми; повна оплата → договір `Completed` |
| 5 | `fn_GetContractDebt`, `vw_OverduePayments` |
| 6 | переплата відхиляється з кодом 50400 |
| 7 | застарілий `RowVersion` відхиляється з кодом 50409 |
| 8 | `fn_PropertiesByCity`, `fn_ListingsByAgent` |
| 9 | помилка процедури всередині зовнішньої транзакції не ламає її (savepoint) |

**Інтеграційні тести** (`tests/RealEstate.Tests`, xUnit, `dotnet test`) — 4/4 пройдено:

| Тест | Що перевіряє |
|---|---|
| `Deal_Commit_SavesAllEntities` | угода з 4 сутностей зберігається; тригер завершує договір, борг 0, комісія 30 000 |
| `Deal_FailsOnPayment_RollsBackEverything` | помилка на кроці платежу → після `Rollback` у БД немає ні об'єкта, ні оголошення, ні договору |
| `SoftDelete_HidesPropertyAndItsListings` | soft delete приховує об'єкт і його оголошення з View |
| `UpdatePrice_WithStaleRowVersion_Throws409` | оптимістичне блокування |

**Ручна перевірка API:** `POST /api/deals` → 201; угода з помилкою → 400, у БД нічого не з'являється; запит без `X-User-Id` → 400; видалення неіснуючого об'єкта → 404.

## 9. Висновки

1. Спроєктовано нормалізовану схему на 20 таблиць з усіма типами зв'язків (1:1, 1:N, M:N) і обмеженнями цілісності на рівні БД.
2. Soft delete реалізовано на двох рівнях: процедурами (`sp_*_SoftDelete`) і тригерами `INSTEAD OF DELETE`, тож навіть звичайний `DELETE` не видаляє дані. Аудит змін заповнюють процедури й тригери.
3. Бізнес-правила (комісія, закриття оголошення, заборона переплати, автозавершення договору, історія цін) виконуються в БД і не залежать від клієнта.
4. Індекси шести видів підібрано під конкретні запити; filtered-індекси поєднуються з soft delete.
5. Repository + Unit of Work дає атомарні операції над кількома сутностями: усі запити з коду йдуть через процедури й View, а кілька процедур виконуються в одній транзакції з повним відкатом при помилці.
