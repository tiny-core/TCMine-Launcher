using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core.Auth;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using TCMine_Launcher.Models;
using TCMine_Launcher.Services;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Parte do shell dedicada à <b>autenticação Microsoft</b>: login interativo
///     (navegador do sistema), login silencioso no arranque, logout e mapeamento
///     da sessão CmlLib (<see cref="MSession" />) para o perfil (dados puros).
/// </summary>
public partial class MainWindowViewModel
{
    private readonly AuthService _auth = new();

    /// <summary>Sessão Minecraft activa (usada depois para lançar o jogo).</summary>
    private MSession? _session;

    /// <summary>Sessão activa, exposta às páginas que precisam de lançar o jogo.</summary>
    public MSession? CurrentSession => _session;

    /// <summary>Permite cancelar o login interactivo em curso.</summary>
    private CancellationTokenSource? _loginCts;

    // ── Autenticação Microsoft (navegador do sistema) ────────────
    [RelayCommand]
    private async Task LoginMicrosoftAsync()
    {
        if (IsAuthenticating) return;

        IsAuthenticating = true;
        LoginError = null;
        StatusMessage = "A autenticar com a Microsoft...";

        // Timeout de segurança: se o utilizador fechar o browser, não fica preso.
        _loginCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            var session = await _auth.LoginAsync(_loginCts.Token);
            ApplySession(session, AccountType.Microsoft);
            EnterApp("Pronto");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Login cancelado";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Falha no login Microsoft");
            var detail = Flatten(ex);
            LoginError = detail.Contains("403") || detail.Contains("Forbidden")
                ? "Autenticaste com sucesso, mas a aplicação do Azure ainda não tem permissão " +
                  "para a API do Minecraft (erro 403). Pede aprovação em https://aka.ms/mce-reviewappid."
                : "Falha no login: " + detail;
            StatusMessage = "Não autenticado";
        }
        finally
        {
            _loginCts?.Dispose();
            _loginCts = null;
            IsAuthenticating = false;
        }
    }

    [RelayCommand]
    private void CancelLogin()
    {
        _loginCts?.Cancel();
    }

    /// <summary>Junta as mensagens de toda a cadeia de InnerException.</summary>
    private static string Flatten(Exception ex)
    {
        var parts = new List<string>();
        for (var e = ex; e is not null; e = e.InnerException)
            parts.Add(e.Message);
        return string.Join(" → ", parts);
    }

    /// <summary>Login silencioso no arranque (não mostra erro se não houver conta).</summary>
    private async Task TrySilentLoginAsync()
    {
        try
        {
            var session = await _auth.LoginSilentAsync();
            ApplySession(session, AccountType.Microsoft);
            EnterApp("Pronto");
        }
        catch
        {
            // Sem sessão em cache — fica na tela de login.
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        try
        {
            await _auth.SignOutAsync();
        }
        catch
        {
            // Best-effort: mesmo que falhe, limpamos o estado local.
        }

        _session = null;
        _player.Name = "Steve";
        _player.Uuid = string.Empty;
        _player.AccountType = AccountType.Offline;
        RefreshPlayer();

        IsLoggedIn = false;
        StatusMessage = "Não autenticado";
    }

    /// <summary>Mapeia a sessão devolvida pelo CmlLib para o perfil (dados puros).</summary>
    private void ApplySession(MSession session, AccountType type)
    {
        _session = session;
        _player.Name = session.Username ?? "Player";
        _player.Uuid = session.UUID ?? string.Empty;
        _player.AccountType = type;
        Log.Information("Sessão iniciada: {User} ({Uuid})", _player.Name, _player.Uuid);
    }

    private void EnterApp(string status)
    {
        RefreshPlayer();
        SelectedTab = AppTab.Home;
        IsLoggedIn = true;
        StatusMessage = status;
    }
}
