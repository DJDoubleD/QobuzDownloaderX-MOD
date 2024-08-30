using DownloadAssistant.Options;
using DownloadAssistant.Requests;
using Newtonsoft.Json.Linq;
using PlaylistsNET.Content;
using PlaylistsNET.Models;
using QobuzApiSharp.Exceptions;
using QobuzApiSharp.Models.Content;
using QobuzApiSharp.Service;
using QobuzDownloaderX.Models.Content;
using QobuzDownloaderX.Properties;
using QobuzDownloaderX.Shared;
using Requests;
using Requests.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ZendeskApi_v2.Requests;

namespace QobuzDownloaderX.Models.Download
{
    internal class TrackRequest : Request<TrackRequestOptions, DownloadItem, DownloadItem>
    {
        private readonly DownloadLogger _logger;
        private readonly DownloadItem _downloadItem;
        private DownloadItemInfo _downloadInfo;
        private DownloadItemPaths _downloadPaths;
        private bool _isAlbum = false;
        private readonly RequestContainer<IRequest> _requestContainer = [];

        public TrackRequest(TrackRequestOptions options)
            : base(options)
        {
            _logger = options.Logger;
            _downloadItem = options.DownloadItem;
            AutoStart();
        }

        protected override async Task<RequestReturn> RunRequestAsync()
        {
            RequestReturn requestReturn = new();
            try
            {
                requestReturn.Successful = await StartDownloadItemTaskAsync();
            }
            catch (Exception ex)
            {
                requestReturn.Successful = false;
                AddException(ex);
            }
            return requestReturn;
        }

        private async Task<bool> StartDownloadItemTaskAsync()
        {
            string logLine = $"Downloading <{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(_downloadItem.Type)}> from {_downloadItem.Url}";
            _logger.AddDownloadLogLine(new string('=', logLine.Length), true);
            _logger.AddDownloadLogLine(logLine, true);
            _logger.AddDownloadLogLine(new string('=', logLine.Length), true);
            _logger.AddEmptyDownloadLogLine(true);

            _downloadInfo = new DownloadItemInfo { DownloadItemID = _downloadItem.Id };
            _downloadPaths = _downloadInfo.CurrentDownloadPaths;

            return _downloadItem.Type switch
            {
                "track" => await StartDownloadTrackTaskAsync(),
                "album" => await StartDownloadAlbumTaskAsync(),
                "artist" => await StartDownloadArtistDiscogTaskAsync(),
                "label" => await StartDownloadLabelTaskAsync(),
                "user" => _downloadInfo.DownloadItemID switch
                {
                    @"library/favorites/albums" => await StartDownloadFaveAlbumsTaskAsync(),
                    @"library/favorites/artists" => await StartDownloadFaveArtistsTaskAsync(),
                    @"library/favorites/tracks" => await StartDownloadFaveTracksTaskAsync(),
                    _ => InvalidUserFavoritesLink(),
                },
                "playlist" => await StartDownloadPlaylistTaskAsync(),
                _ => InvalidUrl(),
            };

            bool InvalidUserFavoritesLink()
            {
                _logger.AddDownloadLogLine("Invalid user favorites link.", true, true);
                _logger.AddDownloadLogLine("Supported links: Tracks, Albums & Artists.", true, true);
                return ReturnFail();
            }

            bool InvalidUrl()
            {
                _logger.AddDownloadLogLine($"URL >{_downloadItem.Url}< not understood. Is there a typo?", true, true);
                return ReturnFail();
            }
        }

        private T ExecuteApiCall<T>(Func<QobuzApiService, T> apiCall)
        {
            try { return apiCall(QobuzApiServiceManager.GetApiService()); }
            catch (Exception ex)
            {
                AddException(ex);
                _logger.AddEmptyDownloadLogLine(false, true);
                _logger.AddDownloadLogErrorLine("Communication problem with Qobuz API. Details saved to error log", true, true);
                _logger.AddDownloadErrorLogLines(GetErrorLines(ex));
                return default;
            }
        }

        private static string[] GetErrorLines(Exception ex)
        {
            return ex switch
            {
                ApiErrorResponseException erEx => ["Failed API request:", erEx.RequestContent, $"Api response code: {erEx.ResponseStatusCode}", $"Api response status: {erEx.ResponseStatus}", $"Api response reason: {erEx.ResponseReason}"],
                ApiResponseParseErrorException pEx => ["Error parsing API response", $"Api response content: {pEx.ResponseContent}"],
                _ => ["Unknown error trying API request:", ex.ToString()],
            };
        }

