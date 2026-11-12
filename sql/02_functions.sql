USE RealEstateDb;
GO

CREATE OR ALTER FUNCTION dbo.fn_CalcCommission (@Amount decimal(14,2), @Rate decimal(5,4))
RETURNS decimal(14,2)
AS
BEGIN
    RETURN ROUND(@Amount * @Rate, 2);
END
GO

CREATE OR ALTER FUNCTION dbo.fn_GetContractDebt (@ContractId int)
RETURNS decimal(14,2)
AS
BEGIN
    DECLARE @Debt decimal(14,2);

    SELECT @Debt = c.TotalAmount - ISNULL((SELECT SUM(p.Amount)
                                           FROM dbo.Payments p
                                           WHERE p.ContractId = c.ContractId AND p.PaidAt IS NOT NULL), 0)
    FROM dbo.Contracts c
    WHERE c.ContractId = @ContractId;

    RETURN @Debt;
END
GO
