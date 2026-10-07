using System.Security.Cryptography;
using System.Text;
using Chamados.Api.Auth;
using Chamados.Api.Data;
using Chamados.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Chamados.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(UsuarioRepository usuarios, TokenService tokens, ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct)
    {
        var u = await usuarios.ObterCredencialAsync(req.Email, ct);

        // Mesma mensagem para e-mail inexistente e senha errada, para não revelar quais e-mails existem.
        if (u is null || !SenhaConfere(req.Senha, u.Senha))
        {
            logger.LogWarning("Login recusado (credenciais inválidas) para {Email}", req.Email);
            return Unauthorized(new LoginErro("E-mail ou senha inválidos."));
        }

        if (!string.Equals(u.SuperUser?.Trim(), "S", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Login recusado (não é superusuário) para {Email}", u.Email);
            return StatusCode(StatusCodes.Status403Forbidden,
                new LoginErro("Acesso não permitido: usuário sem permissão de superusuário."));
        }

        var (token, expira) = tokens.Gerar(u.Email, u.Nome);
        logger.LogInformation("Login efetuado por {Email}", u.Email);
        return Ok(new LoginResponse(token, expira, u.Nome, u.Email));
    }

    // As senhas são gravadas em maiúsculas na CH_USER: a senha digitada é convertida antes da comparação.
    private static bool SenhaConfere(string informada, string? armazenada) =>
        armazenada is not null &&
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(informada.ToUpperInvariant()),
            Encoding.UTF8.GetBytes(armazenada));
}
