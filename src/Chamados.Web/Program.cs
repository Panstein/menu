using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Chamados.Web;
using Chamados.Web.Auth;
using Chamados.Web.Layout.Menu;
using Chamados.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("pt-BR");

// Autenticação
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());
builder.Services.AddScoped<AuthService>();

// HttpClient da API, com o token anexado automaticamente
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(sp => new HttpClient(new AuthHeaderHandler(
        sp.GetRequiredService<TokenStore>(),
        sp.GetRequiredService<JwtAuthStateProvider>(),
        sp.GetRequiredService<NavigationManager>())
    { InnerHandler = new HttpClientHandler() })
{
    BaseAddress = new Uri(apiBaseUrl)
});

builder.Services.AddScoped<UsuariosApi>();
builder.Services.AddScoped<MenusApi>();
builder.Services.AddScoped<ProjetosApi>();
builder.Services.AddScoped<GruposApi>();
builder.Services.AddScoped<MenuLateralService>();
builder.Services.AddSingleton(new ProjetoAtual(ProjetoAtual.Padrao));

await builder.Build().RunAsync();
