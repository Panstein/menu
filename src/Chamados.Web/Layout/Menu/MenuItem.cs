namespace Chamados.Web.Layout.Menu;

public enum MenuAcao
{
    /// <summary>Navega para <see cref="MenuItem.Rota"/> (ou só agrupa, se tiver filhos).</summary>
    Navegar,

    /// <summary>Encerra a sessão do usuário.</summary>
    Encerrar
}

/// <summary>Item do menu em árvore. O Id segue o padrão do sistema (ex.: "Cad_Usuarios").</summary>
public sealed record MenuItem(
    string Id,
    string Texto,
    string? Icone = null,
    string? Rota = null,
    IReadOnlyList<MenuItem>? Filhos = null,
    MenuAcao Acao = MenuAcao.Navegar)
{
    public bool EhPasta => Filhos is { Count: > 0 };
}
