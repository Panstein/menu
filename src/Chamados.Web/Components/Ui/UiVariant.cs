namespace Chamados.Web.Components.Ui;

public enum UiVariant
{
    Neutral,
    Primary,
    Success,
    Warning,
    Danger,
    Info
}

internal static class UiVariantExtensions
{
    public static string Css(this UiVariant v, string prefix) =>
        v == UiVariant.Neutral ? "" : $"{prefix}-{v.ToString().ToLowerInvariant()}";
}
