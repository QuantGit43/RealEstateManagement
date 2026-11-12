USE RealEstateDb;
GO

CREATE OR ALTER TRIGGER dbo.trg_Listings_PriceHistory
ON dbo.Listings
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(Price) RETURN;

    INSERT dbo.PriceHistory (ListingId, OldPrice, NewPrice, ChangedBy)
    SELECT i.ListingId, d.Price, i.Price, i.UpdatedBy
    FROM inserted i
    JOIN deleted d ON d.ListingId = i.ListingId
    WHERE i.Price <> d.Price;
END
GO

CREATE OR ALTER TRIGGER dbo.trg_Properties_InsteadOfDelete
ON dbo.Properties
INSTEAD OF DELETE
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId int = CAST(SESSION_CONTEXT(N'UserId') AS int);

    UPDATE p
    SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), DeletedBy = @UserId
    FROM dbo.Properties p
    JOIN deleted d ON d.PropertyId = p.PropertyId
    WHERE p.IsDeleted = 0;

    UPDATE l
    SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), DeletedBy = @UserId
    FROM dbo.Listings l
    JOIN deleted d ON d.PropertyId = l.PropertyId
    WHERE l.IsDeleted = 0;
END
GO

CREATE OR ALTER TRIGGER dbo.trg_Listings_InsteadOfDelete
ON dbo.Listings
INSTEAD OF DELETE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE l
    SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), DeletedBy = CAST(SESSION_CONTEXT(N'UserId') AS int)
    FROM dbo.Listings l
    JOIN deleted d ON d.ListingId = l.ListingId
    WHERE l.IsDeleted = 0;
END
GO

CREATE OR ALTER TRIGGER dbo.trg_Contracts_SetUpdatedAt
ON dbo.Contracts
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE c
    SET UpdatedAt = SYSUTCDATETIME()
    FROM dbo.Contracts c
    JOIN inserted i ON i.ContractId = c.ContractId;
END
GO

CREATE OR ALTER TRIGGER dbo.trg_Payments_CompleteContract
ON dbo.Payments
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE c
    SET Status = N'Completed', UpdatedBy = i.UserId
    FROM dbo.Contracts c
    JOIN (SELECT ContractId, MAX(COALESCE(UpdatedBy, CreatedBy)) AS UserId
          FROM inserted
          WHERE PaidAt IS NOT NULL
          GROUP BY ContractId) i ON i.ContractId = c.ContractId
    WHERE c.Status = N'Active'
      AND c.ContractType = N'Sale'
      AND dbo.fn_GetContractDebt(c.ContractId) <= 0;
END
GO
