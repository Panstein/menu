using System.ComponentModel.DataAnnotations;

namespace Chamados.Shared.Auth;

public class LoginRequest
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(50)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Informe a senha.")]
    [StringLength(16)]
    public string Senha { get; set; } = "";
}

public record LoginResponse(string Token, DateTime ExpiraEm, string Nome, string Email);

/// <summary>Corpo de erro devolvido pelo login (401/403/429).</summary>
public record LoginErro(string Mensagem);
