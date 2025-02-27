using System.Collections.Generic;
using System;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using System.Threading;

namespace QobuzDownloaderX.Shared
{
    public class DownloadLogger(TextBox outputTextBox, string specifier)
    {
        public readonly string logPath = Path.Combine(Globals.LoggingDir, $"{specifier}.log");
        public readonly string downloadErrorLogPath = Path.Combine(Globals.LoggingDir, "Download_Errors.log");
        private static readonly SemaphoreSlim _fileSemaphore = new(1, 1);
        private TextBox ScreenOutputTextBox { get; } = outputTextBox;

        public string DownloadLogPath { get; set; }

        public void RemovePreviousErrorLog()
        {
            // Remove previous download error log
            if (System.IO.File.Exists(downloadErrorLogPath))
            {
                System.IO.File.Delete(downloadErrorLogPath);
            }
        }

        /// <summary>
        /// Add string to download log file, screen or both.<br/>
        /// When a line is written to the log file, a timestamp is added as prefix for non blank lines.<br/>
        /// For writing to log file, blank lines are filtered exept if the given string starts with a blank line.<br/>
        /// Use AddEmptyDownloadLogLine to create empty devider lines in the download log.
        /// </summary>
        /// <param name="logEntry">String to be logged</param>
        /// <param name="logToFile">Should string be logged to file?</param>
        /// <param name="logToScreen">Should string be logged to screen?</param>
        public void AddDownloadLogLine(string logEntry, bool logToFile, bool logToScreen = false)
        {
            if (string.IsNullOrEmpty(logEntry)) return;

            _fileSemaphore.Wait();
            if (logToScreen) ScreenOutputTextBox?.Invoke(new Action(() => ScreenOutputTextBox.AppendText(logEntry)));

            if (logToFile)
            {
                try
                {
                    var logEntries = logEntry.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                        .Select(logLine => string.IsNullOrWhiteSpace(logLine) ? logLine : $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} : {logLine}")
                        .Where(logLine => !string.IsNullOrWhiteSpace(logLine));

                    File.AppendAllLines(logPath, logEntries);
                }
                finally { _fileSemaphore.Release(); }
            }
        }

        /// <summary>
        /// Convenience method to add [ERROR] prefix to logged string before calling AddDownloadLogLine.
        /// </summary>
        /// <param name="logEntry">String to be logged</param>
        /// <param name="logToFile">Should string be logged to file?</param>
        /// <param name="logToScreen">Should string be logged to screen?</param>
        public void AddDownloadLogErrorLine(string logEntry, bool logToFile, bool logToScreen = false)
        {
            AddDownloadLogLine($"[ERROR] {logEntry}", logToFile, logToScreen);
        }

        /// <summary>
        /// Convenience method to add empty spacing line to log.
        /// </summary>
        /// <param name="logToFile">Should string be logged to file?</param>
        /// <param name="logToScreen">Should string be logged to screen?</param>
        public void AddEmptyDownloadLogLine(bool logToFile, bool logToScreen = false)
        {
            AddDownloadLogLine($"{Environment.NewLine}{Environment.NewLine}", logToFile, logToScreen);
        }

        public void AddDownloadErrorLogLines(IEnumerable<string> logEntries)
        {
            if (logEntries == null && !logEntries.Any()) return;
            _fileSemaphore.Wait();
            try { System.IO.File.AppendAllLines(downloadErrorLogPath, logEntries);}
            finally{ _fileSemaphore.Release(); }
        }

        public void AddDownloadErrorLogLine(string logEntry)
        {
            AddDownloadErrorLogLines(new string[] { logEntry });
        }

        /// <summary>
        /// Standardized logging when global download task fails.
        /// After logging, disabled controls are re-enabled.
        /// </summary>
        /// <param name="downloadTaskType">Name of the failed download task</param>
        /// <param name="downloadEx">Exception thrown by task</param>
        public void LogDownloadTaskException(string downloadTaskType, Exception downloadEx)
        {
            // If there is an issue trying to, or during the download, show error info.
            // ClearUiLogComponent();
            AddDownloadLogErrorLine($"{downloadTaskType} Download Task ERROR. Details saved to error log.{Environment.NewLine}", true, true);

            AddDownloadErrorLogLine($"{downloadTaskType} Download Task ERROR.");
            AddDownloadErrorLogLine(downloadEx.ToString());
            AddDownloadErrorLogLine(Environment.NewLine);
        }

        public void LogFinishedDownloadJob(bool noErrorsOccured)
        {
            AddEmptyDownloadLogLine(true, true);

            // Say that downloading is completed.
            if (noErrorsOccured)
            {
                AddDownloadLogLine($"Download job completed! All downloaded files will be located in your chosen path..{Environment.NewLine}", true, true);
            }
            else
            {
                AddDownloadLogLine($"Download job completed with warnings and/or errors! Some or all files could be missing!.{Environment.NewLine}", true, true);
            }
        }

        public void ClearUiLogComponent()
        {
            ScreenOutputTextBox.Invoke(new Action(() => ScreenOutputTextBox.Text = String.Empty));
        }
    }
}