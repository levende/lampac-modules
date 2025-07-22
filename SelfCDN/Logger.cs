using System;
using System.IO;

namespace SelfCDN
{
    public class Logger
    {
        private static readonly string LogDirectory = ModInit.ModulePath + "/Logs";
        private static readonly object LockObject = new object();

        static Logger()
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
        }

        public static void Log(string message)
        {
            if (!ModInit.ModuleSettings.IsLogEnabled == true)
            {
                return;
            }

            try
            {
                string fileName = $"log_{DateTime.Now:yyyy-MM-dd}.txt";
                string fullPath = Path.Combine(LogDirectory, fileName);
                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}";

                lock (LockObject)
                {
                    File.AppendAllText(fullPath, logMessage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }

        public static void Log(Func<string> messageFunc)
        {
            if (!ModInit.ModuleSettings.IsLogEnabled == true)
            {
                return;
            }

            Log(messageFunc());
        }
    }
}
