using System;
using System.IO;

namespace BimboClub
{
    public static class Logger
    {
        private static string GetLogPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "BimboClub", "Logs");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, "BimboClubTools.log");
        }

        public static void Log(string message, string level = "INFO")
        {
            try
            {
                string path = GetLogPath();
                string formattedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                
                lock (typeof(Logger))
                {
                    File.AppendAllText(path, formattedMessage + Environment.NewLine);
                }
            }
            catch { }
        }

        public static void LogError(string message, Exception ex)
        {
            Log($"{message} | Exception: {ex?.Message}\nStackTrace: {ex?.StackTrace}", "ERROR");
        }
    }
}
