using Chamados.Shared.Menus;

namespace Chamados.Web.Layout.Menu;

/// <summary>Monta o menu lateral a partir dos registros da CH_MENU (hierarquia por MN_DEPENDE, ordem por MN_ORDEM).</summary>
public static class MenuConfig
{
    /// <summary>Telas do sistema, por MN_NOME. Um MN_NOME fora desta lista aparece no menu, mas sem tela vinculada.</summary>
    private static readonly Dictionary<string, (string Rota, string Icone)> Telas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CAD_USUARIOS"] = ("cadastros/usuarios", "bi-people"),
        ["CAD_PROJETOS"] = ("cadastros/projetos", "bi-kanban"),
        ["CAD_MENUS"] = ("cadastros/menus", "bi-list-nested"),
        ["CAD_GRUPOS"] = ("cadastros/grupos", "bi-collection"),
        ["CAD_USUARIOSGRUPOS"] = ("cadastros/usuarios-grupos", "bi-person-vcard"),
        ["CAD_GRUPOSMENUS"] = ("cadastros/grupos-menus", "bi-person-lock"),
    };

    /// <summary>Item fixo ao final do menu (não vem da CH_MENU).</summary>
    public static readonly MenuItem Encerrar = new("Encerrar", "Encerrar", "bi-box-arrow-left", Acao: MenuAcao.Encerrar);

    /// <summary>Menu lateral: a árvore da CH_MENU (ver <see cref="MenuArvore"/>) seguida do "Encerrar".</summary>
    public static IReadOnlyList<MenuItem> Montar(IEnumerable<MenuDto> registros) =>
        [.. MenuArvore.Montar(registros).Select(Converter), Encerrar];

    /// <summary>Ícone do item: pasta se tiver submenus; senão, o da tela vinculada ao MN_NOME.</summary>
    public static string IconeDe(MenuNo no) =>
        no.Filhos.Count > 0 ? "bi-folder2"
        : Telas.TryGetValue(no.Menu.Nome, out var tela) ? tela.Icone
        : "bi-file-earmark";

    private static MenuItem Converter(MenuNo no)
    {
        var rota = Telas.TryGetValue(no.Menu.Nome, out var tela) ? tela.Rota : null;
        return new MenuItem(no.Menu.Nome, no.Menu.Label, IconeDe(no), rota, no.Filhos.Select(Converter).ToList());
    }
}