        public bool IsStreamable(Track qobuzTrack, bool inPlaylist = false)
        {
            if (qobuzTrack.Streamable != false)
                return true;

            string trackReference = inPlaylist ? $"{qobuzTrack.Performer?.Name} - {qobuzTrack.Title}" : $"{qobuzTrack.TrackNumber.GetValueOrDefault()} {StringTools.DecodeEncodedNonAsciiCharacters(qobuzTrack.Title.Trim())}";

            _logger.AddDownloadLogLine($"Track {trackReference} is not available for streaming. Unable to download.\r\n", true, true);
            return Options.CheckIfStreamable;
        }

        private bool DownloadTrack(Track qobuzTrack, string basePath, bool isPartOfTracklist, bool removeTagArtFileAfterDownload = false, string albumPathSuffix = "")
        {
            if (State != RequestState.Running)
                return false;
            _downloadInfo.SetTrackTaggingInfo(qobuzTrack);

            if (!_isAlbum)
            {
                _downloadInfo.SetAlbumDownloadInfo(qobuzTrack.Album);
                Options.UpdateAlbumTagsUi.Invoke(_downloadInfo);
            }
            if (!IsStreamable(qobuzTrack, isPartOfTracklist))
                return ReturnFail();

            CreateTrackDirectories(basePath, _downloadPaths.QualityPath, albumPathSuffix, isPartOfTracklist);
            string trackPath = _downloadInfo.CurrentDownloadPaths.Path4Full;

            if (!PrepareTrackFile(trackPath, isPartOfTracklist))
                return true;

            string streamUrl = ExecuteApiCall(apiService => apiService.GetTrackFileUrl(qobuzTrack.Id.ToString(), Globals.FormatIdString))?.Url;

            if (string.IsNullOrEmpty(streamUrl))
            {
                _logger.AddDownloadLogLine($"Couldn't get streaming URL for Track \"{_downloadPaths.FinalTrackNamePath}\". Skipping.\r\n", true, true);
                return ReturnFail();
            }
            if (DownloadCoverArt(isPartOfTracklist) is GetRequest getReq)
                _requestContainer.Add(getReq);

            _requestContainer.Add(DownloadTrackFile(streamUrl, trackPath));



            if (removeTagArtFileAfterDownload)
            {
                Notify<IRequest, DownloadItem> x = (_, _) => RemoveTempTaggingArtFile();
                Options.RequestCompleated = (Notify<IRequest, DownloadItem>)(Delegate.Combine(Options.RequestCompleated, x));
            }


            return true;
        }

        private bool PrepareTrackFile(string trackPath, bool isPartOfTracklist)
        {
            string paddedTrackNumber = _downloadInfo.TrackNumber.ToString().PadLeft(Math.Max(2, (int)Math.Floor(Math.Log10(_downloadInfo.TrackTotal) + 1)), '0');
            _downloadPaths.FinalTrackNamePath = isPartOfTracklist ? string.Concat(_downloadPaths.PerformerNamePath, Globals.FileNameTemplateString, _downloadPaths.TrackNamePath).TrimEnd() : string.Concat(paddedTrackNumber, Globals.FileNameTemplateString, _downloadPaths.TrackNamePath).TrimEnd();
            _downloadPaths.FinalTrackNamePath = StringTools.TrimToMaxLength(_downloadPaths.FinalTrackNamePath, Globals.MaxLength);
            _downloadPaths.FullTrackFileName = _downloadPaths.FinalTrackNamePath + Globals.AudioFileType;
            _downloadPaths.FullTrackFilePath = Path.Combine(trackPath, _downloadPaths.FullTrackFileName);

            if (File.Exists(_downloadPaths.FullTrackFilePath))
            {
                _logger.AddDownloadLogLine($"File for \"{_downloadPaths.FinalTrackNamePath}\" already exists. Skipping.\r\n", true, true);
                return false;
            }
            return true;
        }

