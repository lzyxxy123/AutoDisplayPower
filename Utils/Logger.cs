using System;
using System.IO;

namespace AutoDisplayPower.Utils;

/// <summary>
/// 极简文件日志，写入 %LOCALAPPDATA%\AutoDisplayPower\app.log。
/// 日志失败绝不阻塞主流程。
/// </summary>
public static class Logger
{
    private const long MaxBytes = 2 * 1024 * 1024; // 超过 2MB 滚动为 .old

    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoDisplayPower");

    private static readonly string FilePath = Path.Combine(Dir, "app.log");
    private static readonly object Sync = new();

    public static void Info(string msg) => Write("INFO", msg);
    public static void Warn(string msg) => Write("WARN", msg);
    public static void Error(string msg, Exception? ex = null)
        => Write("ERROR", msg + (ex is null ? "" : $" | {ex.GetType().Name}: {ex.Message}"));

    public static string LogFilePath => FilePath;

    private static void Write(string level, string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Dir);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
                {
                    try { File.Copy(FilePath, FilePath + ".old", true); } catch { /* 忽略滚动失败 */ }
                    try { File.Delete(FilePath); } catch { /* 忽略 */ }
                }

                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}\r\n");
            }
        }
        catch
        {
            // 日志不可用时不抛异常
        }
    }
}
