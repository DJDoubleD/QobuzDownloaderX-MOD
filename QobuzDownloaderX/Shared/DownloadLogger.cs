using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;


namespace QobuzDownloaderX.Shared
{
    public class DownloadLogger(TextBox outputTextBox, string specifier)
    {
        public readonly string logPath = Path.Combine(Globals.LoggingDir, $"{specifier}.log");
        public readonly string downloadErrorLogPath = Path.Combine(Globals.LoggingDir, "Download_Errors.log");
        private TextBox ScreenOutputTextBox { get; } = outputTextBox;

        public void RemovePreviousErrorLog()
        {
            Debug.WriteLine(logPath);
            if (File.Exists(downloadErrorLogPath)) File.Delete(downloadErrorLogPath);
        }

        public void AddDownloadLogLine(string logEntry, bool logToFile, bool logToScreen = false)
        {
            if (string.IsNullOrEmpty(logEntry)) return;

            if (logToScreen)
                ScreenOutputTextBox?.Invoke(() => ScreenOutputTextBox.AppendText(logEntry));

            if (logToFile)
            {
                var logEntries = logEntry.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                    .Select(logLine => string.IsNullOrWhiteSpace(logLine) ? logLine : $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} : {logLine}")
                    .Where(logLine => !string.IsNullOrWhiteSpace(logLine));

                File.AppendAllLines(logPath, logEntries);
            }
        }

        public void AddDownloadLogErrorLine(string logEntry, bool logToFile, bool logToScreen = false) => AddDownloadLogLine($"[ERROR] {logEntry}", logToFile, logToScreen);


        public void AddEmptyDownloadLogLine(bool logToFile, bool logToScreen = false) => AddDownloadLogLine($"{Environment.NewLine}{Environment.NewLine}", logToFile, logToScreen);


        public void AddDownloadErrorLogLines(params string[] logEntries)
        {
            if (logEntries?.Any() == true) File.AppendAllLines(downloadErrorLogPath, logEntries);
        }

        public void AddDownloadErrorLogLine(string logEntry) => AddDownloadErrorLogLines(logEntry);


        public void LogDownloadTaskException(string downloadTaskType, Exception downloadEx)
        {
            // ClearUiLogComponent();
            AddDownloadLogErrorLine($"{downloadTaskType} Download Task ERROR. Details saved to error log.{Environment.NewLine}", true, true);

            AddDownloadErrorLogLine($"{downloadTaskType} Download Task ERROR.");
            AddDownloadErrorLogLine(downloadEx.ToString());
            AddDownloadErrorLogLine(Environment.NewLine);
        }

        public void LogFinishedDownloadJob(bool noErrorsOccurred)
        {
            AddEmptyDownloadLogLine(true, true);

            if (noErrorsOccurred)
                AddDownloadLogLine("Download job completed! All downloaded files will be located in your chosen path.", true, true);
            else
                AddDownloadLogLine("Download job completed with warnings and/or errors! Some or all files could be missing!", true, true);
        }

        public void ClearUiLogComponent() => ScreenOutputTextBox.Invoke(() => ScreenOutputTextBox.Text = string.Empty);

    }
}
