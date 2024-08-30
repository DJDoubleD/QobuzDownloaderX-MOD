using QobuzDownloaderX.Shared;
using Requests;
using Requests.Options;

namespace QobuzDownloaderX.Models.Download
{
    public record TrackRequestOptions : RequestOptions<DownloadItem, DownloadItem>
    {
        public DownloadLogger Logger { get; init; }
        public DownloadItem DownloadItem { get; init; }
        public string DownloadPath { get; init; }
        public bool CheckIfStreamable { get; init; }
        public Notify<DownloadItemInfo> UpdateAlbumTagsUi { get; set; }

        /// <summary>
        /// Main Constructor
        /// </summary>
        public TrackRequestOptions()
        { }

        /// <summary>
        /// Copy Constructor
        /// </summary>
        /// <param name="options">Copied object</param>
        protected TrackRequestOptions(TrackRequestOptions options) : base(options)
        {
            Logger = options.Logger;
            DownloadItem = options.DownloadItem;
            DownloadPath = options.DownloadPath;
            CheckIfStreamable = options.CheckIfStreamable;
            UpdateAlbumTagsUi = options.UpdateAlbumTagsUi;
        }
    }
}