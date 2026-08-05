using System.Text.Json;
using System.Text.Json.Serialization;
using NotchFlow.Core.Logging;

namespace NotchFlow.Core.Settings;

/// <summary>Forma serializada das preferências. Separada da classe observável para o
/// arquivo em disco não depender da mecânica de notificação.</summary>
public sealed class AppSettingsData
{
    [JsonPropertyName("minimizeOnSecondaryDisplays")]
    public bool MinimizeOnSecondaryDisplays { get; set; } = true;

    [JsonPropertyName("showTrayIcon")]
    public bool ShowTrayIcon { get; set; } = true;

    [JsonPropertyName("showOnAllDisplays")]
    public bool ShowOnAllDisplays { get; set; } = true;
}

/// <summary>
/// Preferências do usuário. Equivalente ao AppSettings sobre UserDefaults da versão macOS,
/// gravando um JSON em %APPDATA%\NotchFlow. Nenhum dado de mídia é persistido.
/// </summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string? _path;
    private readonly AppSettingsData _data;

    public event EventHandler? Changed;

    /// <summary>O ícone da bandeja tem um evento próprio porque quem reage é o host do
    /// aplicativo, não a geometria das janelas.</summary>
    public event EventHandler<bool>? TrayIconVisibilityChanged;

    public AppSettings(string? path = null)
    {
        _path = path ?? ResolveDefaultPath();
        _data = Load(_path);
    }

    /// <summary>Nas telas secundárias a ilha fica reduzida a uma tira.</summary>
    public bool MinimizeOnSecondaryDisplays
    {
        get => _data.MinimizeOnSecondaryDisplays;
        set => Update(v => _data.MinimizeOnSecondaryDisplays = v, value, _data.MinimizeOnSecondaryDisplays);
    }

    /// <summary>Diferente do macOS, aqui o ícone da bandeja vem ligado: no Windows é o
    /// único ponto de acesso óbvio para um aplicativo sem janela.</summary>
    public bool ShowTrayIcon
    {
        get => _data.ShowTrayIcon;
        set
        {
            if (_data.ShowTrayIcon == value)
            {
                return;
            }

            _data.ShowTrayIcon = value;
            Save();
            TrayIconVisibilityChanged?.Invoke(this, value);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool ShowOnAllDisplays
    {
        get => _data.ShowOnAllDisplays;
        set => Update(v => _data.ShowOnAllDisplays = v, value, _data.ShowOnAllDisplays);
    }

    private void Update(Action<bool> apply, bool value, bool current)
    {
        if (current == value)
        {
            return;
        }

        apply(value);
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static AppSettingsData Load(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return new AppSettingsData();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettingsData>(json) ?? new AppSettingsData();
        }
        catch (Exception ex)
        {
            // Preferências corrompidas voltam ao padrão em vez de impedir a abertura.
            AppLog.Lifecycle.Error($"Preferências ilegíveis, usando padrões: {ex.GetType().Name}");
            return new AppSettingsData();
        }
    }

    private void Save()
    {
        if (_path is null)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, JsonSerializer.Serialize(_data, SerializerOptions));
        }
        catch (Exception ex)
        {
            AppLog.Lifecycle.Error($"Falha ao gravar preferências: {ex.GetType().Name}");
        }
    }

    private static string? ResolveDefaultPath()
    {
        try
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NotchFlow",
                "settings.json");
        }
        catch (Exception)
        {
            return null;
        }
    }
}
