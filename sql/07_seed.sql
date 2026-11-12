USE RealEstateDb;
GO
SET NOCOUNT ON;

INSERT dbo.Roles (Name) VALUES (N'Admin'), (N'Manager'), (N'Agent');

INSERT dbo.Users (RoleId, Email, FullName, Phone) VALUES
 (1, N'admin@realestate.example',   N'Адміністратор Системи', N'+380500000001'),
 (2, N'manager@realestate.example', N'Олена Коваль',          N'+380500000002'),
 (3, N'agent1@realestate.example',  N'Андрій Мельник',        N'+380500000003'),
 (3, N'agent2@realestate.example',  N'Ірина Шевчук',          N'+380500000004'),
 (3, N'agent3@realestate.example',  N'Тарас Бондар',          N'+380500000005');

INSERT dbo.Agents (UserId, LicenseNo, CommissionRate, HireDate) VALUES
 (3, N'LIC-2021-001', 0.0300, '2021-03-01'),
 (4, N'LIC-2022-014', 0.0250, '2022-06-15'),
 (5, N'LIC-2024-027', 0.0400, '2024-01-10');

INSERT dbo.Owners (FullName, Phone, Email) VALUES
 (N'Петро Іваненко',   N'+380671110001', N'ivanenko@mail.example'),
 (N'Марія Гончаренко', N'+380671110002', NULL),
 (N'ТОВ "Буд-Інвест"', N'+380441110003', N'office@bud-invest.example'),
 (N'Сергій Лисенко',   N'+380671110004', N'lysenko@mail.example');

INSERT dbo.Clients (FullName, Phone, Email, ClientType, CreatedBy) VALUES
 (N'Оксана Ткаченко',  N'+380932220001', N'tkachenko@mail.example', N'Buyer',  2),
 (N'Дмитро Савченко',  N'+380932220002', NULL,                      N'Tenant', 2),
 (N'Наталія Кравець',  N'+380932220003', N'kravets@mail.example',   N'Buyer',  3),
 (N'Віктор Поліщук',   N'+380932220004', NULL,                      N'Tenant', 4),
 (N'Юлія Мороз',       N'+380932220005', N'moroz@mail.example',     N'Buyer',  5);

INSERT dbo.Cities (Name, Region) VALUES
 (N'Київ', N'Київська'), (N'Львів', N'Львівська'), (N'Одеса', N'Одеська');

INSERT dbo.Districts (CityId, Name) VALUES
 (1, N'Печерський'), (1, N'Оболонський'), (2, N'Галицький'), (2, N'Сихівський'), (3, N'Приморський'), (3, N'Київський');

INSERT dbo.Addresses (DistrictId, Street, Building, Apartment, PostalCode) VALUES
 (1, N'вул. Лютеранська',     N'12',  N'7',  '01001'),
 (2, N'просп. Героїв Сталінграду', N'4', N'115', '04210'),
 (2, N'вул. Тимошенка',       N'21',  NULL,  '04212'),
 (3, N'пл. Ринок',            N'5',   N'3',  '79008'),
 (4, N'просп. Червоної Калини', N'60', N'42', '79066'),
 (5, N'Французький бульвар',  N'33',  N'9',  '65044'),
 (6, N'вул. Академіка Корольова', N'18', NULL, '65114'),
 (1, N'вул. Хрещатик',        N'25',  NULL,  '01001');

INSERT dbo.PropertyTypes (Name) VALUES (N'Квартира'), (N'Будинок'), (N'Офіс'), (N'Комерційне приміщення');

INSERT dbo.Properties (OwnerId, PropertyTypeId, AddressId, Title, Description, AreaSqm, Rooms, Floor, YearBuilt, CreatedBy) VALUES
 (1, 1, 1, N'2-кімнатна квартира в центрі',     N'Свіжий ремонт, панорамні вікна', 64.50, 2, 4,    2015, 2),
 (2, 1, 2, N'Квартира з видом на Дніпро',       NULL,                               48.00, 1, 16,   2010, 2),
 (3, 3, 3, N'Офіс 120 м² на Оболоні',           N'Open space, 2 переговорні',      120.00, 4, 1,    2018, 2),
 (1, 1, 4, N'Апартаменти на площі Ринок',       N'Історичний будинок',              75.20, 3, 2,    1890, 3),
 (4, 1, 5, N'3-кімнатна на Сихові',             NULL,                               82.00, 3, 9,    1987, 3),
 (2, 1, 6, N'Квартира біля моря',               N'5 хвилин до пляжу',               55.00, 2, 3,    2005, 4),
 (4, 2, 7, N'Будинок з ділянкою',               N'6 соток, гараж',                 160.00, 5, NULL, 2012, 4),
 (3, 4, 8, N'Торгове приміщення на Хрещатику',  N'Перша лінія',                    210.00, 2, 1,    1954, 2);

INSERT dbo.Amenities (Name) VALUES
 (N'Паркінг'), (N'Ліфт'), (N'Балкон'), (N'Кондиціонер'), (N'Охорона'), (N'Меблі');

INSERT dbo.PropertyAmenities (PropertyId, AmenityId) VALUES
 (1,2),(1,3),(1,4),(1,6),(2,2),(2,3),(3,1),(3,4),(3,5),(4,6),(5,2),(5,3),(6,3),(6,4),(7,1),(8,5);

