USE RealEstateDb;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Property_Create
    @OwnerId int, @PropertyTypeId int, @AddressId int,
    @Title nvarchar(200), @Description nvarchar(max) = NULL,
    @AreaSqm decimal(9,2), @Rooms tinyint, @Floor smallint = NULL, @YearBuilt smallint = NULL,
    @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Owners WHERE OwnerId = @OwnerId AND IsDeleted = 0)
        THROW 50404, N'Власника не знайдено.', 1;

    INSERT dbo.Properties (OwnerId, PropertyTypeId, AddressId, Title, Description,
                           AreaSqm, Rooms, Floor, YearBuilt, CreatedBy)
    VALUES (@OwnerId, @PropertyTypeId, @AddressId, @Title, @Description,
            @AreaSqm, @Rooms, @Floor, @YearBuilt, @UserId);

    SELECT CAST(SCOPE_IDENTITY() AS int) AS PropertyId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Property_Update
    @PropertyId int, @OwnerId int, @PropertyTypeId int, @AddressId int,
    @Title nvarchar(200), @Description nvarchar(max) = NULL,
    @AreaSqm decimal(9,2), @Rooms tinyint, @Floor smallint = NULL, @YearBuilt smallint = NULL,
    @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Owners WHERE OwnerId = @OwnerId AND IsDeleted = 0)
        THROW 50404, N'Власника не знайдено.', 1;

    UPDATE dbo.Properties
    SET OwnerId = @OwnerId, PropertyTypeId = @PropertyTypeId, AddressId = @AddressId, Title = @Title, Description = @Description,
        AreaSqm = @AreaSqm, Rooms = @Rooms, Floor = @Floor, YearBuilt = @YearBuilt,
        UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
    WHERE PropertyId = @PropertyId AND IsDeleted = 0;

    IF @@ROWCOUNT = 0
        THROW 50404, N'Об''єкт не знайдено.', 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Property_SoftDelete
    @PropertyId int, @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.Contracts c JOIN dbo.Listings l ON l.ListingId = c.ListingId
               WHERE l.PropertyId = @PropertyId AND c.Status = N'Active')
        THROW 50409, N'Об''єкт має активний договір.', 1;

    DECLARE @OuterTran int = @@TRANCOUNT;
    BEGIN TRY
        IF @OuterTran = 0 BEGIN TRAN; ELSE SAVE TRAN sp_Property_SoftDelete;

        UPDATE dbo.Properties
        SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), DeletedBy = @UserId
        WHERE PropertyId = @PropertyId AND IsDeleted = 0;

        IF @@ROWCOUNT = 0
            THROW 50404, N'Об''єкт не знайдено.', 1;

        UPDATE dbo.Listings
        SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), DeletedBy = @UserId
        WHERE PropertyId = @PropertyId AND IsDeleted = 0;

        IF @OuterTran = 0 COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() = -1 OR (XACT_STATE() = 1 AND @OuterTran = 0) ROLLBACK;
        ELSE IF XACT_STATE() = 1 ROLLBACK TRAN sp_Property_SoftDelete;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Listing_Create
    @PropertyId int, @AgentId int, @ListingType nvarchar(10), @Price decimal(14,2),
    @Currency char(3) = 'UAH', @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Properties WHERE PropertyId = @PropertyId AND IsDeleted = 0)
        THROW 50404, N'Об''єкт не знайдено.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Agents WHERE AgentId = @AgentId AND IsDeleted = 0)
        THROW 50404, N'Агента не знайдено.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Listings
               WHERE PropertyId = @PropertyId AND ListingType = @ListingType
                 AND Status = N'Active' AND IsDeleted = 0)
        THROW 50409, N'Для об''єкта вже є активне оголошення цього типу.', 1;

    INSERT dbo.Listings (PropertyId, AgentId, ListingType, Price, Currency, CreatedBy)
    VALUES (@PropertyId, @AgentId, @ListingType, @Price, @Currency, @UserId);

    SELECT CAST(SCOPE_IDENTITY() AS int) AS ListingId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Listing_UpdatePrice
    @ListingId int, @NewPrice decimal(14,2), @RowVersion binary(8) = NULL, @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Listings
    SET Price = @NewPrice, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
    WHERE ListingId = @ListingId AND IsDeleted = 0 AND Status = N'Active'
      AND (@RowVersion IS NULL OR RowVersion = @RowVersion);

    IF @@ROWCOUNT = 0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.Listings WHERE ListingId = @ListingId AND IsDeleted = 0)
            THROW 50404, N'Оголошення не знайдено.', 1;
        THROW 50409, N'Оголошення закрите або змінене іншим користувачем.', 1;
    END
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Listing_SoftDelete
    @ListingId int, @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Listings
    SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), DeletedBy = @UserId
    WHERE ListingId = @ListingId AND IsDeleted = 0;

    IF @@ROWCOUNT = 0
        THROW 50404, N'Оголошення не знайдено.', 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Client_Create
    @FullName nvarchar(150), @Phone nvarchar(20), @Email nvarchar(255) = NULL,
    @ClientType nvarchar(10), @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.Clients (FullName, Phone, Email, ClientType, CreatedBy)
    VALUES (@FullName, @Phone, @Email, @ClientType, @UserId);

    SELECT CAST(SCOPE_IDENTITY() AS int) AS ClientId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Client_SoftDelete
    @ClientId int, @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Clients
    SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), DeletedBy = @UserId
    WHERE ClientId = @ClientId AND IsDeleted = 0;

    IF @@ROWCOUNT = 0
        THROW 50404, N'Клієнта не знайдено.', 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Contract_Create
    @ListingId int, @ClientId int, @TotalAmount decimal(14,2),
    @StartDate date, @EndDate date = NULL, @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @AgentId int, @ListingType nvarchar(10), @Rate decimal(5,4), @ContractId int;

    SELECT @AgentId = l.AgentId, @ListingType = l.ListingType, @Rate = a.CommissionRate
    FROM dbo.Listings l
    JOIN dbo.Agents a ON a.AgentId = l.AgentId
    WHERE l.ListingId = @ListingId AND l.IsDeleted = 0 AND l.Status = N'Active';

    IF @AgentId IS NULL
        THROW 50409, N'Оголошення не знайдено або вже не активне.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Clients WHERE ClientId = @ClientId AND IsDeleted = 0)
        THROW 50404, N'Клієнта не знайдено.', 1;
    IF @ListingType = N'Rent' AND @EndDate IS NULL
        THROW 50400, N'Для договору оренди потрібна дата завершення.', 1;

    DECLARE @OuterTran int = @@TRANCOUNT;
    BEGIN TRY
        IF @OuterTran = 0 BEGIN TRAN; ELSE SAVE TRAN sp_Contract_Create;

        INSERT dbo.Contracts (ListingId, ClientId, AgentId, ContractType, TotalAmount, CommissionAmount,
                              StartDate, EndDate, CreatedBy)
        VALUES (@ListingId, @ClientId, @AgentId,
                CASE @ListingType WHEN N'Sale' THEN N'Sale' ELSE N'Lease' END,
                @TotalAmount, dbo.fn_CalcCommission(@TotalAmount, @Rate),
                @StartDate, @EndDate, @UserId);
        SET @ContractId = SCOPE_IDENTITY();

        UPDATE dbo.Listings
        SET Status = N'Closed', UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
        WHERE ListingId = @ListingId;

        IF @OuterTran = 0 COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() = -1 OR (XACT_STATE() = 1 AND @OuterTran = 0) ROLLBACK;
        ELSE IF XACT_STATE() = 1 ROLLBACK TRAN sp_Contract_Create;
        THROW;
    END CATCH

    SELECT @ContractId AS ContractId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Payment_Register
    @ContractId int, @Amount decimal(14,2), @DueDate date, @PaidAt datetime2 = NULL,
    @Method nvarchar(10), @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Contracts WHERE ContractId = @ContractId AND Status = N'Active')
        THROW 50409, N'Договір не знайдено або він не активний.', 1;
    IF @PaidAt IS NOT NULL AND @Amount > dbo.fn_GetContractDebt(@ContractId)
        THROW 50400, N'Сума платежу перевищує борг за договором.', 1;

    INSERT dbo.Payments (ContractId, Amount, DueDate, PaidAt, Method, CreatedBy)
    VALUES (@ContractId, @Amount, @DueDate, @PaidAt, @Method, @UserId);

    SELECT CAST(SCOPE_IDENTITY() AS int) AS PaymentId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Payment_MarkPaid
    @PaymentId int, @PaidAt datetime2 = NULL, @UserId int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ContractId int, @Amount decimal(14,2);
    SELECT @ContractId = ContractId, @Amount = Amount FROM dbo.Payments WHERE PaymentId = @PaymentId AND PaidAt IS NULL;

    IF @ContractId IS NULL
        THROW 50404, N'Неоплачений платіж не знайдено.', 1;
    IF @Amount > dbo.fn_GetContractDebt(@ContractId)
        THROW 50400, N'Сума платежу перевищує борг за договором.', 1;

    UPDATE dbo.Payments
    SET PaidAt = ISNULL(@PaidAt, SYSUTCDATETIME()), UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UserId
    WHERE PaymentId = @PaymentId;
END
GO
