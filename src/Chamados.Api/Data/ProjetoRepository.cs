using Chamados.Shared.Projetos;
using Dapper;

namespace Chamados.Api.Data;

/// <summary>Acesso à CH_PROJETOS. Não há sequence: PJ_ID é o maior código + 1.</summary>
public sealed class ProjetoRepository(IDbConnectionFactory db)
{
    private const string SelectColumns = "SELECT PJ_ID AS Id, PJ_NOME AS Nome FROM CH_PROJETOS";

    public async Task<IReadOnlyList<ProjetoDto>> ListarAsync(CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var rows = await conn.QueryAsync<ProjetoDto>(new CommandDefinition(
            $"{SelectColumns} ORDER BY PJ_NOME, PJ_ID", cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<ProjetoDto?> ObterAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ProjetoDto>(new CommandDefinition(
            $"{SelectColumns} WHERE PJ_ID = @Id", new { Id = id }, cancellationToken: ct));
    }

    public async Task<bool> ExisteAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM CH_PROJETOS WHERE PJ_ID = @Id", new { Id = id }, cancellationToken: ct)) > 0;
    }

    /// <summary>Outro projeto (diferente de <paramref name="ignorarId"/>) já usa este PJ_NOME?</summary>
    public async Task<bool> NomeEmUsoAsync(string nome, long? ignorarId, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM CH_PROJETOS WHERE UPPER(PJ_NOME) = UPPER(@Nome) AND PJ_ID <> COALESCE(@Ignorar, -1)",
            new { Nome = nome, Ignorar = ignorarId }, cancellationToken: ct)) > 0;
    }

    /// <summary>Menus (CH_MENU.MN_PROJETO) vinculados ao projeto: impedem a exclusão (FK ch_menu_ch_projetos_fk).</summary>
    public async Task<IReadOnlyList<string>> MenusVinculadosAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var rows = await conn.QueryAsync<string>(new CommandDefinition(
            "SELECT MN_ID || ' - ' || MN_LABEL FROM CH_MENU WHERE MN_PROJETO = @Id ORDER BY MN_ID",
            new { Id = id }, cancellationToken: ct));
        return rows.AsList();
    }

    /// <summary>Grupos (CH_GRUPOS.CH_PROJETO_GRUPO) do projeto: impedem a exclusão (FK ch_grupos_ch_projetos_fk).</summary>
    public async Task<IReadOnlyList<string>> GruposVinculadosAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var rows = await conn.QueryAsync<string>(new CommandDefinition(
            "SELECT CH_ID_GRUPO || ' - ' || CH_NOME_GRUPO FROM CH_GRUPOS WHERE CH_PROJETO_GRUPO = @Id ORDER BY CH_ID_GRUPO",
            new { Id = id }, cancellationToken: ct));
        return rows.AsList();
    }

    /// <summary>Espera o DTO já normalizado. Retorna o PJ_ID gerado.</summary>
    public async Task<long> InserirAsync(ProjetoDto p, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition("""
            INSERT INTO CH_PROJETOS (PJ_ID, PJ_NOME)
            SELECT COALESCE(MAX(PJ_ID), 0) + 1, @Nome FROM CH_PROJETOS
            RETURNING PJ_ID
            """, new { p.Nome }, cancellationToken: ct));
    }

    /// <summary>Espera o DTO já normalizado. PJ_ID não é alterado.</summary>
    public async Task<bool> AtualizarAsync(long id, ProjetoDto p, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE CH_PROJETOS SET PJ_NOME = @Nome WHERE PJ_ID = @Id",
            new { Id = id, p.Nome }, cancellationToken: ct)) > 0;
    }

    public async Task<bool> ExcluirAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CH_PROJETOS WHERE PJ_ID = @Id", new { Id = id }, cancellationToken: ct)) > 0;
    }
}
