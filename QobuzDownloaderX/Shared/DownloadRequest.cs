using DownloadAssistant.Options;
using DownloadAssistant.Requests;
using PlaylistsNET.Content;
using PlaylistsNET.Models;
using QobuzApiSharp.Exceptions;
using QobuzApiSharp.Models.Content;
using QobuzApiSharp.Service;
using QobuzDownloaderX.Models;
using QobuzDownloaderX.Models.Content;
using QobuzDownloaderX.Properties;
using Requests;
using Requests.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QobuzDownloaderX.Shared
{
    internal class DownloadRequest : Request<DownloadRequestOptions, DownloadItem, DownloadItem>, ISpeedReportable
    {
        private readonly DownloadLogger _logger;
        private readonly DownloadItem _downloadItem;
        private readonly ConcurrentDictionary<string, DownloadItemInfo> _downloadInfoDict = new();
        private readonly ConcurrentDictionary<string, DownloadItemPaths> _downloadPathsDict = new();
        private bool _isAlbum = false;
        private readonly ExtendedContainer<IRequest> _requestContainer = [];
        private bool _isRequestContainerFilled = false;
        private readonly HashSet<string> _IdContainer = [];

        public bool IsAlbum => _isAlbum;

        public SpeedReporter<long> SpeedReporter => _requestContainer.SpeedReporter;

        public DownloadRequest(DownloadRequestOptions options)
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
            if (_isRequestContainerFilled == true)
                return await WaitOnCompletion();

            string key = _downloadItem.Id.ToString();
            _downloadInfoDict[key] = new DownloadItemInfo { DownloadItemID = _downloadItem.Id };
            _downloadPathsDict[key] = _downloadInfoDict[key].CurrentDownloadPaths;
            return _downloadItem.Type switch
            {
                "track" => await StartDownloadTrackTaskAsync(),
                "album" => await StartDownloadAlbumTaskAsync(),
                "artist" => await StartDownloadArtistDiscogTaskAsync(),
                "label" => await StartDownloadLabelTaskAsync(),
                "user" => _downloadInfoDict[key].DownloadItemID switch
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

        private async Task<bool> WaitOnCompletion()
        {
            if (State != RequestState.Running)
                return false;
            _isRequestContainerFilled = true;
            await _requestContainer.Task;
            await Task.Delay(1000);
            _logger.LogFinishedDownloadJob(true);
            return true;
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
            if (_IdContainer.Contains(qobuzTrack.Id.ToString()))
                return true;
            if (State != RequestState.Running && State != RequestState.Idle)
                return false;

            string key = qobuzTrack.Id.ToString();
            _downloadInfoDict[key] = new DownloadItemInfo { DownloadItemID = qobuzTrack.Id.ToString() };
            _downloadInfoDict[key].SetAlbumDownloadInfo(qobuzTrack.Album);
            _downloadInfoDict[key].SetTrackTaggingInfo(qobuzTrack);

            _downloadPathsDict[key] = _downloadInfoDict[key].CurrentDownloadPaths;

            if (!_isAlbum)
                Options.UpdateAlbumTagsUi.Invoke(_downloadInfoDict[key]);
            if (!IsStreamable(qobuzTrack, isPartOfTracklist))
                return ReturnFail();

            CreateTrackDirectories(basePath, _downloadPathsDict[key].QualityPath, key, albumPathSuffix, isPartOfTracklist);
            string trackPath = _downloadInfoDict[key].CurrentDownloadPaths.Path4Full;

            if (!PrepareTrackFile(trackPath, isPartOfTracklist, key))
                return true;

            string streamUrl = ExecuteApiCall(apiService => apiService.GetTrackFileUrl(qobuzTrack.Id.ToString(), Globals.FormatIdString))?.Url;

            if (string.IsNullOrEmpty(streamUrl))
            {
                _logger.AddDownloadLogLine($"Couldn't get streaming URL for Track \"{_downloadPathsDict[key].FinalTrackNamePath}\". Skipping.\r\n", true, true);
                return ReturnFail();
            }
            Task.Delay(50).Wait(); //Issue with the library! Too fast populated!
            if (DownloadCoverArt(isPartOfTracklist, key) is GetRequest getReq)
                _requestContainer.Add(getReq);

            _requestContainer.Add(DownloadTrackFile(streamUrl, trackPath, key));
            _IdContainer.Add(qobuzTrack.Id.ToString());

            if (removeTagArtFileAfterDownload)
            {
                Notify<IRequest, DownloadItem> x = (_, _) => RemoveTempTaggingArtFile(key);
                Options.RequestCompleated = (Notify<IRequest, DownloadItem>)Delegate.Combine(Options.RequestCompleated, x);
            }
            return true;
        }

        private bool PrepareTrackFile(string trackPath, bool isPartOfTracklist, string key)
        {
            string paddedTrackNumber = _downloadInfoDict[key].TrackNumber.ToString().PadLeft(Math.Max(2, (int)Math.Floor(Math.Log10(_downloadInfoDict[key].TrackTotal) + 1)), '0');
            _downloadPathsDict[key].FinalTrackNamePath = isPartOfTracklist ? string.Concat(_downloadPathsDict[key].PerformerNamePath, Globals.FileNameTemplateString, _downloadPathsDict[key].TrackNamePath).TrimEnd() : string.Concat(paddedTrackNumber, Globals.FileNameTemplateString, _downloadPathsDict[key].TrackNamePath).TrimEnd();
            _downloadPathsDict[key].FinalTrackNamePath = StringTools.TrimToMaxLength(_downloadPathsDict[key].FinalTrackNamePath, Globals.MaxLength);
            _downloadPathsDict[key].FullTrackFileName = _downloadPathsDict[key].FinalTrackNamePath + Globals.AudioFileType;
            _downloadPathsDict[key].FullTrackFilePath = Path.Combine(trackPath, _downloadPathsDict[key].FullTrackFileName);

            if (File.Exists(_downloadPathsDict[key].FullTrackFilePath))
            {
                _logger.AddDownloadLogLine($"File for \"{_downloadPathsDict[key].FinalTrackNamePath}\" already exists. Skipping.\r\n", true, true);
                return false;
            }
            return true;
        }

        private GetRequest DownloadTrackFile(string streamUrl, string trackPath, string key) => new(streamUrl, new()
        {
            IsDownload = true,
            DirectoryPath = trackPath,
            Filename = _downloadPathsDict[key].FullTrackFileName,
            NumberOfAttempts = 3,
            SpeedReporter = new SpeedReporter<long>(),
            RequestStarted = (req) => _logger.AddDownloadLogLine($"Start Downloading - {((GetRequest)req).Filename} ...... \r\n", true, true),
            RequestCompleated = (req, _) =>
            {
                string coverArtTagFilePath = Path.Combine(trackPath, Globals.TaggingOptions.ArtSize + ".jpg");
                AudioFileTagger.AddMetaDataTags(_downloadInfoDict[key], (req as GetRequest).FilePath, File.Exists(coverArtTagFilePath) ? coverArtTagFilePath : Path.Combine(trackPath, "Cover.jpg"), _logger);
                _logger.AddDownloadLogLine($"Track {(req as GetRequest).Filename} Download Done!\r\n", true, true);
            },
            RequestCancelled = req => HandleDownloadException(req.Exception, "Track Download canceled, probably due to network error or request timeout."),
            RequestFailed = (req, _) => HandleDownloadException(req.Exception, "Unknown error during Track Download.")
        });

        private GetRequest DownloadCoverArt(bool isPartOfTracklist, string key)
        {
            string coverArtTagFilePath = Path.Combine(_downloadPathsDict[key].Path3Full, Globals.TaggingOptions.ArtSize + ".jpg");
            if (!File.Exists(coverArtTagFilePath))
                return new(_downloadInfoDict[key].FrontCoverImgTagUrl, new GetRequestOptions
                {
                    IsDownload = true,
                    Priority = RequestPriority.High,
                    SpeedReporter = new SpeedReporter<long>(),
                    DirectoryPath = _downloadPathsDict[key].Path3Full,
                    Filename = Globals.TaggingOptions.ArtSize + ".jpg",
                    NumberOfAttempts = 2,
                    RequestFailed = (req, _) => _logger.AddDownloadErrorLogLines(["Error downloading image file for tagging.", req.Exception.Message, Environment.NewLine])
                });

            string coverArtFilePath = Path.Combine(_downloadPathsDict[key].Path3Full, "Cover.jpg");

            if (!isPartOfTracklist && !File.Exists(coverArtFilePath))
                return new(_downloadInfoDict[key].FrontCoverImgUrl, new()
                {
                    IsDownload = true,
                    SpeedReporter = new SpeedReporter<long>(),
                    DirectoryPath = _downloadPathsDict[key].Path3Full,
                    Filename = "Cover.jpg",
                    Priority = RequestPriority.High,
                    NumberOfAttempts = 1,
                    RequestFailed = (req, _) => _logger.AddDownloadErrorLogLines(["Error downloading full size cover image file.", req.Exception.Message, Environment.NewLine])
                });
            return null;
        }

        private void RemoveTempTaggingArtFile(string key)
        {
            try
            {
                string coverArtTagFilePath = Path.Combine(_downloadPathsDict[key].Path3Full, Globals.TaggingOptions.ArtSize + ".jpg");
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
            _logger.AddDownloadErrorLogLine(ex?.ToString());
            _logger.AddDownloadErrorLogLine(Environment.NewLine);
        }

        private bool DownloadAlbum(Album qobuzAlbum, string basePath, string albumPathSuffix = "")
        {
            bool noErrorsOccured = true;
            const int tracksLimit = 50;
            qobuzAlbum = ExecuteApiCall(apiService => apiService.GetAlbum(qobuzAlbum.Id, true, null, tracksLimit, 0));

            if (string.IsNullOrEmpty(qobuzAlbum.Id))
                return ReturnFail();

            string albumKey = qobuzAlbum.Id.ToString();
            _downloadInfoDict[albumKey] = new DownloadItemInfo { DownloadItemID = albumKey };
            _downloadInfoDict[albumKey].SetAlbumDownloadInfo(qobuzAlbum);
            _downloadPathsDict[albumKey] = _downloadInfoDict[albumKey].CurrentDownloadPaths;

            Options.UpdateAlbumTagsUi.Invoke(_downloadInfoDict[albumKey]);
            _isAlbum = true;
            int tracksTotal = qobuzAlbum.Tracks.Total ?? 0;
            int tracksPageOffset = qobuzAlbum.Tracks.Offset ?? 0;
            int tracksLoaded = qobuzAlbum.Tracks.Items?.Count ?? 0;
            int i = 0;

            while (i < tracksLoaded)
            {
                if (State != RequestState.Running && State != RequestState.Idle)
                    return false;
                bool isLastTrackOfAlbum = i + tracksPageOffset == tracksTotal - 1;
                Track qobuzTrack = qobuzAlbum.Tracks.Items[i];
                qobuzTrack.Album = qobuzAlbum;

                if (!DownloadTrack(qobuzTrack, basePath, false, isLastTrackOfAlbum, albumPathSuffix))
                    noErrorsOccured = false;

                i++;
                if (i == tracksLoaded && tracksTotal > i + tracksPageOffset)
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

            DownloadBooklets(qobuzAlbum, _downloadPathsDict[albumKey].Path3Full);
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

        private async Task<bool> DownloadAlbums(string basePath, List<Album> albums, bool isEndOfDownloadJob)
        {
            bool noAlbumErrorsOccured = true;
            var albumContainer = new RequestContainer<OwnRequest>();

            if (!_IdContainer.Contains("DownloadAlbums"))
            {
                foreach (Album qobuzAlbum in albums)
                {
                    albumContainer.Add(new OwnRequest((token) =>
                    {
                        //_logger.ClearUiLogComponent();

                        if (!DownloadAlbum(qobuzAlbum, basePath, $" [{qobuzAlbum.Id}]"))
                            noAlbumErrorsOccured = false;
                        return Task.FromResult(true);
                    }, new RequestOptions<VoidStruct, VoidStruct>()
                    {
                        Priority = RequestPriority.Low,
                        NumberOfAttempts = 1,
                        RequestStarted = (req) =>
                        {
                            _logger.AddEmptyDownloadLogLine(true, false);
                            _logger.AddDownloadLogLine($"Creating Download requests for album \"{qobuzAlbum.Title}\" with ID: <{qobuzAlbum.Id}>...", true, true);
                            _logger.AddEmptyDownloadLogLine(true, true);
                        },
                        Handler = RequestHandler.MainRequestHandlers[1]
                    }));
                }
                _IdContainer.Add("DownloadAlbums");
            }

            _requestContainer.Add(albumContainer);
            await albumContainer.Task;
            return noAlbumErrorsOccured;
        }

        private async Task<bool> DownloadReleases(string basePath, List<Release> releases)
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
                await _requestContainer.Task;
            }
            return noAlbumErrorsOccured;
        }

        private async Task<bool> DownloadArtistReleases(Artist qobuzArtist, string basePath, string releaseType, bool isEndOfDownloadJob)
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
                if (!await DownloadReleases(basePath, releasesList.Items))
                    noErrorsOccured = false;
                if (releasesList.HasMore)
                {
                    releasesOffset += releasesLimit;
                    releasesList = ExecuteApiCall(apiService => apiService.GetReleaseList(qobuzArtist.Id.ToString(), true, releaseType, "release_date", "desc", 0, releasesLimit, releasesOffset));
                }
                else
                    continueDownload = false;
            }
            return noErrorsOccured;
        }

        private async Task<bool> StartDownloadTrackTaskAsync()
        {
            _logger.AddDownloadLogLine("Grabbing Track info...\r\n", true, true);
            string downloadBasePath = Settings.Default.savedFolder;
            try
            {
                Track qobuzTrack = ExecuteApiCall(apiService => apiService.GetTrack(_downloadInfoDict[_downloadItem.Id.ToString()].DownloadItemID, true));

                if (qobuzTrack == null)
                    return ReturnFail();

                _logger.AddDownloadLogLine($"Track \"{qobuzTrack.Title}\" found. Starting Download...\r\n", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);

                if (!DownloadTrack(qobuzTrack, downloadBasePath, true, true))
                    return false;
                return await WaitOnCompletion();
            }
            catch (Exception downloadEx)
            {
                _logger.LogDownloadTaskException("Track", downloadEx);
                return false;
            }
        }

        private async Task<bool> StartDownloadAlbumTaskAsync()
        {
            _logger.AddDownloadLogLine("Grabbing Album info...", true, true);
            string downloadBasePath = Settings.Default.savedFolder;
            try
            {
                Album qobuzAlbum = ExecuteApiCall(apiService => apiService.GetAlbum(_downloadInfoDict[_downloadItem.Id.ToString()].DownloadItemID, true, null, 0));
                if (qobuzAlbum == null)
                    return ReturnFail();
                _logger.AddDownloadLogLine($"Album \"{qobuzAlbum.Title}\" found. Starting Downloads...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);

                bool finished = DownloadAlbum(qobuzAlbum, downloadBasePath);
                return await WaitOnCompletion();
            }
            catch (Exception downloadEx)
            {
                _logger.LogDownloadTaskException("Album", downloadEx);
                return false;
            }
        }

        private async Task<bool> StartDownloadArtistDiscogTaskAsync()
        {
            string artistBasePath = Settings.Default.savedFolder;
            _logger.AddDownloadLogLine("Grabbing Artist info...", true, true);
            try
            {
                Artist qobuzArtist = ExecuteApiCall(apiService => apiService.GetArtist(_downloadInfoDict[_downloadItem.Id.ToString()].DownloadItemID, true));
                if (qobuzArtist == null)
                    return ReturnFail();
                _logger.AddDownloadLogLine($"Starting Downloads for artist \"{qobuzArtist.Name}\" with ID: <{qobuzArtist.Id}>...", true, true);
                await DownloadArtistReleases(qobuzArtist, artistBasePath, "all", true);
                return await WaitOnCompletion();
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Artist", downloadEx);
                return false;
            }
        }

        private async Task<bool> StartDownloadLabelTaskAsync()
        {
            string labelBasePath = Path.Combine(Settings.Default.savedFolder, "- Labels");
            _logger.AddDownloadLogLine("Grabbing Label albums...\r\n", true, true);
            try
            {
                Label qobuzLabel = null;
                List<Album> labelAlbums = new();
                const int albumLimit = 500;
                int albumsOffset = 0;
                while (true)
                {
                    qobuzLabel = ExecuteApiCall(apiService => apiService.GetLabel(_downloadInfoDict[_downloadItem.Id.ToString()].DownloadItemID, true, "albums", albumLimit, albumsOffset));
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
                    _logger.AddDownloadLogLine($"No albums found for label \"{qobuzLabel.Name}\" with ID: <{qobuzLabel.Id}>, nothing to download.\r\n", true, true);
                    return ReturnFail();
                }

                _logger.AddDownloadLogLine($"Starting Downloads for label \"{qobuzLabel.Name}\" with ID: <{qobuzLabel.Id}>...\r\n", true, true);
                string safeLabelName = StringTools.GetSafeFilename(StringTools.DecodeEncodedNonAsciiCharacters(qobuzLabel.Name));
                labelBasePath = Path.Combine(labelBasePath, safeLabelName);
                await DownloadAlbums(labelBasePath, labelAlbums, true);
                return await WaitOnCompletion();
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Label", downloadEx);
                return false;
            }
        }

        private async Task<bool> StartDownloadFaveAlbumsTaskAsync()
        {
            string favoritesBasePath = Path.Combine(Settings.Default.savedFolder, "- Favorites");
            _logger.AddDownloadLogLine("Grabbing Favorite Albums...\r\n", true, true);
            try
            {
                List<Album> favoriteAlbums = new();
                const int albumLimit = 500;
                int albumsOffset = 0;
                while (true)
                {
                    UserFavorites qobuzUserFavorites = ExecuteApiCall(apiService => apiService.GetUserFavorites(_downloadInfoDict[_downloadItem.Id.ToString()].DownloadItemID, "albums", albumLimit, albumsOffset));
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
                return await WaitOnCompletion();
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Favorite Albums", downloadEx);
                return false;
            }
        }

        private async Task<bool> StartDownloadFaveArtistsTaskAsync()
        {
            string favoritesBasePath = Path.Combine(Settings.Default.savedFolder, "- Favorites");
            _logger.AddDownloadLogLine("Grabbing Favorite Artists...", true, true);

            try
            {
                UserFavoritesIds qobuzUserFavoritesIds = ExecuteApiCall(apiService => apiService.GetUserFavoriteIds(_downloadInfoDict[_downloadItem.Id.ToString()].DownloadItemID));
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
                        continue;
                    }
                    _logger.AddEmptyDownloadLogLine(true, true);
                    _logger.AddDownloadLogLine($"Starting Downloads for artist \"{qobuzArtist.Name}\" with ID: <{qobuzArtist.Id}>...", true, true);
                    await DownloadArtistReleases(qobuzArtist, favoritesBasePath, "all", false);
                }
                return await WaitOnCompletion();
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Favorite Albums", downloadEx);
                return false;
            }
        }

        private async Task<bool> StartDownloadFaveTracksTaskAsync()
        {
            string favoriteTracksBasePath = Path.Combine(Settings.Default.savedFolder, "- Favorites");
            _logger.AddDownloadLogLine("Grabbing Favorite Tracks...", true, true);
            _logger.AddEmptyDownloadLogLine(true, true);
            try
            {
                UserFavoritesIds qobuzUserFavoritesIds = ExecuteApiCall(apiService => apiService.GetUserFavoriteIds(_downloadInfoDict[_downloadItem.Id.ToString()].DownloadItemID));
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
                        continue;
                    DownloadTrack(qobuzTrack, favoriteTracksBasePath, true, true);
                }
                return await WaitOnCompletion();
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Playlist", downloadEx);
                return false;
            }
        }

        private async Task<bool> StartDownloadPlaylistTaskAsync()
        {
            string playlistBasePath = Settings.Default.savedFolder;
            _logger.AddDownloadLogLine("Grabbing Playlist tracks...", true, true);
            _logger.AddEmptyDownloadLogLine(true, true);
            try
            {
                Playlist qobuzPlaylist = ExecuteApiCall(apiService => apiService.GetPlaylist(_downloadInfoDict[_downloadItem.Id.ToString()].DownloadItemID, true, "track_ids", 10000));
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
                        SpeedReporter = new SpeedReporter<long>(),
                        IsDownload = true,
                        RequestFailed = (req, _) => _logger.AddDownloadErrorLogLines(["Error downloading full size playlist cover image file.", req.Exception.Message, "\r\n"])
                    }));
                }
                M3uPlaylist m3uPlaylist = new();
                m3uPlaylist.IsExtended = true;
                foreach (long trackId in qobuzPlaylist.TrackIds)
                {
                    if (State != RequestState.Running)
                        return false;
                    Track qobuzTrack = ExecuteApiCall(apiService => apiService.GetTrack(trackId.ToString(), true));
                    if (qobuzTrack == null)
                        continue;
                    if (!IsStreamable(qobuzTrack, true))
                        continue;
                    DownloadTrack(qobuzTrack, playlistBasePath, true, true);
                    AddTrackToPlaylistFile(m3uPlaylist, _downloadInfoDict[trackId.ToString()], _downloadPathsDict[trackId.ToString()]);
                }
                string m3uPlaylistFile = Path.Combine(playlistBasePath, $"{playlistSafeName}.m3u8");
                File.WriteAllText(m3uPlaylistFile, PlaylistToTextHelper.ToText(m3uPlaylist), System.Text.Encoding.UTF8);
                return await WaitOnCompletion();
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Playlist", downloadEx);
                return false;
            }
        }

        public void AddTrackToPlaylistFile(M3uPlaylist m3uPlaylist, DownloadItemInfo downloadInfo, DownloadItemPaths downloadPaths)
        {
            if (!File.Exists(downloadPaths.FullTrackFilePath)) return;
            m3uPlaylist.PlaylistEntries.Add(new M3uPlaylistEntry()
            {
                Path = downloadPaths.FullTrackFilePath,
                Duration = TimeSpan.FromSeconds(downloadInfo.Duration),
                Title = $"{downloadInfo.PerformerName} - {downloadInfo.TrackName}"
            });
        }

        public void CreateTrackDirectories(string basePath, string qualityPath, string key, string albumPathSuffix = "", bool forTracklist = false)
        {
            if (forTracklist)
            {
                _downloadPathsDict[key].Path1Full = basePath;
                _downloadPathsDict[key].Path2Full = _downloadPathsDict[key].Path1Full;
                _downloadPathsDict[key].Path3Full = Path.Combine(basePath, qualityPath);
                _downloadPathsDict[key].Path4Full = _downloadPathsDict[key].Path3Full;
            }
            else
            {
                _downloadPathsDict[key].Path1Full = Path.Combine(basePath, _downloadPathsDict[key].AlbumArtistPath);
                _downloadPathsDict[key].Path2Full = Path.Combine(basePath, _downloadPathsDict[key].AlbumArtistPath, _downloadPathsDict[key].AlbumNamePath + albumPathSuffix);
                _downloadPathsDict[key].Path3Full = Path.Combine(basePath, _downloadPathsDict[key].AlbumArtistPath, _downloadPathsDict[key].AlbumNamePath + albumPathSuffix, qualityPath);
                if (_downloadInfoDict[key].DiscTotal > 1)
                {
                    string discFolder = "CD " + _downloadInfoDict[key].DiscNumber.ToString().PadLeft(Math.Max(2, (int)Math.Floor(Math.Log10(_downloadInfoDict[key].DiscTotal) + 1)), '0');
                    _downloadPathsDict[key].Path4Full = Path.Combine(basePath, _downloadPathsDict[key].AlbumArtistPath, _downloadPathsDict[key].AlbumNamePath + albumPathSuffix, qualityPath, discFolder);
                }
                else
                {
                    _downloadPathsDict[key].Path4Full = _downloadPathsDict[key].Path3Full;
                }
            }
            Directory.CreateDirectory(_downloadPathsDict[key].Path4Full);
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