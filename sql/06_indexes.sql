USE RealEstateDb;
GO

CREATE UNIQUE NONCLUSTERED INDEX UX_Users_Email     ON dbo.Users(Email);
CREATE UNIQUE NONCLUSTERED INDEX UX_Agents_LicenseNo ON dbo.Agents(LicenseNo);

CREATE UNIQUE NONCLUSTERED INDEX UX_Clients_Phone_Active ON dbo.Clients(Phone) WHERE IsDeleted = 0;

CREATE NONCLUSTERED INDEX IX_Properties_Owner_Active
    ON dbo.Properties(OwnerId) INCLUDE (Title, PropertyTypeId) WHERE IsDeleted = 0;
CREATE NONCLUSTERED INDEX IX_Listings_Property_Active
    ON dbo.Listings(PropertyId, ListingType) INCLUDE (Status) WHERE IsDeleted = 0;

CREATE NONCLUSTERED INDEX IX_Listings_Status_Price
    ON dbo.Listings(Status, Price) INCLUDE (PropertyId, AgentId, ListingType);

CREATE NONCLUSTERED INDEX IX_Districts_City      ON dbo.Districts(CityId);
CREATE NONCLUSTERED INDEX IX_Addresses_District  ON dbo.Addresses(DistrictId);
CREATE NONCLUSTERED INDEX IX_Listings_Agent      ON dbo.Listings(AgentId);
CREATE NONCLUSTERED INDEX IX_Contracts_Client    ON dbo.Contracts(ClientId);
CREATE NONCLUSTERED INDEX IX_Contracts_Agent     ON dbo.Contracts(AgentId);
CREATE NONCLUSTERED INDEX IX_Payments_Contract   ON dbo.Payments(ContractId) INCLUDE (Amount, PaidAt);
CREATE NONCLUSTERED INDEX IX_Viewings_Listing    ON dbo.Viewings(ListingId);
CREATE NONCLUSTERED INDEX IX_PriceHistory_Listing ON dbo.PriceHistory(ListingId);

CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_Payments
    ON dbo.Payments(ContractId, Amount, DueDate, PaidAt);
GO