        private GetRequest DownloadTrackFile(string streamUrl, string trackPath) => new(streamUrl, new()
        {
            IsDownload = true,
            DirectoryPath = trackPath,
            Filename = _downloadPaths.FullTrackFileName,
            NumberOfAttempts = 3,
            RequestStarted = (req) => _logger.AddDownloadLogLine($"Start Downloading - {((GetRequest)req).Filename} ...... \r\n", true, true),
            RequestCompleated = (req, _) =>
            {
                string coverArtTagFilePath = Path.Combine(trackPath, Globals.TaggingOptions.ArtSize + ".jpg");
                AudioFileTagger.AddMetaDataTags(_downloadInfo, (req as GetRequest).FilePath, File.Exists(coverArtTagFilePath) ? coverArtTagFilePath : Path.Combine(trackPath, "Cover.jpg"), _logger);

                _logger.AddDownloadLogLine($"Track {(req as GetRequest).Filename} Download Done!\r\n", true, true);
            },
            RequestCancelled = req => HandleDownloadException(req.Exception, "Track Download canceled, probably due to network error or request timeout."),
            RequestFailed = (req, _) => HandleDownloadException(req.Exception, "Unknown error during Track Download.")
        });

        private GetRequest DownloadCoverArt(bool isPartOfTracklist)
        {
            string coverArtTagFilePath = Path.Combine(_downloadPaths.Path3Full, Globals.TaggingOptions.ArtSize + ".jpg");
            if (!File.Exists(coverArtTagFilePath))
                return new(_downloadInfo.FrontCoverImgTagUrl, new GetRequestOptions
                {
                    IsDownload = true,
                    Priority = RequestPriority.High,
                    DirectoryPath = _downloadPaths.Path3Full,
                    Filename = Globals.TaggingOptions.ArtSize + ".jpg",
                    NumberOfAttempts = 2,
                    RequestFailed = (req, _) => _logger.AddDownloadErrorLogLines(["Error downloading image file for tagging.", req.Exception.Message, Environment.NewLine])
                });

            string coverArtFilePath = Path.Combine(_downloadPaths.Path3Full, "Cover.jpg");

            if (!isPartOfTracklist && !File.Exists(coverArtFilePath))
                return new(_downloadInfo.FrontCoverImgUrl, new()
                {
                    IsDownload = true,
                    DirectoryPath = _downloadPaths.Path3Full,
                    Filename = "Cover.jpg",
                    Priority = RequestPriority.High,
                    NumberOfAttempts = 1,
                    RequestFailed = (req, _) => _logger.AddDownloadErrorLogLines(["Error downloading full size cover image file.", req.Exception.Message, Environment.NewLine])
                });
            return null;
        }


        private void RemoveTempTaggingArtFile()
        {
            try
            {
                string coverArtTagFilePath = Path.Combine(_downloadPaths.Path3Full, Globals.TaggingOptions.ArtSize + ".jpg");
                if (File.Exists(coverArtTagFilePath))
                    File.Delete(coverArtTagFilePath);
            }
            catch
            {
            }

        }

        private void HandleDownloadException(Exception ex, string message)
        {
            _logger.AddDownloadLogErrorLine($"{message} Details saved to error log.{Environment.NewLine}", true, true);
            _logger.AddDownloadErrorLogLine(message);
            _logger.AddDownloadErrorLogLine(ex.ToString());
            _logger.AddDownloadErrorLogLine(Environment.NewLine);
        }

