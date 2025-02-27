using QobuzDownloaderX.Models;
using Requests.Options;

namespace QobuzDownloaderX.Shared
{
    public record DownloadRequestOptions : RequestOptions<DownloadItem, DownloadItem>
    {
        public DownloadLogger Logger { get; init; }
        public DownloadItem DownloadItem { get; init; }
        public string DownloadPath { get; init; }
        public bool CheckIfStreamable { get; init; }
        public Notify<DownloadItemInfo> UpdateAlbumTagsUi { get; set; }

        /// <summary>
        /// Main Constructor
        /// </summary>
        public DownloadRequestOptions()
        { }

        /// <summary>
        /// Copy Constructor
        /// </summary>
        /// <param name="options">Copied object</param>
        protected DownloadRequestOptions(DownloadRequestOptions options) : base(options)
        {
            Logger = options.Logger;
            DownloadItem = options.DownloadItem;
            DownloadPath = options.DownloadPath;
            CheckIfStreamable = options.CheckIfStreamable;
            UpdateAlbumTagsUi = options.UpdateAlbumTagsUi;
        }
    }
}