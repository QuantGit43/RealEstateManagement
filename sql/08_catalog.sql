USE RealEstateDb;
GO

CREATE TABLE PropertyAttributes (
    PropertyId int NOT NULL REFERENCES Properties(PropertyId),
    AttrKey    varchar(40) NOT NULL,
    NumValue   decimal(18,2) NULL,
    TextValue  nvarchar(100) NULL,
    CONSTRAINT PK_PropertyAttributes PRIMARY KEY (PropertyId, AttrKey),
    CONSTRAINT CK_PropertyAttributes_Value CHECK (NumValue IS NOT NULL OR TextValue IS NOT NULL)
);
GO

CREATE NONCLUSTERED INDEX IX_PropertyAttributes_Num  ON dbo.PropertyAttributes(AttrKey, NumValue);
CREATE NONCLUSTERED INDEX IX_PropertyAttributes_Text ON dbo.PropertyAttributes(AttrKey, TextValue);
CREATE NONCLUSTERED INDEX IX_Properties_Address_Active
    ON dbo.Properties(AddressId) INCLUDE (PropertyTypeId) WHERE IsDeleted = 0;
CREATE NONCLUSTERED INDEX IX_Listings_Property_Search
    ON dbo.Listings(PropertyId) INCLUDE (ListingType, Status, Price, AgentId) WHERE IsDeleted = 0;
CREATE NONCLUSTERED INDEX IX_Listings_Search
    ON dbo.Listings(ListingType, Status, Price) INCLUDE (PropertyId) WHERE IsDeleted = 0;
CREATE NONCLUSTERED INDEX IX_PropertyAmenities_Amenity ON dbo.PropertyAmenities(AmenityId, PropertyId);
CREATE NONCLUSTERED INDEX IX_PropertyPhotos_Property   ON dbo.PropertyPhotos(PropertyId) INCLUDE (IsMain, Url, SortOrder);
GO

CREATE OR ALTER VIEW dbo.vw_ListingCards
AS
SELECT l.ListingId, l.PropertyId, p.Title, p.Description,
       pt.PropertyTypeId, pt.Name AS PropertyType,
       l.ListingType, l.Price, l.Currency, l.Status, l.ListedAt,
       c.CityId, c.Name AS City, d.DistrictId, d.Name AS District, a.Street, a.Building, a.Apartment,
       p.AreaSqm, p.Rooms, p.Floor, p.YearBuilt,
       o.OwnerId, o.FullName AS OwnerName, o.Phone AS OwnerPhone,
       ag.AgentId, u.FullName AS AgentName, u.Phone AS AgentPhone, ag.LicenseNo
FROM dbo.Listings l
JOIN dbo.Properties p     ON p.PropertyId = l.PropertyId
JOIN dbo.PropertyTypes pt ON pt.PropertyTypeId = p.PropertyTypeId
JOIN dbo.Addresses a      ON a.AddressId = p.AddressId
JOIN dbo.Districts d      ON d.DistrictId = a.DistrictId
JOIN dbo.Cities c         ON c.CityId = d.CityId
JOIN dbo.Owners o         ON o.OwnerId = p.OwnerId
JOIN dbo.Agents ag        ON ag.AgentId = l.AgentId
JOIN dbo.Users u          ON u.UserId = ag.UserId
WHERE l.IsDeleted = 0 AND p.IsDeleted = 0;
GO

CREATE OR ALTER PROCEDURE dbo.sp_ListingCard_Create
    @DistrictId int, @Street nvarchar(150), @Building nvarchar(10), @Apartment nvarchar(10) = NULL,
    @OwnerId int, @PropertyTypeId int, @Title nvarchar(200), @Description nvarchar(max) = NULL,
    @AreaSqm decimal(9,2), @Rooms tinyint, @Floor smallint = NULL, @YearBuilt smallint = NULL,
    @AgentId int, @ListingType nvarchar(10), @Price decimal(14,2), @Currency char(3), @Status nvarchar(10), @ListedAt datetime2,
    @Photos nvarchar(max),
    @Amenities nvarchar(max),
    @PriceHistory nvarchar(max),
    @Attributes nvarchar(max),
    @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @AddressId int, @PropertyId int, @ListingId int;

    BEGIN TRAN;
    INSERT dbo.Addresses (DistrictId, Street, Building, Apartment) VALUES (@DistrictId, @Street, @Building, @Apartment);
    SET @AddressId = SCOPE_IDENTITY();

    INSERT dbo.Properties (OwnerId, PropertyTypeId, AddressId, Title, Description, AreaSqm, Rooms, Floor, YearBuilt, CreatedBy)
    VALUES (@OwnerId, @PropertyTypeId, @AddressId, @Title, @Description, @AreaSqm, @Rooms, @Floor, @YearBuilt, @UserId);
    SET @PropertyId = SCOPE_IDENTITY();

    INSERT dbo.Listings (PropertyId, AgentId, ListingType, Price, Currency, Status, ListedAt, CreatedBy)
    VALUES (@PropertyId, @AgentId, @ListingType, @Price, @Currency, @Status, @ListedAt, @UserId);
    SET @ListingId = SCOPE_IDENTITY();

    INSERT dbo.PropertyPhotos (PropertyId, Url, IsMain, SortOrder)
    SELECT @PropertyId, Url, IsMain, SortOrder
    FROM OPENJSON(@Photos) WITH (Url nvarchar(500), IsMain bit, SortOrder int);

    INSERT dbo.PropertyAmenities (PropertyId, AmenityId)
    SELECT @PropertyId, a.AmenityId
    FROM OPENJSON(@Amenities) j JOIN dbo.Amenities a ON a.Name = j.value;

    INSERT dbo.PriceHistory (ListingId, OldPrice, NewPrice, ChangedAt, ChangedBy)
    SELECT @ListingId, OldPrice, NewPrice, ChangedAt, @UserId
    FROM OPENJSON(@PriceHistory) WITH (OldPrice decimal(14,2), NewPrice decimal(14,2), ChangedAt datetime2);

    INSERT dbo.PropertyAttributes (PropertyId, AttrKey, NumValue, TextValue)
    SELECT @PropertyId, [key],
           CASE type WHEN 2 THEN CAST(value AS decimal(18,2)) WHEN 3 THEN IIF(value = N'true', 1.0, 0.0) END,
           CASE WHEN type = 1 THEN value END
    FROM OPENJSON(@Attributes);
    COMMIT;

    SELECT @ListingId AS ListingId;
END
GO
