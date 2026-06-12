using System;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using Microsoft.Identity.Client;
using XboxAuthNet.Game.Msal;

namespace TCMine_Launcher.Services;

/// <summary>
///     Autenticação Microsoft/Xbox via CmlLib + MSAL.
///     No Windows o MSAL usa WebView2 (popup embutido) para o login interativo
///     e o token em cache (DPAPI) para o login silencioso.
///     O token é persistido por <see cref="MsalClientHelper" />, permitindo
///     reentrar entre execuções sem voltar a pedir credenciais.
/// </summary>
public class AuthService
{
    private readonly JELoginHandler _handler = JELoginHandlerBuilder.BuildDefault();
    private IPublicClientApplication? _app;

    private async Task<IPublicClientApplication> GetAppAsync()
    {
        var clientId = AppConfig.MicrosoftClientId;
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException(
                "Client ID da Microsoft não configurado. Cria TCMine-Launcher/Client.props " +
                "(ver Client.props.example) com o teu Azure client ID e recompila.");

        // BuildApplicationWithCache → WebView2 interativo + cache DPAPI persistente.
        return _app ??= await MsalClientHelper.BuildApplicationWithCache(clientId);
    }

    /// <summary>Login silencioso (token em cache). Lança se não houver conta válida.</summary>
    public async Task<MSession> LoginSilentAsync(CancellationToken ct = default)
    {
        var app = await GetAppAsync();
        var auth = _handler.CreateAuthenticatorWithDefaultAccount(ct);
        auth.AddMsalOAuth(app, msal => msal.Silent());
        auth.AddXboxAuthForJE(xb => xb.Basic());
        auth.AddJEAuthenticator();
        return await auth.ExecuteForLauncherAsync();
    }

    /// <summary>Login completo: tenta silencioso e, se preciso, abre popup WebView2.</summary>
    public async Task<MSession> LoginAsync(CancellationToken ct = default)
    {
        try
        {
            return await LoginSilentAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Sem conta/token válido — segue para o login interativo.
        }

        var app = await GetAppAsync();
        var auth = _handler.CreateAuthenticatorWithNewAccount(ct);
        auth.AddMsalOAuth(app, msal => msal.Interactive());
        auth.AddXboxAuthForJE(xb => xb.Basic());
        auth.AddJEAuthenticator();
        return await auth.ExecuteForLauncherAsync();
    }

    /// <summary>Logout real: remove a conta local e o token em cache do MSAL. Best-effort.</summary>
    public async Task SignOutAsync(CancellationToken ct = default)
    {
        var account = _handler.AccountManager.GetDefaultAccount();
        await _handler.Signout(account, ct);

        try
        {
            var app = await GetAppAsync();
            await MsalClientHelper.RemoveAccounts(app);
        }
        catch
        {
            // ignora falhas a limpar o cache MSAL
        }
    }
}