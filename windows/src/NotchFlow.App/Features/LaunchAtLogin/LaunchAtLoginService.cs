using Microsoft.Win32;
using NotchFlow.Core.Logging;

namespace NotchFlow.App.Features.LaunchAtLogin;

/// <summary>
/// Inicialização junto com o Windows. Equivale ao LaunchAtLoginService da versão macOS,
/// que usava o ServiceManagement.
///
/// Usa a chave Run do usuário atual: não exige privilégio de administrador e não instala
/// serviço nem tarefa agendada, mantendo a promessa de não mexer no sistema.
/// </summary>
public sealed class LaunchAtLoginService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NotchFlow";

    private readonly string _executablePath;

    public LaunchAtLoginService(string? executablePath = null)
        => _executablePath = executablePath ?? Environment.ProcessPath ?? string.Empty;

    public bool CanBeConfigured => !string.IsNullOrEmpty(_executablePath) && File.Exists(_executablePath);

    public bool IsEnabled
    {
        get
        {
            if (!CanBeConfigured)
            {
                return false;
            }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                var value = key?.GetValue(ValueName) as string;
                if (string.IsNullOrEmpty(value))
                {
                    return false;
                }

                // O valor é gravado entre aspas; comparar sem elas evita falso negativo.
                return value.Trim('"').Equals(_executablePath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                AppLog.Lifecycle.Error($"Falha ao ler o início automático: {ex.GetType().Name}");
                return false;
            }
        }
    }

    public bool SetEnabled(bool enabled)
    {
        if (!CanBeConfigured)
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return false;
            }

            if (enabled)
            {
                // As aspas são obrigatórias: sem elas um caminho com espaços é lido
                // como programa e argumentos separados.
                key.SetValue(ValueName, $"\"{_executablePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            AppLog.Lifecycle.Info($"Início automático {(enabled ? "ativado" : "desativado")}");
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Lifecycle.Error($"Falha ao alterar o início automático: {ex.GetType().Name}");
            return false;
        }
    }

    public void Toggle() => SetEnabled(!IsEnabled);
}
