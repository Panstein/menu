using Chamados.Shared.Menus;

namespace Chamados.Web.Layout.Menu;

/// <summary>Nó da árvore de menus: o registro da CH_MENU e seus submenus, já ordenados.</summary>
public sealed record MenuNo(MenuDto Menu, IReadOnlyList<MenuNo> Filhos);

/// <summary>
/// Hierarquia da CH_MENU (MN_DEPENDE, ordem por MN_ORDEM). Usada pelo menu lateral e pela tela
/// Grupos x Menus, para que as duas mostrem exatamente a mesma estrutura.
/// </summary>
public static class MenuArvore
{
    /// <summary>
    /// Itens sem pai (ou com pai inexistente) ficam na raiz; os demais, abaixo do MN_DEPENDE.
    /// Dados inconsistentes não escondem o menu: um item que depende dele mesmo, ou preso em um
    /// ciclo de dependência, também vai para a raiz (cada item aparece uma única vez).
    /// </summary>
    public static List<MenuNo> Montar(IEnumerable<MenuDto> registros)
    {
        var lista = registros.Where(m => m.Id is not null).OrderBy(m => m.Ordem).ThenBy(m => m.Id).ToList();
        var ids = lista.Select(m => m.Id!.Value).ToHashSet();
        var filhosDe = lista.ToLookup(m => m.Depende is { } pai && ids.Contains(pai) ? pai : (long?)null);
        var visitados = new HashSet<long>();

        List<MenuNo> Nivel(long? pai) =>
            filhosDe[pai].Where(m => visitados.Add(m.Id!.Value)).Select(m => new MenuNo(m, Nivel(m.Id))).ToList();

        var raiz = Nivel(null);

        // Itens fora da árvore (auto-referência ou ciclo): sobe pelos pais até repetir um item do
        // caminho e monta a partir dele, para que os descendentes fiquem embaixo do pai correto.
        var porId = lista.ToDictionary(m => m.Id!.Value);
        foreach (var m in lista)
        {
            var topo = m;
            var caminho = new HashSet<long>();
            while (caminho.Add(topo.Id!.Value) && topo.Depende is { } pai &&
                   !visitados.Contains(pai) && porId.TryGetValue(pai, out var menuPai))
                topo = menuPai;
            if (visitados.Add(topo.Id!.Value)) raiz.Add(new MenuNo(topo, Nivel(topo.Id)));
        }

        return raiz;
    }
}
