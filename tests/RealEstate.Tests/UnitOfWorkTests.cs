using Microsoft.Data.SqlClient;
using RealEstate.Api.Data;
using RealEstate.Api.Models;

namespace RealEstate.Tests;

public sealed class UnitOfWorkTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("REALESTATE_DB")
        ?? @"Server=(localdb)\MSSQLLocalDB;Database=RealEstateDb;Trusted_Connection=True;TrustServerCertificate=True";

    private const int UserId = 3;
    private const int AgentId = 1;
    private const int ClientId = 1;

    private static PropertyRequest NewProperty() =>
        new(OwnerId: 1, PropertyTypeId: 1, AddressId: 1, Title: $"Тест {Guid.NewGuid():N}",
            Description: null, AreaSqm: 50m, Rooms: 2, Floor: 3, YearBuilt: 2020);

    [Fact]
    public async Task Deal_Commit_SavesAllEntities()
    {
        int propertyId, listingId, contractId;
        using (var uow = new UnitOfWork(ConnectionString))
        {
            uow.BeginTransaction();
            propertyId = await uow.Properties.CreateAsync(NewProperty(), UserId);
            listingId = await uow.Listings.CreateAsync(propertyId, new ListingRequest(AgentId, "Sale", 1_000_000m), UserId);
            contractId = await uow.Contracts.CreateAsync(listingId,
                new ContractRequest(ClientId, 1_000_000m, DateTime.Today, null), UserId);
            await uow.Payments.RegisterAsync(contractId,
                new PaymentRequest(1_000_000m, DateTime.Today, DateTime.UtcNow, "Transfer"), UserId);
            uow.Commit();
        }

        using var check = new UnitOfWork(ConnectionString);
        Assert.NotNull(await check.Properties.GetByIdAsync(propertyId));
        Assert.Equal("Closed", (await check.Listings.GetByIdAsync(listingId))!.Status);
        var contract = await check.Contracts.GetByIdAsync(contractId);
        Assert.NotNull(contract);
        Assert.Equal("Completed", contract.Status);
        Assert.Equal(0m, contract.Debt);
        Assert.Equal(30_000m, contract.CommissionAmount);
        Assert.Single(await check.Payments.GetByContractAsync(contractId));
    }

    [Fact]
    public async Task Deal_FailsOnPayment_RollsBackEverything()
    {
        int propertyId, listingId, contractId;
        using (var uow = new UnitOfWork(ConnectionString))
        {
            uow.BeginTransaction();
            propertyId = await uow.Properties.CreateAsync(NewProperty(), UserId);
            listingId = await uow.Listings.CreateAsync(propertyId, new ListingRequest(AgentId, "Sale", 500_000m), UserId);
            contractId = await uow.Contracts.CreateAsync(listingId,
                new ContractRequest(ClientId, 500_000m, DateTime.Today, null), UserId);

            var ex = await Assert.ThrowsAsync<SqlException>(() => uow.Payments.RegisterAsync(contractId,
                new PaymentRequest(900_000m, DateTime.Today, DateTime.UtcNow, "Cash"), UserId));
            Assert.Equal(50400, ex.Number);
            uow.Rollback();
        }

        using var check = new UnitOfWork(ConnectionString);
        Assert.Null(await check.Properties.GetByIdAsync(propertyId));
        Assert.Null(await check.Listings.GetByIdAsync(listingId));
        Assert.Null(await check.Contracts.GetByIdAsync(contractId));
    }

    [Fact]
    public async Task SoftDelete_HidesPropertyAndItsListings()
    {
        using var uow = new UnitOfWork(ConnectionString);
        uow.BeginTransaction();
        var propertyId = await uow.Properties.CreateAsync(NewProperty(), UserId);
        var listingId = await uow.Listings.CreateAsync(propertyId, new ListingRequest(AgentId, "Rent", 20_000m), UserId);

        await uow.Properties.SoftDeleteAsync(propertyId, UserId);

        Assert.Null(await uow.Properties.GetByIdAsync(propertyId));
        Assert.Null(await uow.Listings.GetByIdAsync(listingId));
        uow.Rollback();
    }

    [Fact]
    public async Task UpdatePrice_WithStaleRowVersion_Throws409()
    {
        using var uow = new UnitOfWork(ConnectionString);
        uow.BeginTransaction();
        var propertyId = await uow.Properties.CreateAsync(NewProperty(), UserId);
        var listingId = await uow.Listings.CreateAsync(propertyId, new ListingRequest(AgentId, "Rent", 20_000m), UserId);
        var stale = (await uow.Listings.GetByIdAsync(listingId))!.RowVersion;

        await uow.Listings.UpdatePriceAsync(listingId, 19_000m, stale, UserId);
        var ex = await Assert.ThrowsAsync<SqlException>(() => uow.Listings.UpdatePriceAsync(listingId, 18_000m, stale, UserId));

        Assert.Equal(50409, ex.Number);
        Assert.Equal(19_000m, (await uow.Listings.GetByIdAsync(listingId))!.Price);
        uow.Rollback();
    }
}
