using Npgsql;

namespace Chamados.Api.Data;

/// <summary>Conexões com o PostgreSQL (banco "sistemas", schema "menu" via Search Path).</summary>
public interface IDbConnectionFactory
{
    Task<NpgsqlConnection> OpenAsync(CancellationToken ct = default);
}

/// <summary>Usa um NpgsqlDataSource único (com pool de conexões) para toda a aplicação.</summary>
public sealed class PostgresConnectionFactory : IDbConnectionFactory, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresConnectionFactory(IConfiguration configuration)
    {
        // Local: dotnet user-secrets; Render: variável de ambiente ConnectionStrings__Postgres
        var connectionString = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:Postgres não configurada (user-secrets ou variável ConnectionStrings__Postgres).");
        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct = default) =>
        await _dataSource.OpenConnectionAsync(ct);

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
