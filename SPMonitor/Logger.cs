using Jil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SPMonitor;

namespace SPMonitor
{
    public static class Logger
    {
        public static string LogLocation { get; set; } = null;
        public static List<LogEntry> Log { get; set; } = new List<LogEntry>();
        static Logger()
        {
            Task.Run(() => Initialize());
            Task.WaitAll();

        }

        public static async Task Initialize()
        {
            var currentDirectory = new DirectoryInfo(Environment.CurrentDirectory);
            var logFileName = $"SPMonitor{DateTime.Now.ToString("MMddyyyy")}.log";
            LogLocation = Path.Combine(currentDirectory.FullName, logFileName);
            var logFile = currentDirectory.GetFiles().FirstOrDefault(f => f.Name == logFileName);

            if (logFile != null)
            {
                await ReadLogFileFromDisk(logFile);
                Task.WaitAll();
            }
        }


        public static async Task ReadLogFileFromDisk(FileInfo log)
        {
            await Task.Run(async () =>
            {
                StreamReader reader = null;
                try
                {
                    reader = new StreamReader(log.FullName, Encoding.UTF8);
                    var fileContents = await reader.ReadToEndAsync();

                    if (fileContents != null)
                    {
                        Log = JSON.Deserialize<List<LogEntry>>(fileContents);
                        await Logger.LogInfo($"Loaded {Log.Count()} log entries from disk.");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    if (reader != null)
                    {
                        reader.Close();
                        reader.Dispose();
                        reader = null;
                    }
                }
            });
        }
        public static async Task WriteLogFileToDisk()
        {
            FileStream stream = null;
            StreamWriter writer = null;
            try
            {
                stream = new FileStream(LogLocation, FileMode.OpenOrCreate);
                writer = new StreamWriter(stream, Encoding.UTF8);
                await writer.WriteAsync(JSON.Serialize<List<LogEntry>>(Log));
                await writer.FlushAsync();
            }
            finally
            {
                if (writer != null)
                {
                    writer.Close();
                    writer.Dispose();
                    writer = null;
                }
            }
        }

        public static async Task LogError(string textToLog, string header = "Error")
        {
            await Task.Run(() =>
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.DarkRed;
                Console.Write($"[{header}]");
                Console.ResetColor();
                Console.Write("  ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(textToLog);

            });
        }

        public static async Task LogWarning(string textToLog, string header = "Warning")
        {
            await Task.Run(() =>
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.DarkYellow;
                Console.Write($"[{header}]");
                Console.ResetColor();
                Console.Write("  ");
                Console.WriteLine(textToLog);
            });
        }
        public static async Task LogInfo(string textToLog, string header = "Info")
        {
            await Task.Run(() =>
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.DarkBlue;
                Console.Write($"[{header}]");
                Console.ResetColor();
                Console.Write("  ");
                Console.WriteLine(textToLog);
            });
        }
    }
}
