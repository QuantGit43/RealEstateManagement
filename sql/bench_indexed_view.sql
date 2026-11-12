USE RealEstateDb;
GO
CREATE OR ALTER VIEW dbo.vw_ListingSearch
WITH SCHEMABINDING
AS
SELECT l.ListingId, l.PropertyId, d.CityId, p.PropertyTypeId, l.ListingType, l.Status, l.Price
FROM dbo.Listings l
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
JOIN dbo.Addresses a  ON a.AddressId = p.AddressId
JOIN dbo.Districts d  ON d.DistrictId = a.DistrictId
WHERE l.IsDeleted = 0 AND p.IsDeleted = 0;
GO
CREATE UNIQUE CLUSTERED INDEX CX_ListingSearch ON dbo.vw_ListingSearch(ListingId);
CREATE NONCLUSTERED INDEX IX_ListingSearch_Search
    ON dbo.vw_ListingSearch(CityId, PropertyTypeId, ListingType, Status, Price, ListingId);
GO
