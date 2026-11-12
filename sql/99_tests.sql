USE RealEstateDb;
GO
SET NOCOUNT ON;
BEGIN TRAN;

EXEC sp_set_session_context @key = N'UserId', @value = 1;
DELETE FROM dbo.Properties WHERE PropertyId = 6;
IF NOT EXISTS (SELECT 1 FROM dbo.Properties WHERE PropertyId = 6 AND IsDeleted = 1 AND DeletedBy = 1)
    THROW 51000, N'FAIL 1: рядок Properties видалено фізично або без DeletedBy', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Listings WHERE PropertyId = 6 AND IsDeleted = 1)
    THROW 51000, N'FAIL 1: оголошення об''єкта не soft-deleted', 1;
IF EXISTS (SELECT 1 FROM dbo.vw_ActiveProperties WHERE PropertyId = 6)
    THROW 51000, N'FAIL 1: видалений об''єкт видно у vw_ActiveProperties', 1;
PRINT N'PASS 1: soft delete через INSTEAD OF DELETE';

IF (SELECT COUNT(*) FROM dbo.PriceHistory WHERE ListingId = 1 AND ChangedBy = 3) <> 2
    THROW 51000, N'FAIL 2: PriceHistory не заповнена тригером', 1;
PRINT N'PASS 2: trg_Listings_PriceHistory';

EXEC dbo.sp_Property_Update @PropertyId = 2, @OwnerId = 2, @PropertyTypeId = 1, @AddressId = 2,
     @Title = N'Квартира з видом на Дніпро (оновлено)', @AreaSqm = 48.00, @Rooms = 1, @UserId = 2;
IF NOT EXISTS (SELECT 1 FROM dbo.Properties WHERE PropertyId = 2 AND UpdatedBy = 2 AND UpdatedAt IS NOT NULL)
    THROW 51000, N'FAIL 3: UpdatedAt/UpdatedBy не встановлено', 1;
PRINT N'PASS 3: аудит UpdatedAt/UpdatedBy';

IF NOT EXISTS (SELECT 1 FROM dbo.Contracts WHERE ContractId = 1 AND Status = N'Completed' AND UpdatedAt IS NOT NULL)
    THROW 51000, N'FAIL 4: договір 1 не завершено тригером', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Listings WHERE ListingId = 1 AND Status = N'Closed')
    THROW 51000, N'FAIL 4: оголошення 1 не закрите', 1;
IF (SELECT CommissionAmount FROM dbo.Contracts WHERE ContractId = 1) <> 109500.00
    THROW 51000, N'FAIL 4: неправильна комісія', 1;
PRINT N'PASS 4: sp_Contract_Create + trg_Payments_CompleteContract + fn_CalcCommission';

IF dbo.fn_GetContractDebt(3) <> 1700000.00
    THROW 51000, N'FAIL 5: fn_GetContractDebt', 1;
IF (SELECT COUNT(*) FROM dbo.vw_OverduePayments) <> 1
    THROW 51000, N'FAIL 5: vw_OverduePayments', 1;
PRINT N'PASS 5: fn_GetContractDebt + vw_OverduePayments';

BEGIN TRY
    EXEC dbo.sp_Payment_Register @ContractId = 3, @Amount = 2000000.00, @DueDate = '2026-01-01',
         @PaidAt = '2026-01-01', @Method = N'Cash', @UserId = 2;
    THROW 51000, N'FAIL 6: переплату прийнято', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 50400 THROW;
END CATCH
PRINT N'PASS 6: sp_Payment_Register відхиляє переплату';

DECLARE @Rv binary(8) = (SELECT RowVersion FROM dbo.Listings WHERE ListingId = 3);
EXEC dbo.sp_Listing_UpdatePrice @ListingId = 3, @NewPrice = 90000.00, @RowVersion = @Rv, @UserId = 4;
BEGIN TRY
    EXEC dbo.sp_Listing_UpdatePrice @ListingId = 3, @NewPrice = 85000.00, @RowVersion = @Rv, @UserId = 4;
    THROW 51000, N'FAIL 7: застарілий RowVersion прийнято', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 50409 THROW;
END CATCH
PRINT N'PASS 7: rowversion у sp_Listing_UpdatePrice';

IF (SELECT COUNT(*) FROM dbo.fn_PropertiesByCity(1)) <> 4
    THROW 51000, N'FAIL 8: fn_PropertiesByCity', 1;
IF (SELECT COUNT(*) FROM dbo.fn_ListingsByAgent(1)) <> 3
    THROW 51000, N'FAIL 8: fn_ListingsByAgent', 1;
PRINT N'PASS 8: fn_PropertiesByCity + fn_ListingsByAgent';

BEGIN TRY
    EXEC dbo.sp_Property_SoftDelete @PropertyId = 999, @UserId = 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 50404 THROW;
END CATCH
IF @@TRANCOUNT <> 1 OR XACT_STATE() <> 1
    THROW 51000, N'FAIL 9: зовнішня транзакція зламана', 1;
PRINT N'PASS 9: savepoint у sp_Property_SoftDelete';

ROLLBACK;
PRINT N'Усі перевірки пройдено.';
GO

SET STATISTICS IO ON;
SELECT ListingId, PropertyId, Price FROM dbo.Listings
WHERE Status = N'Active' AND Price BETWEEN 10000 AND 100000;
SELECT PropertyId, Title FROM dbo.Properties WHERE OwnerId = 1 AND IsDeleted = 0;
SELECT ContractId, SUM(Amount) AS Paid FROM dbo.Payments
WHERE PaidAt IS NOT NULL GROUP BY ContractId;
SET STATISTICS IO OFF;
GO
