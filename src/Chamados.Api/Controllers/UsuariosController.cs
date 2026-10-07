using Chamados.Api.Data;
using Chamados.Shared.Usuarios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Chamados.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsuariosController(UsuarioRepository repo, ILogger<UsuariosController> logger) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<UsuarioDto>> Listar(CancellationToken ct) => repo.ListarAsync(ct);

    [HttpGet("{email}")]
    public async Task<ActionResult<UsuarioDto>> Obter(string email, CancellationToken ct) =>
        await repo.ObterAsync(email, ct) is { } u ? u : NotFound();

    /// <summary>Verifica duplicidade de e-mail (usado pelo formulário ao sair do campo).</summary>
    [HttpGet("existe")]
    public async Task<bool> Existe([FromQuery] string email, CancellationToken ct) =>
        !string.IsNullOrWhiteSpace(email) && await repo.ExisteAsync(email, ct);

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Inserir(UsuarioDto dto, CancellationToken ct)
    {
        dto.Normalizar();

        if (dto.Senha is null)
            return Erro(nameof(dto.Senha), "Informe a senha.");
        if (ValidarConfirmacao(dto) is { } erroSenha)
            return erroSenha;
        if (await repo.ExisteAsync(dto.Email, ct))
            return Erro(nameof(dto.Email), "Já existe um usuário cadastrado com este e-mail.");

        try
        {
            await repo.InserirAsync(dto, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation) // inserção simultânea com o mesmo e-mail
        {
            return Erro(nameof(dto.Email), "Já existe um usuário cadastrado com este e-mail.");
        }

        logger.LogInformation("Usuário {Email} incluído por {Autor}", dto.Email, UsuarioLogado);
        return CreatedAtAction(nameof(Obter), new { email = dto.Email }, Publico(dto));
    }

    [HttpPut("{email}")]
    public async Task<IActionResult> Atualizar(string email, UsuarioDto dto, CancellationToken ct)
    {
        dto.Normalizar();

        if (ValidarConfirmacao(dto) is { } erroSenha)
            return erroSenha;
        if (EhOProprio(email) && !dto.SuperUsuario)
            return Erro(nameof(dto.SuperUsuario), "Você não pode remover o seu próprio acesso de superusuário.");

        if (!await repo.AtualizarAsync(email, dto, ct))
            return NotFound();

        logger.LogInformation("Usuário {Email} alterado por {Autor}", email, UsuarioLogado);
        return NoContent();
    }

    [HttpDelete("{email}")]
    public async Task<IActionResult> Excluir(string email, CancellationToken ct)
    {
        if (EhOProprio(email))
            return Erro("", "Você não pode excluir o seu próprio usuário.");

        if (!await repo.ExcluirAsync(email, ct))
            return NotFound();

        logger.LogInformation("Usuário {Email} excluído por {Autor}", email, UsuarioLogado);
        return NoContent();
    }

    // Duplo check da senha: a confirmação precisa ser igual (ambas já em maiúsculas).
    private ActionResult? ValidarConfirmacao(UsuarioDto dto) =>
        dto.Senha is null && dto.ConfirmacaoSenha is null ? null
        : dto.Senha != dto.ConfirmacaoSenha ? Erro(nameof(dto.ConfirmacaoSenha), "A confirmação não confere com a senha.")
        : null;

    private string? UsuarioLogado => User.FindFirst("email")?.Value;

    private bool EhOProprio(string email) =>
        string.Equals(email.Trim(), UsuarioLogado, StringComparison.OrdinalIgnoreCase);

    private static UsuarioDto Publico(UsuarioDto dto)
    {
        dto.Senha = dto.ConfirmacaoSenha = null;
        return dto;
    }

    private ActionResult Erro(string campo, string mensagem) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [campo] = [mensagem] }));
}
