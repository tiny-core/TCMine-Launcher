using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using TCMine_Launcher.Models;
using TCMine_Launcher.Services;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Página "Jogar": hero da instância ativa + perfil + launch real. O indicador
///     de estado muda de cor conforme o estado (por instalar / pronto / a instalar /
///     em execução) e, em instâncias com servidor, mostra o estado online/offline.
/// </summary>
public partial class HomePageViewModel : ViewModelBase
{
    private readonly GameProfile _game;
    private readonly GameLauncher _launcher = new();
    private readonly MinecraftServerPinger _pinger = new();
    private readonly OverridesInstaller _overrides = new();
    private readonly PlayerProfile _player;
    private readonly MainWindowViewModel _shell;
    private bool _acceptProgress;

    private CancellationTokenSource? _launchCts;

    [ObservableProperty] private double _launchProgress;
    [ObservableProperty] private string _launchStatus = "Pronto para jogar";

    /// <summary>Há uma versão mais recente do modpack oficial no servidor.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateVersionsLabel))]
    [NotifyCanExecuteChangedFor(nameof(UpdateModpackCommand))]
    private bool _updateAvailable;

    /// <summary>Versão disponível no servidor (para o rótulo do botão de atualizar).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateVersionsLabel))]
    private string _availableVersion = string.Empty;

    /// <summary>Transição de versões para o banner (ex.: "v1.0 → v1.1").</summary>
    public string UpdateVersionsLabel =>
        string.IsNullOrWhiteSpace(Active?.ManifestVersion)
            ? $"Nova versão v{AvailableVersion}"
            : $"v{Active!.ManifestVersion} → v{AvailableVersion}";

    public HomePageViewModel(PlayerProfile player, GameProfile game, MainWindowViewModel shell)
    {
        _player = player;
        _game = game;
        _shell = shell;

        LaunchStatus = DefaultStatus();

        RebuildServers();
        _ = ServerStatusLoopAsync();
        RefreshUpdateState();
    }

    /// <summary>
    ///     Verifica (em segundo plano) se a instância oficial ativa tem uma versão mais
    ///     recente no servidor. Chamado ao mudar de instância e ao abrir a tela "Jogar".
    /// </summary>
    public void RefreshUpdateState() => _ = CheckModpackUpdateAsync();

    private async Task CheckModpackUpdateAsync()
    {
        UpdateAvailable = false;

        var instance = Active;
        if (instance is null || !instance.IsOfficial || instance.ModpackId is null) return;
        if (!_shell.Manifest.IsConfigured) return;

        try
        {
            var list = await _shell.Manifest.GetModpacksAsync();
            var match = list.FirstOrDefault(m => m.Id == instance.ModpackId);
            if (match is not null && match.Version != instance.ManifestVersion)
            {
                AvailableVersion = match.Version;
                UpdateAvailable = true;
            }
        }
        catch
        {
            // Servidor offline / sem rede — simplesmente não mostra atualização.
        }
    }

    private bool CanUpdateModpack() => UpdateAvailable && !IsLaunching && !_shell.IsGameRunning;

    [RelayCommand(CanExecute = nameof(CanUpdateModpack))]
    private async Task UpdateModpack()
    {
        var instance = Active;
        if (instance?.ModpackId is null) return;

        try
        {
            var full = await _shell.Manifest.GetManifestAsync(instance.ModpackId);
            if (full is null) return;

            // Atualiza os metadados da instância (mods/overrides nova versão); o
            // download acontece ao Jogar. InstallFromManifest reseleciona a instância,
            // o que dispara NotifyInstanceChanged → RefreshUpdateState.
            _shell.InstallFromManifest(full);
            UpdateAvailable = false;
        }
        catch (Exception ex)
        {
            LaunchStatus = "Erro ao atualizar: " + ex.Message;
        }
    }

