using System.Diagnostics;
using System.Text;

namespace NotchFlow.Core.Logging;

/// <summary>
/// Log local por categoria, equivalente ao OSLog usado na versão macOS.
///
/// Nunca registra conteúdo: nem metadados de mídia, nem remetente ou texto de notificação.
/// Só eventos do próprio aplicativo, para o arquivo não virar um histórico do que o usuário
/// ouviu ou de quem lhe escreveu.
/// </summary>
public sealed class AppLog
{
    public static readonly AppLog Lifecycle = new("lifecycle");
    public static readonly AppLog Media = new("media");
    public static readonly AppLog Window = new("window");
    public static readonly AppLog Calendar = new("calendar");
    public static readonly AppLog Notifications = new("notifications");

    private const long MaxFileBytes = 512 * 1024;

    private static readonly object Gate = new();
    private static readonly Lazy<string?> LogPath = new(ResolveLogPath);

    private readonly string _category;

    private AppLog(string category) => _category = category;

    public void Info(string message) => Write("INFO", message);

    public void Error(string message) => Write("ERRO", message);

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {_category}: {message}";
        Debug.WriteLine(line);

        var path = LogPath.Value;
        if (path is null)
        {
            return;
        }

        try
        {
            lock (Gate)
            {
                // Truncar em vez de rotacionar mantém o log limitado sem acumular arquivos.
                if (File.Exists(path) && new FileInfo(path).Length > MaxFileBytes)
                {
                    File.Delete(path);
                }

                File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch (Exception)
        {
            // Um log que falha não pode derrubar o aplicativo.
        }
    }

    private static string? ResolveLogPath()
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NotchFlow");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "notchflow.log");
        }
        catch (Exception)
        {
            return null;
        }
    }
}
