USE RealEstateDb;
GO

CREATE OR ALTER VIEW dbo.vw_ActiveProperties
AS
SELECT p.PropertyId, p.Title, p.Description,
       p.PropertyTypeId, pt.Name AS PropertyType,
       p.OwnerId, o.FullName AS OwnerName,
       p.AddressId, c.CityId, c.Name AS City, d.Name AS District,
       a.Street, a.Building, a.Apartment,
       p.AreaSqm, p.Rooms, p.Floor, p.YearBuilt,
       p.CreatedAt, p.CreatedBy, p.UpdatedAt, p.UpdatedBy
FROM dbo.Properties p
JOIN dbo.PropertyTypes pt ON pt.PropertyTypeId = p.PropertyTypeId
JOIN dbo.Owners o         ON o.OwnerId = p.OwnerId
JOIN dbo.Addresses a      ON a.AddressId = p.AddressId
JOIN dbo.Districts d      ON d.DistrictId = a.DistrictId
JOIN dbo.Cities c         ON c.CityId = d.CityId
WHERE p.IsDeleted = 0;
GO

CREATE OR ALTER VIEW dbo.vw_ActiveListings
AS
SELECT l.ListingId, l.PropertyId, p.Title AS PropertyTitle, p.City, p.District,
       l.AgentId, u.FullName AS AgentName,
       l.ListingType, l.Price, l.Currency, l.Status, l.ListedAt, l.RowVersion,
       l.CreatedAt, l.CreatedBy, l.UpdatedAt, l.UpdatedBy
FROM dbo.Listings l
JOIN dbo.vw_ActiveProperties p ON p.PropertyId = l.PropertyId
JOIN dbo.Agents a              ON a.AgentId = l.AgentId
JOIN dbo.Users u               ON u.UserId = a.UserId
WHERE l.IsDeleted = 0;
GO

CREATE OR ALTER VIEW dbo.vw_ContractDetails
AS
SELECT c.ContractId, c.ListingId, l.PropertyId, p.Title AS PropertyTitle,
       c.ClientId, cl.FullName AS ClientName,
       c.AgentId, u.FullName AS AgentName,
       c.ContractType, c.TotalAmount, c.CommissionAmount,
       ISNULL(paid.PaidAmount, 0) AS PaidAmount,
       dbo.fn_GetContractDebt(c.ContractId) AS Debt,
       c.StartDate, c.EndDate, c.Status, c.SignedAt,
       c.CreatedAt, c.CreatedBy, c.UpdatedAt, c.UpdatedBy
FROM dbo.Contracts c
JOIN dbo.Listings l   ON l.ListingId = c.ListingId
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
JOIN dbo.Clients cl   ON cl.ClientId = c.ClientId
JOIN dbo.Agents a     ON a.AgentId = c.AgentId
JOIN dbo.Users u      ON u.UserId = a.UserId
OUTER APPLY (SELECT SUM(pm.Amount) AS PaidAmount
             FROM dbo.Payments pm
             WHERE pm.ContractId = c.ContractId AND pm.PaidAt IS NOT NULL) paid;
GO

CREATE OR ALTER VIEW dbo.vw_PaymentsByContract
AS
SELECT pm.PaymentId, pm.ContractId, c.ClientId, cl.FullName AS ClientName,
       pm.Amount, pm.DueDate, pm.PaidAt, pm.Method,
       CAST(CASE WHEN pm.PaidAt IS NULL THEN 0 ELSE 1 END AS bit) AS IsPaid,
       pm.CreatedAt, pm.CreatedBy, pm.UpdatedAt, pm.UpdatedBy
FROM dbo.Payments pm
JOIN dbo.Contracts c ON c.ContractId = pm.ContractId
JOIN dbo.Clients cl  ON cl.ClientId = c.ClientId;
GO

CREATE OR ALTER VIEW dbo.vw_OverduePayments
AS
SELECT pm.PaymentId, pm.ContractId, cl.FullName AS ClientName, cl.Phone AS ClientPhone,
       pm.Amount, pm.DueDate,
       DATEDIFF(day, pm.DueDate, CAST(SYSUTCDATETIME() AS date)) AS DaysOverdue
FROM dbo.Payments pm
JOIN dbo.Contracts c ON c.ContractId = pm.ContractId
JOIN dbo.Clients cl  ON cl.ClientId = c.ClientId
WHERE pm.PaidAt IS NULL
  AND pm.DueDate < CAST(SYSUTCDATETIME() AS date)
  AND c.Status = N'Active';
GO

CREATE OR ALTER VIEW dbo.vw_AgentPerformance
AS
SELECT a.AgentId, u.FullName AS AgentName, a.CommissionRate,
       ISNULL(ls.ActiveListings, 0) AS ActiveListings,
       ISNULL(ct.ContractsCount, 0) AS ContractsCount,
       ISNULL(ct.TotalDealAmount, 0) AS TotalDealAmount,
       ISNULL(ct.TotalCommission, 0) AS TotalCommission,
       rv.AvgRating,
       ISNULL(rv.ReviewsCount, 0) AS ReviewsCount
FROM dbo.Agents a
JOIN dbo.Users u ON u.UserId = a.UserId
OUTER APPLY (SELECT COUNT(*) AS ActiveListings
             FROM dbo.Listings l
             WHERE l.AgentId = a.AgentId AND l.IsDeleted = 0 AND l.Status = N'Active') ls
OUTER APPLY (SELECT COUNT(*) AS ContractsCount, SUM(c.TotalAmount) AS TotalDealAmount,
                    SUM(c.CommissionAmount) AS TotalCommission
             FROM dbo.Contracts c
             WHERE c.AgentId = a.AgentId AND c.Status <> N'Terminated') ct
OUTER APPLY (SELECT CAST(AVG(CAST(r.Rating AS decimal(3,2))) AS decimal(3,2)) AS AvgRating,
                    COUNT(*) AS ReviewsCount
             FROM dbo.Reviews r
             WHERE r.AgentId = a.AgentId) rv
WHERE a.IsDeleted = 0;
GO

CREATE OR ALTER FUNCTION dbo.fn_PropertiesByCity (@CityId int)
RETURNS TABLE
AS
RETURN SELECT * FROM dbo.vw_ActiveProperties WHERE CityId = @CityId;
GO

CREATE OR ALTER FUNCTION dbo.fn_ListingsByAgent (@AgentId int)
RETURNS TABLE
AS
RETURN SELECT * FROM dbo.vw_ActiveListings WHERE AgentId = @AgentId;
GO
