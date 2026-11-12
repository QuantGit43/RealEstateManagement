using Microsoft.Data.SqlClient;
using RealEstate.Api.Data.Repositories;

namespace RealEstate.Api.Data;

public interface IUnitOfWork : IDisposable
{
    IPropertyRepository Properties { get; }
    IListingRepository Listings { get; }
    IContractRepository Contracts { get; }
    IPaymentRepository Payments { get; }

    void BeginTransaction();
    void Commit();
    void Rollback();
}

internal sealed class DbSession(SqlConnection connection)
{
    public SqlConnection Connection { get; } = connection;
    public SqlTransaction? Transaction { get; set; }
}

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly DbSession _db;

    public UnitOfWork(string connectionString)
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        _db = new DbSession(connection);

        Properties = new PropertyRepository(_db);
        Listings = new ListingRepository(_db);
        Contracts = new ContractRepository(_db);
        Payments = new PaymentRepository(_db);
    }

    public IPropertyRepository Properties { get; }
    public IListingRepository Listings { get; }
    public IContractRepository Contracts { get; }
    public IPaymentRepository Payments { get; }

    public void BeginTransaction()
    {
        if (_db.Transaction is not null)
            throw new InvalidOperationException("Транзакцію вже розпочато.");
        _db.Transaction = _db.Connection.BeginTransaction();
    }

    public void Commit()
    {
        var tx = _db.Transaction ?? throw new InvalidOperationException("Транзакцію не розпочато.");
        tx.Commit();
        EndTransaction();
    }

    public void Rollback()
    {
        if (_db.Transaction?.Connection is not null)
            _db.Transaction.Rollback();
        EndTransaction();
    }

    private void EndTransaction()
    {
        _db.Transaction?.Dispose();
        _db.Transaction = null;
    }

    public void Dispose()
    {
        EndTransaction();
        _db.Connection.Dispose();
    }
}