    /// <summary>Estado (online/offline + jogadores + MOTD) de cada servidor do modpack.</summary>
    public ObservableCollection<ServerStatusItem> Servers { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    [NotifyCanExecuteChangedFor(nameof(UpdateModpackCommand))]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    public partial bool IsLaunching { get; set; }

    private MinecraftInstance? Active => _shell.ActiveInstance;

    // ── Hero (derivado da instância ativa) ───────────────────────
    public string InstanceName => Active?.Name ?? "—";
    public string InstanceTag => Active?.SourceLabel ?? "";

    // Subtítulo = versão (com a versão do modpack ao lado do MC).
    public string InstanceSubtitle => Active?.VersionSummary ?? "";

    // Resumo = descrição vinda do servidor; senão, uma etiqueta genérica.
    public string InstanceSummary => Active is null
        ? string.Empty
        : !string.IsNullOrWhiteSpace(Active.Description)
            ? Active.Description!
            : Active.IsOfficial
                ? "Modpack oficial do servidor TCMine"
                : "Instância personalizada";

    // ── Estado de instalação / execução ──────────────────────────
    public bool IsInstalled => Active is not null && _shell.IsInstanceInstalled(Active);

    public string PlayLabel => _shell.IsGameRunning
        ? "EM EXECUÇÃO"
        : IsInstalled
            ? "JOGAR"
            : "INSTALAR";

    public string PlayIcon => _shell.IsGameRunning ? "■" : IsInstalled ? "▶" : "⬇";

    /// <summary>Cor do indicador de estado (verde/âmbar/azul/roxo).</summary>
    public string StatusColor =>
        _shell.IsGameRunning ? "#3B82F6" :
        IsLaunching ? "#A855F7" :
        IsInstalled ? "#22C55E" :
        "#F59E0B";

    // ── Servidor da instância ────────────────────────────────────
    public bool HasServer => Servers.Count > 0;

    public ObservableCollection<string> LaunchLog { get; } = new();

    // ── Perfil ───────────────────────────────────────────────────
    public string PlayerName => _player.Name;
    public string AvatarInitials => _player.ComputeInitials();
    public string AccountLabel => _player.AccountLabel;

    /// <summary>URL da cabeça da skin (para o avatar).</summary>
    public string? PlayerHeadUrl => _player.HeadUrl;

    /// <summary>Id (abreviado) da instância ativa — mostrado no painel; clica abre a pasta.</summary>
    public string InstanceIdShort => Active is null
        ? "—"
        : Active.Id.Length > 30
            ? Active.Id[..30] + "…"
            : Active.Id;

    public void NotifyPlayerChanged()
    {
        OnPropertyChanged(nameof(PlayerName));
        OnPropertyChanged(nameof(AvatarInitials));
        OnPropertyChanged(nameof(AccountLabel));
        OnPropertyChanged(nameof(PlayerHeadUrl));
    }

    public void NotifyInstanceChanged()
    {
        OnPropertyChanged(nameof(InstanceName));
        OnPropertyChanged(nameof(InstanceTag));
        OnPropertyChanged(nameof(InstanceSubtitle));
        OnPropertyChanged(nameof(InstanceSummary));
        OnPropertyChanged(nameof(InstanceIdShort));
        RefreshInstallState();

        if (!IsLaunching) LaunchStatus = DefaultStatus();

        RebuildServers();
        _ = RefreshServersAsync();
        RefreshUpdateState();
    }

    public void NotifyGameRunningChanged()
    {
        OnPropertyChanged(nameof(PlayLabel));
        OnPropertyChanged(nameof(PlayIcon));
        OnPropertyChanged(nameof(StatusColor));
        PlayCommand.NotifyCanExecuteChanged();
        UpdateModpackCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void OpenInstanceFolder()
    {
        if (Active is not null) _shell.OpenInstanceFolder(Active);
    }

    private void RefreshInstallState()
    {
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(PlayLabel));
        OnPropertyChanged(nameof(PlayIcon));
        OnPropertyChanged(nameof(StatusColor));
    }

    private string DefaultStatus()
    {
        if (_shell.IsGameRunning) return "Minecraft em execução";
        return IsInstalled ? "Pronto para jogar" : "Instância por instalar";
    }

    // ── Ping periódico dos servidores ────────────────────────────
    private void RebuildServers()
    {
        Servers.Clear();
        if (Active is not null)
        {
            // Com um único servidor, entra nele automaticamente por defeito.
            if (Active.Servers.Count == 1 && string.IsNullOrEmpty(Active.AutoJoinServerName))
            {
                Active.AutoJoinServerName = Active.Servers[0].Name;
                _shell.SaveInstance(Active);
            }

            foreach (var server in Active.Servers)
                Servers.Add(new ServerStatusItem(server, OnToggleAutoJoin)
                {
                    IsAutoJoin = server.Name == Active.AutoJoinServerName
                });
        }

        OnPropertyChanged(nameof(HasServer));
    }

    /// <summary>
    ///     Alterna o servidor de entrada automática (comportamento "rádio": só um ativo,
    ///     e clicar no que já está ativo desliga — passa a abrir no menu principal).
    /// </summary>
    private void OnToggleAutoJoin(ServerStatusItem item)
    {
        if (Active is null) return;
        var turnOn = !item.IsAutoJoin;
        foreach (var s in Servers) s.IsAutoJoin = false;
        item.IsAutoJoin = turnOn;
        Active.AutoJoinServerName = turnOn ? item.Server.Name : null;
        _shell.SaveInstance(Active);
    }

    private async Task ServerStatusLoopAsync()
    {
        while (true)
        {
            await RefreshServersAsync();
            await Task.Delay(TimeSpan.FromSeconds(30));
        }
    }

    private async Task RefreshServersAsync()
    {
        foreach (var item in Servers.ToList())
        {
            var status = await _pinger.PingAsync(item.Server.Address, item.Server.Port);

            // O item pode ter saído da lista durante o ping (troca de instância).
            if (!Servers.Contains(item)) continue;

            if (status.Online)
            {
                item.StatusText = $"Online · {status.PlayersOnline}/{status.PlayersMax}";
                item.StatusColor = "#22C55E";
                item.Motd = status.Motd;
            }
            else
            {
                item.StatusText = "Offline";
                item.StatusColor = "#EF4444";
                item.Motd = string.Empty;
            }
        }
    }

    private bool CanPlay()
    {
        return !IsLaunching && !_shell.IsGameRunning;
    }

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task PlayAsync()
    {
        var session = _shell.CurrentSession;
        if (session is null)
        {
            LaunchStatus = "Sessão inválida — faz login novamente.";
            return;
        }

        var instance = Active;
        if (instance is null)
        {
            LaunchStatus = "Nenhuma instância selecionada.";
            return;
        }

        IsLaunching = true;
        LaunchProgress = 0;
        LaunchLog.Clear();
        LaunchLog.Add($"Instância: {instance.Name} ({instance.VersionSummary})");
        _launchCts = new CancellationTokenSource();
        _acceptProgress = true;
        Log.Information("A lançar instância {Name} ({Mc}/{Neo})",
            instance.Name, instance.MinecraftVersion, instance.NeoForgeVersion);

        // Progress<T> entrega de forma assíncrona; a flag evita que updates atrasados
        // do download sobrescrevam o estado depois do jogo arrancar.
        var progress = new Progress<LaunchProgress>(p =>
        {
            if (!_acceptProgress) return;
            LaunchProgress = p.Percent;
            LaunchStatus = p.Message;
            LaunchLog.Add($"[{p.Percent,3:0}%] {p.Message}");
            _shell.SetBusy(p.IsActive, p.Percent, p.Message);
        });

        try
        {
            var autoJoin = instance.Servers
                .FirstOrDefault(s => s.Name == instance.AutoJoinServerName);

            var process = await _launcher.PrepareAsync(
                LauncherPaths.InstanceGameDir(instance.Id),
                instance.MinecraftVersion,
                instance.NeoForgeVersion,
                session,
                _shell.ClampRam(instance.RamOverrideMb ?? _game.AllocatedRamMb),
                string.IsNullOrWhiteSpace(instance.JavaPathOverride) ? _game.JavaPath : instance.JavaPathOverride,
                progress,
                _launchCts.Token,
                instance.Servers,
                autoJoin);

            // Com overrides não fazemos prune (eles podem trazer jars próprios).
            await _shell.ModInstaller.EnsureModsAsync(
                instance, progress, _launchCts.Token, instance.IsOfficial && !instance.HasOverrides);

            // Aplica o bundle de overrides do modpack (configs/resourcepacks/options),
            // uma vez por versão. Sobreposto por cima dos mods já instalados.
            ((IProgress<LaunchProgress>)progress).Report(new LaunchProgress(
                LaunchState.DownloadingAssets, 100, "A aplicar configuração do modpack..."));
            await _overrides.EnsureAsync(instance, _game.ServerUrl, _launchCts.Token);

            // Ignora updates de progresso atrasados (a partir daqui o estado é o do jogo).
            _acceptProgress = false;

            // Captura a saída do jogo para ficheiro (e deteção de crash).
            var logCapture = new GameLogCapture(LauncherPaths.InstanceLogFile(instance.Id));
            process.OutputDataReceived += (_, e) => logCapture.Append(e.Data);
            process.ErrorDataReceived += (_, e) => logCapture.Append(e.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            instance.LastPlayedAt = DateTimeOffset.Now;
            _shell.SaveInstance(instance);

            _shell.IsGameRunning = true;
            _shell.MarkGameStarted(instance.Id, process.Id);
            LaunchStatus = "Minecraft em execução";
            LaunchLog.Add("Minecraft iniciado.");

            _ = MonitorGameAsync(process, logCapture);
        }
        catch (OperationCanceledException)
        {
            LaunchStatus = "Launch cancelado";
            LaunchLog.Add("Launch cancelado pelo utilizador.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falha no launch da instância {Name}", instance.Name);
            LaunchStatus = "Falha no launch";
            LaunchLog.Add("ERRO: " + ex.Message);
        }
        finally
        {
            _launchCts?.Dispose();
            _launchCts = null;
            LaunchProgress = 0;
            IsLaunching = false;
            _shell.SetBusy(false, 0, "Pronto");
            RefreshInstallState();
        }
    }

    private async Task MonitorGameAsync(Process process, GameLogCapture logCapture)
    {
        var exitCode = 0;
        try
        {
            await process.WaitForExitAsync();
            exitCode = process.ExitCode;
        }
        catch
        {
            // ignora — o importante é reativar a UI a seguir
        }

        _shell.IsGameRunning = false;
        _shell.MarkGameStopped();

        if (exitCode != 0)
        {
            Log.Warning("Minecraft terminou com erro (código {Code})", exitCode);
            LaunchStatus = "Minecraft terminou com erro";
            LaunchLog.Add($"⚠ Saída com código {exitCode}. Log: {logCapture.LogPath}");
            foreach (var line in logCapture.Tail()) LaunchLog.Add(line);
            _shell.ShowLog();
        }
        else
        {
            LaunchLog.Add("Minecraft fechado.");
            LaunchStatus = DefaultStatus();
        }

        RefreshInstallState();
        logCapture.Dispose();

        try
        {
            process.Dispose();
        }
        catch
        {
            /* noop */
        }
    }

    [RelayCommand]
    private void CancelLaunch()
    {
        _launchCts?.Cancel();
    }
}

/// <summary>Estado de um servidor do modpack (para a lista na tela principal).</summary>
public partial class ServerStatusItem : ViewModelBase
{
    private readonly Action<ServerStatusItem>? _onToggleAutoJoin;

    [ObservableProperty] private string _motd = string.Empty;
    [ObservableProperty] private string _statusColor = "#6B7280";
    [ObservableProperty] private string _statusText = "A verificar...";

    /// <summary>Este servidor é o de entrada automática ao iniciar o jogo.</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(AutoJoinLabel))]
    private bool _isAutoJoin;

    public string AutoJoinLabel => IsAutoJoin
        ? "Entra automaticamente neste servidor ao iniciar (clica para desligar)"
        : "Entrar automaticamente neste servidor ao iniciar o jogo";

    public ServerStatusItem(ServerEntry server, Action<ServerStatusItem>? onToggleAutoJoin = null)
    {
        Server = server;
        Name = server.Name;
        _onToggleAutoJoin = onToggleAutoJoin;
    }

    public ServerEntry Server { get; }
    public string Name { get; }

    [RelayCommand]
    private void ToggleAutoJoin() => _onToggleAutoJoin?.Invoke(this);
}