        private bool DownloadAlbum(Album qobuzAlbum, string basePath, string albumPathSuffix = "")
        {
            bool noErrorsOccured = true;
            const int tracksLimit = 50;
            qobuzAlbum = ExecuteApiCall(apiService => apiService.GetAlbum(qobuzAlbum.Id, true, null, tracksLimit, 0));

            if (string.IsNullOrEmpty(qobuzAlbum.Id))
                return ReturnFail();

            _downloadInfo.SetAlbumDownloadInfo(qobuzAlbum);
            Options.UpdateAlbumTagsUi.Invoke(_downloadInfo);
            _isAlbum = true;
            int tracksTotal = qobuzAlbum.Tracks.Total ?? 0;
            int tracksPageOffset = qobuzAlbum.Tracks.Offset ?? 0;
            int tracksLoaded = qobuzAlbum.Tracks.Items?.Count ?? 0;
            int i = 0;

            while (i < tracksLoaded)
            {
                if (State != RequestState.Running)
                    return false;
                bool isLastTrackOfAlbum = (i + tracksPageOffset) == (tracksTotal - 1);
                Track qobuzTrack = qobuzAlbum.Tracks.Items[i];
                qobuzTrack.Album = qobuzAlbum;
                if (!DownloadTrack(qobuzTrack, basePath, false, isLastTrackOfAlbum, albumPathSuffix))
                    noErrorsOccured = false;

                i++;
                if (i == tracksLoaded && tracksTotal > (i + tracksPageOffset))
                {
                    tracksPageOffset += tracksLimit;
                    qobuzAlbum = ExecuteApiCall(apiService => apiService.GetAlbum(qobuzAlbum.Id, true, null, tracksLimit, tracksPageOffset));

                    if (string.IsNullOrEmpty(qobuzAlbum.Id))
                        return false;
                    if (qobuzAlbum.Tracks?.Items?.Any() != true)
                        break;
                    i = 0;
                    tracksLoaded = qobuzAlbum.Tracks.Items?.Count ?? 0;
                }
            }

            DownloadBooklets(qobuzAlbum, _downloadPaths.Path3Full);
            return noErrorsOccured;
        }

        private void DownloadBooklets(Album qobuzAlbum, string basePath)
        {
            List<Goody> booklets = qobuzAlbum.Goodies?.Where(g => g.FileFormatId == (int)GoodiesFileType.BOOKLET).ToList();

            if (booklets == null || !booklets.Any())
                return;

            _logger.AddDownloadLogLine($"Goodies found, downloading...{Environment.NewLine}", true, true);
            int counter = 1;

            foreach (Goody booklet in booklets)
            {
                string bookletFileName = counter == 1 ? "Digital Booklet.pdf" : $"Digital Booklet {counter}.pdf";
                string bookletFilePath = Path.Combine(basePath, bookletFileName);
                if (File.Exists(bookletFilePath))
                    _logger.AddDownloadLogLine($"Booklet file for \"{bookletFileName}\" already exists. Skipping.{Environment.NewLine}", true, true);
                else
                    _requestContainer.Add(DownloadBooklet(booklet, bookletFileName, bookletFilePath));
                counter++;
            }
        }

        private GetRequest DownloadBooklet(Goody booklet, string fileName, string filePath) => new(booklet.Url, new()
        {
            IsDownload = true,
            Filename = fileName,
            DirectoryPath = filePath,
            NumberOfAttempts = 1,
            RequestCompleated = (req, _) => _logger.AddDownloadLogLine($"Booklet \"{fileName}\" download complete!{Environment.NewLine}", true, true),
            RequestFailed = (req, _) => HandleBookletDownloadException(req.Exception)
        });


        private void HandleBookletDownloadException(Exception ex)
        {
            if (ex.InnerException is AggregateException ae)
            {
                _logger.AddDownloadLogErrorLine("Goodies Download canceled, probably due to network error or request timeout. Details saved to error log.", true, true);
                _logger.AddDownloadErrorLogLine(ae.ToString());
            }
            else
            {
                _logger.AddDownloadLogErrorLine("Unknown error during Goodies Download. Details saved to error log.", true, true);
                _logger.AddDownloadErrorLogLine(ex.ToString());
            }
            _logger.AddDownloadErrorLogLine(Environment.NewLine);
        }

