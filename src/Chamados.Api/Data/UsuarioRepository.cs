using Chamados.Shared.Usuarios;
using Dapper;

namespace Chamados.Api.Data;

public sealed record UsuarioCredencial(string Email, string Nome, string Senha, string SuperUser);

/// <summary>Acesso à CH_USER. E-mails são comparados sem diferenciar maiúsculas/minúsculas.</summary>
public sealed class UsuarioRepository(IDbConnectionFactory db)
{
    private const string SelectColumns = """
        SELECT ID_USER    AS Id,
               EMAIL_USER AS Email,
               NOME_USER  AS Nome,
               CPF_USER   AS Cpf,
               (SUPERUSER = 'S') AS SuperUsuario
          FROM CH_USER
        """;

    public async Task<UsuarioCredencial?> ObterCredencialAsync(string email, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<UsuarioCredencial>(new CommandDefinition("""
            SELECT EMAIL_USER AS Email,
                   NOME_USER  AS Nome,
                   SENHA_USER AS Senha,
                   SUPERUSER  AS SuperUser
              FROM CH_USER
             WHERE UPPER(EMAIL_USER) = UPPER(@Email)
            """, new { Email = email.Trim() }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync(CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var rows = await conn.QueryAsync<UsuarioDto>(
            new CommandDefinition($"{SelectColumns} ORDER BY NOME_USER", cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<UsuarioDto?> ObterAsync(string email, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<UsuarioDto>(new CommandDefinition(
            $"{SelectColumns} WHERE UPPER(EMAIL_USER) = UPPER(@Email)", new { Email = email.Trim() }, cancellationToken: ct));
    }

    public async Task<bool> ExisteAsync(string email, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM CH_USER WHERE UPPER(EMAIL_USER) = UPPER(@Email)",
            new { Email = email.Trim() }, cancellationToken: ct)) > 0;
    }

    /// <summary>Espera o DTO já normalizado (maiúsculas).</summary>
    public async Task InserirAsync(UsuarioDto u, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO CH_USER (EMAIL_USER, NOME_USER, CPF_USER, SUPERUSER, SENHA_USER)
            VALUES (@Email, @Nome, @Cpf, @Super, @Senha)
            """,
            new { u.Email, u.Nome, u.Cpf, Super = u.SuperUsuario ? "S" : "N", u.Senha },
            cancellationToken: ct));
    }

    /// <summary>Espera o DTO já normalizado. O e-mail (chave) não é alterado.</summary>
    public async Task<bool> AtualizarAsync(string emailAtual, UsuarioDto u, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var linhas = await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE CH_USER
               SET NOME_USER  = @Nome,
                   CPF_USER   = @Cpf,
                   SUPERUSER  = @Super,
                   SENHA_USER = COALESCE(@Senha, SENHA_USER)
             WHERE UPPER(EMAIL_USER) = UPPER(@Email)
            """,
            new { Email = emailAtual.Trim(), u.Nome, u.Cpf, Super = u.SuperUsuario ? "S" : "N", u.Senha },
            cancellationToken: ct));
        return linhas > 0;
    }

    /// <summary>Remove também os vínculos do usuário com grupos (CH_USU_GRP), na mesma transação.</summary>
    public async Task<bool> ExcluirAsync(string email, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var parametros = new { Email = email.Trim() };

        await conn.ExecuteAsync(new CommandDefinition("""
            DELETE FROM CH_USU_GRP
             WHERE CH_ID_USU IN (SELECT ID_USER FROM CH_USER WHERE UPPER(EMAIL_USER) = UPPER(@Email))
            """, parametros, tx, cancellationToken: ct));
        var excluidos = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CH_USER WHERE UPPER(EMAIL_USER) = UPPER(@Email)", parametros, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
        return excluidos > 0;
    }
}
