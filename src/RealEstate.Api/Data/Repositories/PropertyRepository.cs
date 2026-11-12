using System.Data;
using Dapper;
using RealEstate.Api.Models;

namespace RealEstate.Api.Data.Repositories;

public interface IPropertyRepository
{
    Task<PropertyView?> GetByIdAsync(int propertyId);
    Task<IEnumerable<PropertyView>> GetByCityAsync(int cityId);
    Task<int> CreateAsync(PropertyRequest request, int userId);
    Task UpdateAsync(int propertyId, PropertyRequest request, int userId);
    Task SoftDeleteAsync(int propertyId, int userId);
}

internal sealed class PropertyRepository(DbSession db) : IPropertyRepository
{
    public Task<PropertyView?> GetByIdAsync(int propertyId) =>
        db.Connection.QuerySingleOrDefaultAsync<PropertyView>(
            "SELECT * FROM dbo.vw_ActiveProperties WHERE PropertyId = @propertyId",
            new { propertyId }, db.Transaction);

    public Task<IEnumerable<PropertyView>> GetByCityAsync(int cityId) =>
        db.Connection.QueryAsync<PropertyView>(
            "SELECT * FROM dbo.fn_PropertiesByCity(@cityId) ORDER BY PropertyId",
            new { cityId }, db.Transaction);

    public Task<int> CreateAsync(PropertyRequest r, int userId) =>
        db.Connection.ExecuteScalarAsync<int>("dbo.sp_Property_Create",
            new
            {
                r.OwnerId, r.PropertyTypeId, r.AddressId, r.Title, r.Description,
                r.AreaSqm, r.Rooms, r.Floor, r.YearBuilt, UserId = userId
            },
            db.Transaction, commandType: CommandType.StoredProcedure);

    public Task UpdateAsync(int propertyId, PropertyRequest r, int userId) =>
        db.Connection.ExecuteAsync("dbo.sp_Property_Update",
            new
            {
                PropertyId = propertyId, r.OwnerId, r.PropertyTypeId, r.AddressId, r.Title, r.Description,
                r.AreaSqm, r.Rooms, r.Floor, r.YearBuilt, UserId = userId
            },
            db.Transaction, commandType: CommandType.StoredProcedure);

    public Task SoftDeleteAsync(int propertyId, int userId) =>
        db.Connection.ExecuteAsync("dbo.sp_Property_SoftDelete",
            new { PropertyId = propertyId, UserId = userId },
            db.Transaction, commandType: CommandType.StoredProcedure);
}
