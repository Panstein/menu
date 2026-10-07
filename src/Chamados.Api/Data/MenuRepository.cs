using Chamados.Shared.Menus;
using Dapper;

namespace Chamados.Api.Data;

/// <summary>Acesso à CH_MENU. MN_ID vem da sequence SEQ_CH_MENU.</summary>
public sealed class MenuRepository(IDbConnectionFactory db)
{
    private const string SelectColumns = """
        SELECT m.MN_ID      AS Id,
               m.MN_NOME    AS Nome,
               m.MN_LABEL   AS Label,
               m.MN_DEPENDE AS Depende,
               m.MN_ORDEM   AS Ordem,
               m.MN_PROJETO AS Projeto,
               p.MN_LABEL   AS DependeLabel,
               pj.PJ_NOME   AS ProjetoNome
          FROM CH_MENU m
          LEFT JOIN CH_MENU p ON p.MN_ID = m.MN_DEPENDE
          LEFT JOIN CH_PROJETOS pj ON pj.PJ_ID = m.MN_PROJETO
        """;

    /// <summary>Com <paramref name="projeto"/>, só os itens daquele MN_PROJETO; sem ele, todos.</summary>
    public async Task<IReadOnlyList<MenuDto>> ListarAsync(long? projeto, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var filtro = projeto is null ? "" : "WHERE m.MN_PROJETO = @Projeto";
        var rows = await conn.QueryAsync<MenuDto>(new CommandDefinition(
            $"{SelectColumns} {filtro} ORDER BY COALESCE(m.MN_DEPENDE, 0), m.MN_ORDEM, m.MN_ID",
            new { Projeto = projeto }, cancellationToken: ct));
        return rows.AsList();
    }

    /// <summary>Submenus de <paramref name="id"/> que estão em projeto diferente de <paramref name="projeto"/>.</summary>
    public async Task<int> FilhosDeOutroProjetoAsync(long id, long? projeto, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM CH_MENU WHERE MN_DEPENDE = @Id AND MN_PROJETO IS DISTINCT FROM @Projeto",
            new { Id = id, Projeto = projeto }, cancellationToken: ct));
    }

    public async Task<MenuDto?> ObterAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<MenuDto>(new CommandDefinition(
            $"{SelectColumns} WHERE m.MN_ID = @Id", new { Id = id }, cancellationToken: ct));
    }

    /// <summary>
    /// Outro menu (diferente de <paramref name="ignorarId"/>) do mesmo projeto já usa este MN_NOME?
    /// Projetos diferentes podem repetir o nome (ex.: CAD_USUARIOS em cada sistema).
    /// </summary>
    public async Task<bool> NomeEmUsoAsync(string nome, long? projeto, long? ignorarId, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM CH_MENU
             WHERE UPPER(MN_NOME) = UPPER(@Nome)
               AND MN_PROJETO IS NOT DISTINCT FROM @Projeto
               AND MN_ID <> COALESCE(@Ignorar, -1)
            """, new { Nome = nome, Projeto = projeto, Ignorar = ignorarId }, cancellationToken: ct)) > 0;
    }

    /// <summary>
    /// <paramref name="id"/> aparece na cadeia de pais de <paramref name="novoPai"/>?
    /// Se sim, ligar id a novoPai criaria um ciclo na árvore.
    /// </summary>
    public async Task<bool> CriariaCicloAsync(long id, long novoPai, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            WITH RECURSIVE pais AS (
                SELECT MN_ID, MN_DEPENDE FROM CH_MENU WHERE MN_ID = @NovoPai
                UNION
                SELECT m.MN_ID, m.MN_DEPENDE FROM CH_MENU m JOIN pais p ON m.MN_ID = p.MN_DEPENDE
            )
            SELECT COUNT(*) FROM pais WHERE MN_ID = @Id
            """, new { Id = id, NovoPai = novoPai }, cancellationToken: ct)) > 0;
    }

    /// <summary>Grupos com acesso ao menu (CH_GRP_MNU), como "ID - NOME": impedem a exclusão (FK ch_grp_mnu_menu_fk).</summary>
    public async Task<IReadOnlyList<string>> GruposComAcessoAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var rows = await conn.QueryAsync<string>(new CommandDefinition("""
            SELECT g.CH_ID_GRUPO || ' - ' || g.CH_NOME_GRUPO
              FROM CH_GRP_MNU gm
              JOIN CH_GRUPOS g ON g.CH_ID_GRUPO = gm.CH_ID_GRP
             WHERE gm.CH_ID_MNU = @Id
             ORDER BY g.CH_ID_GRUPO
            """, new { Id = id }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<string>> DependentesAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var rows = await conn.QueryAsync<string>(new CommandDefinition(
            "SELECT MN_ID || ' - ' || MN_LABEL FROM CH_MENU WHERE MN_DEPENDE = @Id ORDER BY MN_ORDEM, MN_ID",
            new { Id = id }, cancellationToken: ct));
        return rows.AsList();
    }

    /// <summary>Espera o DTO já normalizado. Retorna o MN_ID gerado pela sequence.</summary>
    public async Task<long> InserirAsync(MenuDto m, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var id = await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT nextval('seq_ch_menu')", cancellationToken: ct));

        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO CH_MENU (MN_ID, MN_NOME, MN_LABEL, MN_DEPENDE, MN_ORDEM, MN_PROJETO)
            VALUES (@Id, @Nome, @Label, @Depende, @Ordem, @Projeto)
            """, new { Id = id, m.Nome, m.Label, m.Depende, m.Ordem, m.Projeto }, cancellationToken: ct));
        return id;
    }

    /// <summary>Espera o DTO já normalizado. MN_ID não é alterado.</summary>
    public async Task<bool> AtualizarAsync(long id, MenuDto m, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE CH_MENU
               SET MN_NOME    = @Nome,
                   MN_LABEL   = @Label,
                   MN_DEPENDE = @Depende,
                   MN_ORDEM   = @Ordem,
                   MN_PROJETO = @Projeto
             WHERE MN_ID = @Id
            """, new { Id = id, m.Nome, m.Label, m.Depende, m.Ordem, m.Projeto }, cancellationToken: ct)) > 0;
    }

    public async Task<bool> ExcluirAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CH_MENU WHERE MN_ID = @Id", new { Id = id }, cancellationToken: ct)) > 0;
    }
}
