using QobuzApiSharp.Models.User;

namespace QobuzDownloaderX.Shared
{
    internal static class Globals
    {
        public const string GITHUB_LATEST_VERSION_URL = "https://api.github.com/repos/DJDoubleD/QobuzDownloaderX-MOD/releases/latest";
        public const string GITHUB_LATEST_URL = "https://github.com/DJDoubleD/QobuzDownloaderX-MOD/releases/latest";
        public const string GITHUB_REPO_URL = "https://github.com/DJDoubleD/QobuzDownloaderX-MOD";
        public const string GITHUB_ImAiiR_REPO_URL = "https://github.com/ImAiiR/QobuzDownloaderX";
        public const string GITHUB_ALT_LOGIN_TUTORIAL_URL = "https://github.com/DJDoubleD/QobuzDownloaderX-MOD/wiki/Logging-In-(The-Alternate-Way)";
        public const string WEBPLAYER_BASE_URL = "https://play.qobuz.com";
        public const string USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/110.0";
        public const string DEFAULT_COVER_ART_URL = "https://static.qobuz.com/images/covers/01/00/2013072600001_150.jpg";

        // Forms
        public static LoginForm LoginForm { get; set; }
        public static QobuzDownloaderX QbdlxForm { get; set; }
        public static AboutForm AboutForm { get; set; }
        public static SearchForm SearchForm { get; set; }
        public static CSVForm CSVForm { get; set; }

        // Login
        public static Login Login { get; set; }

        // Tagging options
        public static TaggingOptions TaggingOptions { get; set; }

        // Audio quality selection
        public static string FormatIdString { get; set; }
        public static string AudioFileType { get; set; }

        // Additional user selections
        public static int MaxLength { get; set; }
        public static string FileNameTemplateString { get; set; }

        // Logs
        public static string LoggingDir { get; set; }
    }
}