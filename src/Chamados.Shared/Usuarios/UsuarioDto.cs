using System.ComponentModel.DataAnnotations;

namespace Chamados.Shared.Usuarios;

/// <summary>Usuário do sistema (tabela CH_USER). Os textos são gravados em maiúsculas, exceto o e-mail.</summary>
public class UsuarioDto
{
    /// <summary>ID_USER: gerado pela sequence SEQ_CH_USER na inclusão; não editável. Usado na CH_USU_GRP.</summary>
    public long? Id { get; set; }

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(50, ErrorMessage = "Máximo de 50 caracteres.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(40, ErrorMessage = "Máximo de 40 caracteres.")]
    public string Nome { get; set; } = "";

    [StringLength(20, ErrorMessage = "Máximo de 20 caracteres.")]
    public string? Cpf { get; set; }

    public bool SuperUsuario { get; set; }

    /// <summary>Somente escrita (nunca devolvida pela API). Na alteração, vazio mantém a senha atual.</summary>
    [StringLength(16, ErrorMessage = "Máximo de 16 caracteres.")]
    public string? Senha { get; set; }

    /// <summary>Repetição da senha (duplo check).</summary>
    [StringLength(16, ErrorMessage = "Máximo de 16 caracteres.")]
    public string? ConfirmacaoSenha { get; set; }

    /// <summary>Converte os textos para maiúsculas, como são gravados na CH_USER (o e-mail mantém o que foi digitado).</summary>
    public void Normalizar()
    {
        Email = Email.Trim();
        Nome = Nome.Trim().ToUpperInvariant();
        Cpf = string.IsNullOrWhiteSpace(Cpf) ? null : Cpf.Trim().ToUpperInvariant();
        Senha = string.IsNullOrEmpty(Senha) ? null : Senha.ToUpperInvariant();
        ConfirmacaoSenha = string.IsNullOrEmpty(ConfirmacaoSenha) ? null : ConfirmacaoSenha.ToUpperInvariant();
    }
}
