using System.Data;
using Dapper;
using RealEstate.Api.Models;

namespace RealEstate.Api.Data.Repositories;

public interface IContractRepository
{
    Task<ContractView?> GetByIdAsync(int contractId);
    Task<int> CreateAsync(int listingId, ContractRequest request, int userId);
}

internal sealed class ContractRepository(DbSession db) : IContractRepository
{
    public Task<ContractView?> GetByIdAsync(int contractId) =>
        db.Connection.QuerySingleOrDefaultAsync<ContractView>(
            "SELECT * FROM dbo.vw_ContractDetails WHERE ContractId = @contractId",
            new { contractId }, db.Transaction);

    public Task<int> CreateAsync(int listingId, ContractRequest r, int userId) =>
        db.Connection.ExecuteScalarAsync<int>("dbo.sp_Contract_Create",
            new
            {
                ListingId = listingId, r.ClientId, r.TotalAmount,
                StartDate = r.StartDate.Date, EndDate = r.EndDate?.Date, UserId = userId
            },
            db.Transaction, commandType: CommandType.StoredProcedure);
}
