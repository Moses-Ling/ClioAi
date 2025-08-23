using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace AudioTranscriptionApp
{
    public static class Logger
    {
        private static readonly string LogFilePath;
        private static readonly object LockObject = new object(); // For thread safety
        private const long MaxLogBytes = 5L * 1024 * 1024; // 5 MB
        private const int MaxLogBackups = 2; // app.log.1, app.log.2

        static Logger()
        {
            try
            {
                // Log file next to the executable
                string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                LogFilePath = Path.Combine(exePath ?? Environment.CurrentDirectory, "app.log");

                // Optional: Add header for new session
                Log("INFO", $"--- Log Session Started: {DateTime.Now} ---");
            }
            catch (Exception ex)
            {
                // Fallback or handle error where logging cannot be initialized
                Console.WriteLine($"FATAL: Could not initialize logger: {ex.Message}");
                LogFilePath = null; // Disable logging if path fails
            }
        }

        public static void Info(string message)
        {
            Log("INFO", message);
        }

        public static void Warning(string message)
        {
            Log("WARN", message);
        }

        public static void Error(string message, Exception ex = null)
        {
            string errorMessage = message;
            if (ex != null)
            {
                errorMessage += $"\nException: {ex.GetType().Name} - {ex.Message}\nStackTrace: {ex.StackTrace}";
            }
            Log("ERROR", errorMessage);
        }

        private static void Log(string level, string message)
        {
            if (LogFilePath == null) return; // Logging disabled

            try
            {
                // Use lock for basic thread safety when writing to the file
                lock (LockObject)
                {
                    RotateIfNeeded();
                    // Append log entry to the file
                    using (StreamWriter writer = File.AppendText(LogFilePath))
                    {
                        int tid = Thread.CurrentThread.ManagedThreadId;
                        writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [T{tid}] {message}");
                    }
                }
            }
            catch (Exception logEx)
            {
                // Handle potential errors during logging itself (e.g., disk full, permissions)
                Console.WriteLine($"Logging Error ({level}): {message}. Failed to write to log file: {logEx.Message}");
            }
        }

        private static void RotateIfNeeded()
        {
            try
            {
                if (!File.Exists(LogFilePath)) return;
                var info = new FileInfo(LogFilePath);
                if (info.Length < MaxLogBytes) return;

                // Simple rolling scheme: app.log -> app.log.1 -> app.log.2
                string dir = Path.GetDirectoryName(LogFilePath) ?? Environment.CurrentDirectory;
                string baseName = Path.GetFileName(LogFilePath);
                string backup1 = Path.Combine(dir, baseName + ".1");
                string backup2 = Path.Combine(dir, baseName + ".2");

                // Delete oldest
                if (File.Exists(backup2)) File.Delete(backup2);
                // Shift 1->2
                if (File.Exists(backup1)) File.Move(backup1, backup2);
                // Current -> 1
                File.Move(LogFilePath, backup1);
                // New log file will be created on next write
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Logging rotation error: {ex.Message}");
            }
        }
    }
}