INSERT dbo.PropertyPhotos (PropertyId, Url, IsMain, SortOrder) VALUES
 (1, N'https://cdn.example/photos/1-main.jpg', 1, 0),
 (1, N'https://cdn.example/photos/1-kitchen.jpg', 0, 1),
 (2, N'https://cdn.example/photos/2-main.jpg', 1, 0),
 (3, N'https://cdn.example/photos/3-main.jpg', 1, 0),
 (4, N'https://cdn.example/photos/4-main.jpg', 1, 0),
 (6, N'https://cdn.example/photos/6-main.jpg', 1, 0),
 (7, N'https://cdn.example/photos/7-main.jpg', 1, 0),
 (8, N'https://cdn.example/photos/8-main.jpg', 1, 0);

INSERT dbo.Listings (PropertyId, AgentId, ListingType, Price, Currency, CreatedBy) VALUES
 (1, 1, N'Sale',  3900000.00, 'UAH', 3),
 (2, 1, N'Rent',    18000.00, 'UAH', 3),
 (3, 2, N'Rent',    95000.00, 'UAH', 4),
 (4, 2, N'Sale',   145000.00, 'USD', 4),
 (5, 3, N'Sale',  2700000.00, 'UAH', 5),
 (6, 3, N'Rent',    15000.00, 'UAH', 5),
 (7, 1, N'Sale',   180000.00, 'USD', 3),
 (8, 2, N'Rent',   250000.00, 'UAH', 4);

EXEC dbo.sp_Listing_UpdatePrice @ListingId = 1, @NewPrice = 3750000.00, @UserId = 3;
EXEC dbo.sp_Listing_UpdatePrice @ListingId = 1, @NewPrice = 3650000.00, @UserId = 3;
EXEC dbo.sp_Listing_UpdatePrice @ListingId = 4, @NewPrice = 139000.00,  @UserId = 4;

INSERT dbo.Viewings (ListingId, ClientId, AgentId, ScheduledAt, Status, Feedback) VALUES
 (1, 1, 1, DATEADD(day, -20, SYSUTCDATETIME()), N'Done',      N'Сподобалось, торгується'),
 (1, 3, 1, DATEADD(day, -18, SYSUTCDATETIME()), N'Done',      NULL),
 (2, 2, 1, DATEADD(day, -15, SYSUTCDATETIME()), N'Done',      N'Готовий підписати'),
 (5, 5, 3, DATEADD(day, -10, SYSUTCDATETIME()), N'Done',      NULL),
 (4, 3, 2, DATEADD(day,   3, SYSUTCDATETIME()), N'Scheduled', NULL),
 (6, 4, 3, DATEADD(day,  -2, SYSUTCDATETIME()), N'Cancelled', N'Клієнт не прийшов');

DECLARE @Today date = CAST(SYSUTCDATETIME() AS date);
DECLARE @LeaseStart date = DATEADD(month, -3, @Today), @LeaseEnd date = DATEADD(month, 9, @Today);
EXEC dbo.sp_Contract_Create @ListingId = 1, @ClientId = 1, @TotalAmount = 3650000.00,
     @StartDate = @Today, @UserId = 3;
EXEC dbo.sp_Contract_Create @ListingId = 2, @ClientId = 2, @TotalAmount = 216000.00,
     @StartDate = @LeaseStart, @EndDate = @LeaseEnd, @UserId = 3;
EXEC dbo.sp_Contract_Create @ListingId = 5, @ClientId = 5, @TotalAmount = 2700000.00,
     @StartDate = @Today, @UserId = 5;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @D1 date = DATEADD(month, -3, @Today), @D2 date = DATEADD(month, -2, @Today),
        @D3 date = DATEADD(month, -1, @Today), @D4 date = DATEADD(month,  1, @Today);
EXEC dbo.sp_Payment_Register @ContractId = 1, @Amount = 3650000.00, @DueDate = @Today, @PaidAt = @Now, @Method = N'Transfer', @UserId = 2;
EXEC dbo.sp_Payment_Register @ContractId = 2, @Amount = 18000.00, @DueDate = @D1, @PaidAt = @Now, @Method = N'Card',     @UserId = 2;
EXEC dbo.sp_Payment_Register @ContractId = 2, @Amount = 18000.00, @DueDate = @D2, @PaidAt = @Now, @Method = N'Card',     @UserId = 2;
EXEC dbo.sp_Payment_Register @ContractId = 2, @Amount = 18000.00, @DueDate = @D3, @Method = N'Card', @UserId = 2;
EXEC dbo.sp_Payment_Register @ContractId = 2, @Amount = 18000.00, @DueDate = @D4, @Method = N'Card', @UserId = 2;
EXEC dbo.sp_Payment_Register @ContractId = 3, @Amount = 1000000.00, @DueDate = @Today, @PaidAt = @Now, @Method = N'Transfer', @UserId = 2;

INSERT dbo.MaintenanceRequests (PropertyId, ClientId, Description, Priority, Status, ClosedAt) VALUES
 (2, 2,    N'Протікає змішувач на кухні',       N'Medium', N'Done',       SYSUTCDATETIME()),
 (2, 2,    N'Не працює домофон',                N'Low',    N'Open',       NULL),
 (3, NULL, N'Перевірка пожежної сигналізації',  N'High',   N'InProgress', NULL);

INSERT dbo.Reviews (ClientId, AgentId, Rating, Comment) VALUES
 (1, 1, 5, N'Швидко знайшли квартиру'),
 (2, 1, 4, NULL),
 (5, 3, 5, N'Дуже уважний агент'),
 (3, 2, 3, N'Довго чекала відповіді');
GO
