using Chamados.Shared.Grupos;
using Dapper;

namespace Chamados.Api.Data;

/// <summary>
/// Acesso à CH_GRUPOS (cada grupo é de um projeto: CH_PROJETO_GRUPO) e aos vínculos usuário x grupo
/// (CH_USU_GRP, chave CH_ID_USU + CH_ID_GRP) e grupo x menu (CH_GRP_MNU).
/// CH_ID_GRUPO vem da sequence SEQ_CH_GRUPOS. CH_ID_USU guarda o CH_USER.ID_USER.
/// </summary>
public sealed class GrupoRepository(IDbConnectionFactory db)
{
    private const string SelectColumns = """
        SELECT g.CH_ID_GRUPO      AS Id,
               g.CH_NOME_GRUPO    AS Nome,
               g.CH_PROJETO_GRUPO AS Projeto,
               pj.PJ_NOME         AS ProjetoNome,
               (SELECT COUNT(*) FROM CH_USU_GRP ug WHERE ug.CH_ID_GRP = g.CH_ID_GRUPO) AS Usuarios
          FROM CH_GRUPOS g
          LEFT JOIN CH_PROJETOS pj ON pj.PJ_ID = g.CH_PROJETO_GRUPO
        """;

    /// <summary>Com <paramref name="projeto"/>, só os grupos daquele projeto; sem ele, todos.</summary>
    public async Task<IReadOnlyList<GrupoDto>> ListarAsync(long? projeto, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var filtro = projeto is null ? "" : "WHERE g.CH_PROJETO_GRUPO = @Projeto";
        var rows = await conn.QueryAsync<GrupoDto>(new CommandDefinition(
            $"{SelectColumns} {filtro} ORDER BY g.CH_NOME_GRUPO, g.CH_ID_GRUPO", new { Projeto = projeto }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<GrupoDto?> ObterAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<GrupoDto>(new CommandDefinition(
            $"{SelectColumns} WHERE g.CH_ID_GRUPO = @Id", new { Id = id }, cancellationToken: ct));
    }

    /// <summary>Outro grupo do mesmo projeto (diferente de <paramref name="ignorarId"/>) já usa este CH_NOME_GRUPO?</summary>
    public async Task<bool> NomeEmUsoAsync(string nome, long? projeto, long? ignorarId, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM CH_GRUPOS
             WHERE UPPER(CH_NOME_GRUPO) = UPPER(@Nome)
               AND CH_PROJETO_GRUPO IS NOT DISTINCT FROM @Projeto
               AND CH_ID_GRUPO <> COALESCE(@Ignorar, -1)
            """, new { Nome = nome, Projeto = projeto, Ignorar = ignorarId }, cancellationToken: ct)) > 0;
    }

    /// <summary>Espera o DTO já normalizado. Retorna o CH_ID_GRUPO gerado pela sequence SEQ_CH_GRUPOS.</summary>
    public async Task<long> InserirAsync(GrupoDto g, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition("""
            INSERT INTO CH_GRUPOS (CH_ID_GRUPO, CH_NOME_GRUPO, CH_PROJETO_GRUPO)
            VALUES (nextval('seq_ch_grupos'), @Nome, @Projeto)
            RETURNING CH_ID_GRUPO
            """, new { g.Nome, g.Projeto }, cancellationToken: ct));
    }

    /// <summary>Espera o DTO já normalizado. CH_ID_GRUPO não é alterado.</summary>
    public async Task<bool> AtualizarAsync(long id, GrupoDto g, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE CH_GRUPOS SET CH_NOME_GRUPO = @Nome, CH_PROJETO_GRUPO = @Projeto WHERE CH_ID_GRUPO = @Id",
            new { Id = id, g.Nome, g.Projeto }, cancellationToken: ct)) > 0;
    }

    /// <summary>Acessos do grupo (CH_GRP_MNU) a menus de projeto diferente de <paramref name="projeto"/>.</summary>
    public async Task<int> MenusDeOutroProjetoAsync(long id, long? projeto, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*)
              FROM CH_GRP_MNU gm
              JOIN CH_MENU m ON m.MN_ID = gm.CH_ID_MNU
             WHERE gm.CH_ID_GRP = @Id AND m.MN_PROJETO IS DISTINCT FROM @Projeto
            """, new { Id = id, Projeto = projeto }, cancellationToken: ct));
    }

