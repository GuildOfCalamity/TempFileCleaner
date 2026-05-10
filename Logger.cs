using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TempFileCleaner
{
    public enum LogLevel     
    {
        None    = 0,
        Verbose = 1,
        Info    = 2,
        Warning = 4,
        Error   = 8
    }

    public static class Logger
    {
        #region [Properties]
        public static string LogDrive { get; private set; } = string.Empty;
        static object fileLock = new object();
        static object driveLock = new object();
        static readonly int minimumWait = 5; // in milliseconds
        #endregion

        public static event Action<Exception> OnException;

        public static bool WriteInfo(string message) => Write(App.Title, message, LogLevel.Info);

        public static bool WriteWarn(string message) => Write(App.Title, message, LogLevel.Warning);
        
        public static bool WriteError(string message) => Write(App.Title, message, LogLevel.Warning);

        public static bool Write(string application, string message, LogLevel level, bool insertTimeStamp = true)
        {
            int maxTries = minimumWait;
            try
            {
                lock (driveLock)
                {
                    if (string.IsNullOrEmpty(LogDrive))
                    {
                        DriveInfo[] info = DriveInfo.GetDrives();
                        if (info.Any(i => (i.DriveType == System.IO.DriveType.Fixed) && (i.IsReady) && (i.Name == @"D:\")))
                            LogDrive = @"D:\Logs";
                        else if (info.Any(i => (i.DriveType == System.IO.DriveType.Fixed) && (i.IsReady) && (i.Name == @"C:\")))
                            LogDrive = @"C:\Logs";
                        else if (info.Any(i => (i.DriveType == System.IO.DriveType.Fixed) && (i.IsReady) && (i.Name == @"E:\")))
                            LogDrive = @"E:\Logs";
                        else if (info.Any(i => (i.DriveType == System.IO.DriveType.Fixed) && (i.IsReady) && (i.Name == @"F:\")))
                            LogDrive = @"F:\Logs";
                    }
                }


                string path = String.Format(@"{0}\{2}\{5}\{3}-{4}\", 
                    LogDrive, 
                    "", 
                    application,
                    DateTime.Now.Month.ToString("00"), 
                    DateTime.Now.Date.ToString("MMMM"), 
                    DateTime.Now.Year.ToString("0000"));


                DirectoryInfo dInfo = new DirectoryInfo(path);
                lock (fileLock)
                {
                    if (!dInfo.Exists)
                    {
                        dInfo.Create();
                    }
                    dInfo = null;
                    path += application + "_" + DateTime.Now.ToString("dd") + ".log";
                    Debug.WriteLine($"[LOG] {message}");

                    while (IsFileLocked(new FileInfo(path)) && --maxTries > 0)
                        Thread.Sleep(minimumWait);

                    using (StreamWriter writer = new StreamWriter(path, true))
                    {
                        if (insertTimeStamp)
                        {
                            DateTime date = DateTime.Now;
                            string value = "[" + date.ToString("MM/dd/yyyy hh:mm:ss.fff tt") + "]";
                            value += " {" + message + "}";
                            writer.WriteLine(value);
                        }
                        else
                        {
                            writer.WriteLine(message);
                        }
                    }
                }
                return true;
            }
            catch (Exception ex) /* typically permission or file locked issue */
            {
                Debug.WriteLine($"[ERROR] {MethodBase.GetCurrentMethod()?.DeclaringType?.Namespace}.{MethodBase.GetCurrentMethod()?.Name}: {ex.Message}");
                OnException?.Invoke(ex);
                return false;
            }
        }

        public static async Task<bool> WriteAsync(string application, string message, bool insertTimeStamp = true)
        {
            int maxTries = minimumWait;
            try
            {
                if (string.IsNullOrEmpty(LogDrive))
                {
                    DriveInfo[] info = DriveInfo.GetDrives();
                    if (info.Any(i => (i.DriveType == System.IO.DriveType.Fixed) && (i.IsReady) && (i.Name == @"D:\")))
                        LogDrive = @"D:\Logs";
                    else if (info.Any(i => (i.DriveType == System.IO.DriveType.Fixed) && (i.IsReady) && (i.Name == @"C:\")))
                        LogDrive = @"C:\Logs";
                    else if (info.Any(i => (i.DriveType == System.IO.DriveType.Fixed) && (i.IsReady) && (i.Name == @"E:\")))
                        LogDrive = @"E:\Logs";
                    else if (info.Any(i => (i.DriveType == System.IO.DriveType.Fixed) && (i.IsReady) && (i.Name == @"F:\")))
                        LogDrive = @"F:\Logs";
                }


                string path = String.Format(@"{0}\{2}\{5}\{3}-{4}\",
                    LogDrive,
                    "",
                    application,
                    DateTime.Now.Month.ToString("00"),
                    DateTime.Now.Date.ToString("MMMM"),
                    DateTime.Now.Year.ToString("0000"));


                DirectoryInfo dInfo = new DirectoryInfo(path);
                if (!dInfo.Exists)
                {
                    dInfo.Create();
                }
                dInfo = null;
                path += application + "_" + DateTime.Now.ToString("dd") + ".log";
                Debug.WriteLine($"[LOG] {message}");

                while (IsFileLocked(new FileInfo(path)) && --maxTries > 0)
                    await Task.Delay(minimumWait);

                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    if (insertTimeStamp)
                    {
                        DateTime date = DateTime.Now;
                        string value = "[" + date.ToString("MM/dd/yyyy hh:mm:ss.fff tt") + "]";
                        value += " {" + message + "}";
                        await writer.WriteLineAsync(value);
                    }
                    else
                    {
                        await writer.WriteLineAsync(message);
                    }
                }
                return true;
            }
            catch (Exception ex) /* typically permission or file locked issue */
            {
                Debug.WriteLine($"[ERROR] {MethodBase.GetCurrentMethod()?.DeclaringType?.Namespace}.{MethodBase.GetCurrentMethod()?.Name}: {ex.Message}");
                OnException?.Invoke(ex);
                return false;
            }
        }

        public static string GetCurrentLogPath() => Path.Combine(LogDrive, $@"{App.Title}\{DateTime.Today.Year}\{DateTime.Today.Month.ToString("00")}-{DateTime.Today.ToString("MMMM")}\{App.Title}_{DateTime.Now.ToString("dd")}.log");

        /// <summary>
        /// Provides a virtual method to determine if a file is being accessed by another thread.
        /// </summary>
        /// <param name="file"><see cref="FileInfo"/></param>
        /// <returns>true if file is in use, false otherwise</returns>
        public static bool IsFileLocked(FileInfo file)
        {
            FileStream stream = null;
            try
            {
                if (!File.Exists(file.FullName))
                    return false;

                stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) // still being written to or being accessed by another process 
            {
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                    stream = null;
                }
            }
            return false; // file is not locked
        }

        /// <summary>
        /// Tests <paramref name="path"/> to determine if it has too many characters.
        /// </summary>
        public static bool IsPathTooLong(string path)
        {
            try
            {
                var tmp = Path.GetFullPath(path);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
            catch (PathTooLongException)
            {
                return true;
            }
        }

        /// <summary>
        /// Get rid of older log files.
        /// </summary>
        /// <param name="age">age of oldest files to remove (in days)</param>
        /// <param name="ext">file extension to look for</param>
        public static void PurgeOldLogs(int age = 180, string ext = "*.log")
        {
            new Thread(() => { PerformPurge(Path.GetDirectoryName(GetCurrentLogPath()), age, ext); })
            { IsBackground = true, Name = "PurgeLogs", Priority = ThreadPriority.Lowest }.Start();
        }

        /// <summary>
        /// This method should be called by a lower thread.
        /// </summary>
        static void PerformPurge(string location, int age, string ext)
        {
            try
            {
                string lastFilePath = "";
                string[] logFiles = Directory.GetFiles(location, ext, SearchOption.AllDirectories);
                // Only remove 50K files per round.
                IEnumerable<string> topFiles = logFiles.OrderBy(files => files).Take(50000);
                foreach (string fn in topFiles)
                {
                    if (IsPathTooLong(fn))
                        continue;

                    lastFilePath = fn;
                    DateTime dtOfLog = System.IO.File.GetCreationTime(fn);
                    if ((DateTime.Now - dtOfLog).TotalDays > age)
                    {
                        try { File.Delete(fn); }
                        catch (Exception ex) { OnException?.Invoke(new Exception($"DeleteFile: {ex.Message}")); }
                    }
                    Thread.Sleep(1); // Relax this loop.
                }

                if (!string.IsNullOrEmpty(lastFilePath))
                {   // Check if the folder empty now.
                    if (Directory.GetFiles(Path.GetDirectoryName(lastFilePath)).Length < 1) // Remove folder if no more files exist.
                    {
                        try
                        {
                            Write(App.Title, $"Log purge will delete this directory '{Path.GetDirectoryName(lastFilePath)}'", LogLevel.Verbose);
                            Directory.Delete(Path.GetDirectoryName(lastFilePath), true);
                        }
                        catch (Exception ex) { OnException?.Invoke(new Exception($"DeleteFolder: {ex.Message}")); }
                    }
                }
            }
            catch (Exception ex)
            {
                OnException?.Invoke(new Exception($"PurgeOldLogs: {ex.Message}"));
            }
        }
    }
}
