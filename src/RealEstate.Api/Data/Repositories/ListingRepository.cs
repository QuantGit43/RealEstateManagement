using System.Data;
using Dapper;
using RealEstate.Api.Models;

namespace RealEstate.Api.Data.Repositories;

public interface IListingRepository
{
    Task<ListingView?> GetByIdAsync(int listingId);
    Task<IEnumerable<ListingView>> GetActiveAsync();
    Task<IEnumerable<ListingView>> GetByAgentAsync(int agentId);
    Task<int> CreateAsync(int propertyId, ListingRequest request, int userId);
    Task UpdatePriceAsync(int listingId, decimal newPrice, byte[]? rowVersion, int userId);
    Task SoftDeleteAsync(int listingId, int userId);
}

internal sealed class ListingRepository(DbSession db) : IListingRepository
{
    public Task<ListingView?> GetByIdAsync(int listingId) =>
        db.Connection.QuerySingleOrDefaultAsync<ListingView>(
            "SELECT * FROM dbo.vw_ActiveListings WHERE ListingId = @listingId",
            new { listingId }, db.Transaction);

    public Task<IEnumerable<ListingView>> GetActiveAsync() =>
        db.Connection.QueryAsync<ListingView>(
            "SELECT * FROM dbo.vw_ActiveListings WHERE Status = N'Active' ORDER BY ListedAt DESC",
            transaction: db.Transaction);

    public Task<IEnumerable<ListingView>> GetByAgentAsync(int agentId) =>
        db.Connection.QueryAsync<ListingView>(
            "SELECT * FROM dbo.fn_ListingsByAgent(@agentId) ORDER BY ListedAt DESC",
            new { agentId }, db.Transaction);

    public Task<int> CreateAsync(int propertyId, ListingRequest r, int userId) =>
        db.Connection.ExecuteScalarAsync<int>("dbo.sp_Listing_Create",
            new { PropertyId = propertyId, r.AgentId, r.ListingType, r.Price, r.Currency, UserId = userId },
            db.Transaction, commandType: CommandType.StoredProcedure);

    public Task UpdatePriceAsync(int listingId, decimal newPrice, byte[]? rowVersion, int userId)
    {
        var p = new DynamicParameters(new { ListingId = listingId, NewPrice = newPrice, UserId = userId });
        p.Add("RowVersion", rowVersion, DbType.Binary, size: 8);
        return db.Connection.ExecuteAsync("dbo.sp_Listing_UpdatePrice", p,
            db.Transaction, commandType: CommandType.StoredProcedure);
    }

    public Task SoftDeleteAsync(int listingId, int userId) =>
        db.Connection.ExecuteAsync("dbo.sp_Listing_SoftDelete",
            new { ListingId = listingId, UserId = userId },
            db.Transaction, commandType: CommandType.StoredProcedure);
}