    public async Task<bool> ExcluirAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CH_GRUPOS WHERE CH_ID_GRUPO = @Id", new { Id = id }, cancellationToken: ct)) > 0;
    }

    /// <summary>Usuários do grupo, como "ID - NOME" (para mensagens).</summary>
    public async Task<IReadOnlyList<string>> NomesDosUsuariosAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var rows = await conn.QueryAsync<string>(new CommandDefinition("""
            SELECT u.ID_USER || ' - ' || u.NOME_USER
              FROM CH_USU_GRP ug
              JOIN CH_USER u ON u.ID_USER = ug.CH_ID_USU
             WHERE ug.CH_ID_GRP = @Id
             ORDER BY u.NOME_USER
            """, new { Id = id }, cancellationToken: ct));
        return rows.AsList();
    }

    /// <summary>ID_USER dos usuários do grupo.</summary>
    public async Task<IReadOnlyList<long>> UsuariosAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var rows = await conn.QueryAsync<long>(new CommandDefinition(
            "SELECT CH_ID_USU FROM CH_USU_GRP WHERE CH_ID_GRP = @Id ORDER BY CH_ID_USU",
            new { Id = id }, cancellationToken: ct));
        return rows.AsList();
    }

    /// <summary>Dos códigos informados, quais não existem na CH_USER.</summary>
    public async Task<IReadOnlyList<long>> UsuariosInexistentesAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        await using var conn = await db.OpenAsync(ct);
        var existentes = (await conn.QueryAsync<long>(new CommandDefinition(
            "SELECT ID_USER FROM CH_USER WHERE ID_USER = ANY(@Ids)", new { Ids = ids.ToArray() }, cancellationToken: ct))).ToHashSet();
        return ids.Where(i => !existentes.Contains(i)).ToList();
    }

    /// <summary>MN_ID dos menus que o grupo acessa (CH_GRP_MNU), só do projeto informado.</summary>
    public async Task<IReadOnlyList<long>> MenusAsync(long id, long projeto, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var rows = await conn.QueryAsync<long>(new CommandDefinition("""
            SELECT gm.CH_ID_MNU
              FROM CH_GRP_MNU gm
              JOIN CH_MENU m ON m.MN_ID = gm.CH_ID_MNU
             WHERE gm.CH_ID_GRP = @Id AND m.MN_PROJETO = @Projeto
             ORDER BY gm.CH_ID_MNU
            """, new { Id = id, Projeto = projeto }, cancellationToken: ct));
        return rows.AsList();
    }

    /// <summary>Dos menus informados, quais não existem ou não pertencem ao projeto.</summary>
    public async Task<IReadOnlyList<long>> MenusForaDoProjetoAsync(IReadOnlyCollection<long> menus, long projeto, CancellationToken ct)
    {
        if (menus.Count == 0) return [];
        await using var conn = await db.OpenAsync(ct);
        var validos = (await conn.QueryAsync<long>(new CommandDefinition(
            "SELECT MN_ID FROM CH_MENU WHERE MN_ID = ANY(@Ids) AND MN_PROJETO = @Projeto",
            new { Ids = menus.ToArray(), Projeto = projeto }, cancellationToken: ct))).ToHashSet();
        return menus.Where(m => !validos.Contains(m)).ToList();
    }

    /// <summary>
    /// Grava na CH_GRP_MNU só a diferença, numa única transação: inclui os menus marcados que ainda
    /// não estão na tabela e remove os desmarcados. Só mexe nos menus do projeto informado.
    /// Retorna (incluídos, removidos).
    /// </summary>
    public async Task<(int Incluidos, int Removidos)> DefinirMenusAsync(long id, long projeto, IReadOnlyCollection<long> menus, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var parametros = new { Id = id, Projeto = projeto, Ids = menus.ToArray() };

        var removidos = await conn.ExecuteAsync(new CommandDefinition("""
            DELETE FROM CH_GRP_MNU gm
             USING CH_MENU m
             WHERE m.MN_ID = gm.CH_ID_MNU
               AND gm.CH_ID_GRP = @Id
               AND m.MN_PROJETO = @Projeto
               AND NOT (gm.CH_ID_MNU = ANY(@Ids))
            """, parametros, tx, cancellationToken: ct));
        var incluidos = await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO CH_GRP_MNU (CH_ID_GRP, CH_ID_MNU)
            SELECT @Id, x.MN_ID FROM UNNEST(@Ids) AS x(MN_ID)
            ON CONFLICT (CH_ID_GRP, CH_ID_MNU) DO NOTHING
            """, parametros, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
        return (incluidos, removidos);
    }

    /// <summary>Menus que o grupo acessa, como "ID - LABEL" (para mensagens).</summary>
    public async Task<IReadOnlyList<string>> NomesDosMenusAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var rows = await conn.QueryAsync<string>(new CommandDefinition("""
            SELECT m.MN_ID || ' - ' || m.MN_LABEL
              FROM CH_GRP_MNU gm
              JOIN CH_MENU m ON m.MN_ID = gm.CH_ID_MNU
             WHERE gm.CH_ID_GRP = @Id
             ORDER BY m.MN_ID
            """, new { Id = id }, cancellationToken: ct));
        return rows.AsList();
    }

    /// <summary>Substitui os usuários do grupo pelos informados (CH_USU_GRP), numa única transação.</summary>
    public async Task DefinirUsuariosAsync(long id, IReadOnlyCollection<long> usuarios, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CH_USU_GRP WHERE CH_ID_GRP = @Id", new { Id = id }, tx, cancellationToken: ct));
        if (usuarios.Count > 0)
            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO CH_USU_GRP (CH_ID_USU, CH_ID_GRP) VALUES (@Usuario, @Grupo)",
                usuarios.Select(u => new { Usuario = u, Grupo = id }), tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
    }
}