        private bool DownloadAlbums(string basePath, List<Album> albums, bool isEndOfDownloadJob)
        {
            bool noAlbumErrorsOccured = true;
            foreach (Album qobuzAlbum in albums)
            {
                if (State != RequestState.Running)
                    return false;
                //_logger.ClearUiLogComponent();
                _logger.AddEmptyDownloadLogLine(true, false);
                _logger.AddDownloadLogLine($"Creating Download requests for album \"{qobuzAlbum.Title}\" with ID: <{qobuzAlbum.Id}>...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);
                if (!DownloadAlbum(qobuzAlbum, basePath, $" [{qobuzAlbum.Id}]"))
                    noAlbumErrorsOccured = false;
            }
            if (isEndOfDownloadJob)
                _logger.LogFinishedDownloadJob(noAlbumErrorsOccured);
            return noAlbumErrorsOccured;
        }

        private bool DownloadReleases(string basePath, List<Release> releases)
        {
            bool noAlbumErrorsOccured = true;
            foreach (Release qobuzRelease in releases)
            {
                if (State != RequestState.Running)
                    return false;
                Album qobuzAlbum = ExecuteApiCall(apiService => apiService.GetAlbum(qobuzRelease.Id, true, null, 0, 0));

                if (string.IsNullOrEmpty(qobuzAlbum.Id))
                {
                    noAlbumErrorsOccured = false;
                    continue;
                }
                _logger.AddEmptyDownloadLogLine(true, false);
                _logger.AddDownloadLogLine($"Creating Download requests for album \"{qobuzAlbum.Title}\" with ID: <{qobuzAlbum.Id}>...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);
                if (!DownloadAlbum(qobuzAlbum, basePath, $" [{qobuzAlbum.Id}]"))
                    noAlbumErrorsOccured = false;
            }
            return noAlbumErrorsOccured;
        }

        private bool DownloadArtistReleases(Artist qobuzArtist, string basePath, string releaseType, bool isEndOfDownloadJob)
        {
            bool noErrorsOccured = true;
            const int releasesLimit = 100;
            int releasesOffset = 0;
            ReleasesList releasesList = ExecuteApiCall(apiService => apiService.GetReleaseList(qobuzArtist.Id.ToString(), true, releaseType, "release_date", "desc", 0, releasesLimit, releasesOffset));

            if (releasesList == null)
                return ReturnFail();
            bool continueDownload = true;
            while (continueDownload)
            {
                if (State != RequestState.Running)
                    return false;
                if (!DownloadReleases(basePath, releasesList.Items))
                    noErrorsOccured = false;
                if (releasesList.HasMore)
                {
                    releasesOffset += releasesLimit;
                    releasesList = ExecuteApiCall(apiService => apiService.GetReleaseList(qobuzArtist.Id.ToString(), true, releaseType, "release_date", "desc", 0, releasesLimit, releasesOffset));
                }
                else
                    continueDownload = false;
            }
            if (isEndOfDownloadJob)
                _logger.LogFinishedDownloadJob(noErrorsOccured);
            return noErrorsOccured;
        }


        private async Task<bool> StartDownloadTrackTaskAsync()
        {
            _logger.AddDownloadLogLine("Grabbing Track info...", true, true);
            string downloadBasePath = Settings.Default.savedFolder;
            try
            {
                Track qobuzTrack = ExecuteApiCall(apiService => apiService.GetTrack(_downloadInfo.DownloadItemID, true));

                if (qobuzTrack == null)
                    return ReturnFail();

                _logger.AddDownloadLogLine($"Track \"{qobuzTrack.Title}\" found. Starting Download...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);

                if (!DownloadTrack(qobuzTrack, downloadBasePath, true, true))
                    return false;

                await _requestContainer.Task;

                _logger.AddEmptyDownloadLogLine(true, true);
                _logger.AddDownloadLogLine("Download job completed! All downloaded files will be located in your chosen path.\r\n", true, true);
            }
            catch (Exception downloadEx)
            {
                _logger.LogDownloadTaskException("Track", downloadEx);
                return false;
            }
            return true;
        }

        private async Task<bool> StartDownloadAlbumTaskAsync()
        {
            _logger.AddDownloadLogLine("Grabbing Album info...", true, true);
            string downloadBasePath = Settings.Default.savedFolder;
            try
            {
                Album qobuzAlbum = ExecuteApiCall(apiService => apiService.GetAlbum(_downloadInfo.DownloadItemID, true, null, 0));
                if (qobuzAlbum == null)
                    return ReturnFail();
                _logger.AddDownloadLogLine($"Album \"{qobuzAlbum.Title}\" found. Starting Downloads...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);

                bool finished = DownloadAlbum(qobuzAlbum, downloadBasePath);
                await _requestContainer.Task;
                _logger.LogFinishedDownloadJob(finished);
            }
            catch (Exception downloadEx)
            {
                _logger.LogDownloadTaskException("Album", downloadEx);
                return false;
            }
            return true;
        }

        private async Task<bool> StartDownloadArtistDiscogTaskAsync()
        {
            string artistBasePath = Settings.Default.savedFolder;
            _logger.AddDownloadLogLine("Grabbing Artist info...", true, true);
            try
            {
                Artist qobuzArtist = ExecuteApiCall(apiService => apiService.GetArtist(_downloadInfo.DownloadItemID, true));
                if (qobuzArtist == null)
                    return ReturnFail();
                _logger.AddDownloadLogLine($"Starting Downloads for artist \"{qobuzArtist.Name}\" with ID: <{qobuzArtist.Id}>...", true, true);
                DownloadArtistReleases(qobuzArtist, artistBasePath, "all", true);
                await _requestContainer.Task;
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Artist", downloadEx);
                return false;
            }
            return true;
        }

        private async Task<bool> StartDownloadLabelTaskAsync()
        {
            string labelBasePath = Path.Combine(Settings.Default.savedFolder, "- Labels");
            _logger.AddDownloadLogLine("Grabbing Label albums...", true, true);
            try
            {
                Label qobuzLabel = null;
                List<Album> labelAlbums = new();
                const int albumLimit = 500;
                int albumsOffset = 0;
                while (true)
                {
                    qobuzLabel = ExecuteApiCall(apiService => apiService.GetLabel(_downloadInfo.DownloadItemID, true, "albums", albumLimit, albumsOffset));
                    if (qobuzLabel == null)
                        return ReturnFail();
                    if (qobuzLabel.Albums?.Items?.Any() != true)
                        break;
                    labelAlbums.AddRange(qobuzLabel.Albums.Items);
                    if ((qobuzLabel.Albums?.Total ?? 0) == labelAlbums.Count)
                        break;
                    albumsOffset += albumLimit;
                }

                if (!labelAlbums.Any())
                {
                    _logger.AddDownloadLogLine($"No albums found for label \"{qobuzLabel.Name}\" with ID: <{qobuzLabel.Id}>, nothing to download.", true, true);
                    return ReturnFail();
                }

                _logger.AddDownloadLogLine($"Starting Downloads for label \"{qobuzLabel.Name}\" with ID: <{qobuzLabel.Id}>...", true, true);
                string safeLabelName = StringTools.GetSafeFilename(StringTools.DecodeEncodedNonAsciiCharacters(qobuzLabel.Name));
                labelBasePath = Path.Combine(labelBasePath, safeLabelName);
                DownloadAlbums(labelBasePath, labelAlbums, true);
                await _requestContainer.Task;
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Label", downloadEx);
                return false;
            }
            return true;
        }

        private async Task<bool> StartDownloadFaveAlbumsTaskAsync()
        {
            string favoritesBasePath = Path.Combine(Settings.Default.savedFolder, "- Favorites");
            _logger.AddDownloadLogLine("Grabbing Favorite Albums...", true, true);
            try
            {
                List<Album> favoriteAlbums = new();
                const int albumLimit = 500;
                int albumsOffset = 0;
                while (true)
                {
                    UserFavorites qobuzUserFavorites = ExecuteApiCall(apiService => apiService.GetUserFavorites(_downloadInfo.DownloadItemID, "albums", albumLimit, albumsOffset));
                    if (qobuzUserFavorites == null)
                        return ReturnFail();
                    if (qobuzUserFavorites.Albums?.Items?.Any() != true)
                        break;
                    favoriteAlbums.AddRange(qobuzUserFavorites.Albums.Items);
                    if ((qobuzUserFavorites.Albums?.Total ?? 0) == favoriteAlbums.Count)
                        break;
                    albumsOffset += albumLimit;
                }

                if (!favoriteAlbums.Any())
                {
                    _logger.AddDownloadLogLine("No favorite albums found, nothing to download.", true, true);
                    return ReturnFail();
                }
                DownloadAlbums(favoritesBasePath, favoriteAlbums, true);
                await _requestContainer.Task;
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Favorite Albums", downloadEx);
                return false;
            }
            return true;
        }

        private async Task<bool> StartDownloadFaveArtistsTaskAsync()
        {
            string favoritesBasePath = Path.Combine(Settings.Default.savedFolder, "- Favorites");
            _logger.AddDownloadLogLine("Grabbing Favorite Artists...", true, true);

            try
            {
                bool noArtistErrorsOccured = true;
                UserFavoritesIds qobuzUserFavoritesIds = ExecuteApiCall(apiService => apiService.GetUserFavoriteIds(_downloadInfo.DownloadItemID));
                if (qobuzUserFavoritesIds == null)
                    return ReturnFail();

                if (qobuzUserFavoritesIds.Artists?.Any() != true)
                {
                    _logger.AddDownloadLogLine("No favorite artists found, nothing to download.", true, true);
                    return ReturnFail();
                }

                foreach (int favoriteArtistId in qobuzUserFavoritesIds.Artists)
                {
                    if (State != RequestState.Running)
                        return false;
                    Artist qobuzArtist = ExecuteApiCall(apiService => apiService.GetArtist(favoriteArtistId.ToString(), true));
                    if (qobuzArtist == null)
                    {
                        noArtistErrorsOccured = false;
                        continue;
                    }
                    _logger.AddEmptyDownloadLogLine(true, true);
                    _logger.AddDownloadLogLine($"Starting Downloads for artist \"{qobuzArtist.Name}\" with ID: <{qobuzArtist.Id}>...", true, true);
                    if (!DownloadArtistReleases(qobuzArtist, favoritesBasePath, "all", false))
                        noArtistErrorsOccured = false;
                }

                await _requestContainer.Task;
                _logger.LogFinishedDownloadJob(noArtistErrorsOccured);
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Favorite Albums", downloadEx);
                return false;
            }
            return true;
        }

        private async Task<bool> StartDownloadFaveTracksTaskAsync()
        {
            string favoriteTracksBasePath = Path.Combine(Settings.Default.savedFolder, "- Favorites");
            _logger.AddDownloadLogLine("Grabbing Favorite Tracks...", true, true);
            _logger.AddEmptyDownloadLogLine(true, true);
            try
            {
                bool noTrackErrorsOccured = true;
                UserFavoritesIds qobuzUserFavoritesIds = ExecuteApiCall(apiService => apiService.GetUserFavoriteIds(_downloadInfo.DownloadItemID));
                if (qobuzUserFavoritesIds == null)
                    return ReturnFail();

                if (qobuzUserFavoritesIds.Tracks?.Any() != true)
                {
                    _logger.AddDownloadLogLine("No favorite tracks found, nothing to download.", true, true);
                    return ReturnFail();
                }

                _logger.AddDownloadLogLine("Favorite tracks found. Starting Downloads...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);

                foreach (int favoriteTrackId in qobuzUserFavoritesIds.Tracks)
                {
                    if (State != RequestState.Running)
                        return false;
                    Track qobuzTrack = ExecuteApiCall(apiService => apiService.GetTrack(favoriteTrackId.ToString(), true));
                    if (qobuzTrack == null)
                    {
                        noTrackErrorsOccured = false;
                        continue;
                    }
                    if (!DownloadTrack(qobuzTrack, favoriteTracksBasePath, true, true))
                        noTrackErrorsOccured = false;
                }
                await _requestContainer.Task;
                _logger.LogFinishedDownloadJob(noTrackErrorsOccured);
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Playlist", downloadEx);
                return false;
            }
            return true;
        }

        private async Task<bool> StartDownloadPlaylistTaskAsync()
        {
            string playlistBasePath = Settings.Default.savedFolder;
            _logger.AddDownloadLogLine("Grabbing Playlist tracks...", true, true);
            _logger.AddEmptyDownloadLogLine(true, true);
            try
            {
                Playlist qobuzPlaylist = ExecuteApiCall(apiService => apiService.GetPlaylist(_downloadInfo.DownloadItemID, true, "track_ids", 10000));
                if (qobuzPlaylist == null)
                    return ReturnFail();
                if (qobuzPlaylist.TrackIds?.Any() != true)
                {
                    _logger.AddDownloadLogLine($"Playlist \"{qobuzPlaylist.Name}\" is empty, nothing to download.", true, true);
                    return ReturnFail();
                }

                _logger.AddDownloadLogLine($"Playlist \"{qobuzPlaylist.Name}\" found. Starting Downloads...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);

                string playlistSafeName = StringTools.GetSafeFilename(StringTools.DecodeEncodedNonAsciiCharacters(qobuzPlaylist.Name));
                string playlistNamePath = StringTools.TrimToMaxLength(playlistSafeName, Globals.MaxLength);
                playlistBasePath = Path.Combine(playlistBasePath, "- Playlists", playlistNamePath);
                Directory.CreateDirectory(playlistBasePath);
                string coverArtFilePath = Path.Combine(playlistBasePath, "Playlist.jpg");

                if (!File.Exists(coverArtFilePath))
                {
                    _requestContainer.Add(new GetRequest(qobuzPlaylist.ImageRectangle.FirstOrDefault(), new()
                    {
                        DirectoryPath = playlistBasePath,
                        Filename = "Playlist.jpg",
                        IsDownload = true,
                        RequestFailed = (req, _) => _logger.AddDownloadErrorLogLines(["Error downloading full size playlist cover image file.", req.Exception.Message, "\r\n"])
                    }));
                }
                bool noTrackErrorsOccured = true;
                M3uPlaylist m3uPlaylist = new();
                m3uPlaylist.IsExtended = true;
                foreach (long trackId in qobuzPlaylist.TrackIds)
                {
                    if (State != RequestState.Running)
                        return false;
                    Track qobuzTrack = ExecuteApiCall(apiService => apiService.GetTrack(trackId.ToString(), true));
                    if (qobuzTrack == null)
                    {
                        noTrackErrorsOccured = false;
                        continue;
                    }
                    if (!IsStreamable(qobuzTrack, true))
                        continue;
                    if (!DownloadTrack(qobuzTrack, playlistBasePath, true, true))
                        noTrackErrorsOccured = false;
                    AddTrackToPlaylistFile(m3uPlaylist, _downloadInfo, _downloadPaths);
                }
                await _requestContainer.Task;
                string m3uPlaylistFile = Path.Combine(playlistBasePath, $"{playlistSafeName}.m3u8");
                File.WriteAllText(m3uPlaylistFile, PlaylistToTextHelper.ToText(m3uPlaylist), System.Text.Encoding.UTF8);
                _logger.LogFinishedDownloadJob(noTrackErrorsOccured);
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Playlist", downloadEx);
                return false;
            }
            return true;
        }

        public void AddTrackToPlaylistFile(M3uPlaylist m3uPlaylist, DownloadItemInfo downloadInfo, DownloadItemPaths downloadPaths)
        {
            if (!File.Exists(downloadPaths.FullTrackFilePath)) return;
            m3uPlaylist.PlaylistEntries.Add(new M3uPlaylistEntry()
            {
                Path = downloadPaths.FullTrackFilePath,
                Duration = TimeSpan.FromSeconds(_downloadInfo.Duration),
                Title = $"{downloadInfo.PerformerName} - {downloadInfo.TrackName}"
            });
        }

        public void CreateTrackDirectories(string basePath, string qualityPath, string albumPathSuffix = "", bool forTracklist = false)
        {
            if (forTracklist)
            {
                _downloadPaths.Path1Full = basePath;
                _downloadPaths.Path2Full = _downloadPaths.Path1Full;
                _downloadPaths.Path3Full = Path.Combine(basePath, qualityPath);
                _downloadPaths.Path4Full = _downloadPaths.Path3Full;
            }
            else
            {
                _downloadPaths.Path1Full = Path.Combine(basePath, _downloadPaths.AlbumArtistPath);
                _downloadPaths.Path2Full = Path.Combine(basePath, _downloadPaths.AlbumArtistPath, _downloadPaths.AlbumNamePath + albumPathSuffix);
                _downloadPaths.Path3Full = Path.Combine(basePath, _downloadPaths.AlbumArtistPath, _downloadPaths.AlbumNamePath + albumPathSuffix, qualityPath);
                if (_downloadInfo.DiscTotal > 1)
                {
                    string discFolder = "CD " + _downloadInfo.DiscNumber.ToString().PadLeft(Math.Max(2, (int)Math.Floor(Math.Log10(_downloadInfo.DiscTotal) + 1)), '0');
                    _downloadPaths.Path4Full = Path.Combine(basePath, _downloadPaths.AlbumArtistPath, _downloadPaths.AlbumNamePath + albumPathSuffix, qualityPath, discFolder);
                }
                else
                {
                    _downloadPaths.Path4Full = _downloadPaths.Path3Full;
                }
            }
            Directory.CreateDirectory(_downloadPaths.Path4Full);
        }

        public override void Pause()
        {
            base.Pause();
            _requestContainer.Pause();
        }
        public override void Start()
        {
            base.Start();
            _requestContainer.Start();
        }

        public override void Cancel()
        {
            base.Cancel();
            _requestContainer.Cancel();
        }


        private bool ReturnFail()
        {
            Options.NumberOfAttempts = 0;
            return false;
        }
    }
}
