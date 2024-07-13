using PlaylistsNET.Content;
using PlaylistsNET.Models;
using QobuzApiSharp.Exceptions;
using QobuzApiSharp.Models.Content;
using QobuzApiSharp.Service;
using QobuzDownloaderX.Models.Download;
using QobuzDownloaderX.Properties;
using QobuzDownloaderX.Shared.Tools;
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

namespace QobuzDownloaderX.Shared
{
    public class DownloadManager
    {
        private readonly DownloadLogger _logger;
        private CancellationTokenSource _cancellationTokenSource;

        public delegate void DownloadTaskStatusChanged();
        public delegate void UpdateAlbumTagsUi(DownloadItemInfo downloadInfo);
        private readonly UpdateAlbumTagsUi _updateAlbumUiTags;
        public delegate void UpdateDownloadSpeed(string speed);
        private readonly UpdateDownloadSpeed _updateUiDownloadSpeed;

        public DownloadItemInfo DownloadInfo { get; private set; }

        public DownloadItemPaths DownloadPaths { get; private set; }

        public bool IsBusy { get; private set; }

        public bool CheckIfStreamable { get; set; }

        public DownloadManager(DownloadLogger logger, UpdateAlbumTagsUi updateAlbumTagsUi, UpdateDownloadSpeed updateUiDownloadSpeed)
        {
            IsBusy = false;
            CheckIfStreamable = true;
            this._logger = logger;
            _updateUiDownloadSpeed = updateUiDownloadSpeed;
            _updateAlbumUiTags = updateAlbumTagsUi;
        }

        private T ExecuteApiCall<T>(Func<QobuzApiService, T> apiCall)
        {
            try
            {
                return apiCall(QobuzApiServiceManager.GetApiService());
            }
            catch (Exception ex)
            {
                // If connection to API fails, or something is incorrect, show error info + log details.
                var errorLines = new List<string>();

                _logger.AddEmptyDownloadLogLine(false, true);
                _logger.AddDownloadLogErrorLine($"Communication problem with Qobuz API. Details saved to error log{Environment.NewLine}", true, true);

                switch (ex)
                {
                    case ApiErrorResponseException erEx:
                        errorLines.Add("Failed API request:");
                        errorLines.Add(erEx.RequestContent);
                        errorLines.Add($"Api response code: {erEx.ResponseStatusCode}");
                        errorLines.Add($"Api response status: {erEx.ResponseStatus}");
                        errorLines.Add($"Api response reason: {erEx.ResponseReason}");
                        break;
                    case ApiResponseParseErrorException pEx:
                        errorLines.Add("Error parsing API response");
                        errorLines.Add($"Api response content: {pEx.ResponseContent}");
                        break;
                    default:
                        errorLines.Add("Unknown error trying API request:");
                        errorLines.Add($"{ex}");
                        break;
                }

                // Write detailed info to error log
                _logger.AddDownloadErrorLogLines(errorLines);
            }

            return default;
        }

        public void StopDownloadTask()
        {
            _cancellationTokenSource?.Cancel();
        }

        public bool IsStreamable(Track qobuzTrack, bool inPlaylist = false)
        {
            if (qobuzTrack.Streamable != false)
            {
                return true;
            }

            var tryToStream = true;

            switch (CheckIfStreamable)
            {
                case true:
                    var trackReference = inPlaylist
                        ? $"{qobuzTrack.Performer?.Name} - {qobuzTrack.Title}"
                        : $"{qobuzTrack.TrackNumber.GetValueOrDefault()} {StringTools.DecodeEncodedNonAsciiCharacters(qobuzTrack.Title.Trim())}";

                    _logger.AddDownloadLogLine($"Track {trackReference} is not available for streaming. Unable to download.\r\n", true, true);
                    tryToStream = false;

                    break;

                default:
                    _logger.AddDownloadLogLine("Track is not available for streaming. But streamable check is being ignored for debugging, or messed up releases. Attempting to download...\r\n", tryToStream, tryToStream);

                    break;
            }

            return tryToStream;
        }

        public async Task DownloadFileAsync(HttpClient httpClient, string downloadUrl, string filePath)
        {
            using (var streamToReadFrom = await httpClient.GetStreamAsync(downloadUrl))
            {
                using (var streamToWriteTo = File.Create(filePath))
                {
                    long totalBytesRead = 0;
                    var stopwatch = Stopwatch.StartNew();
                    var buffer = new byte[32768]; // Use a 32KB buffer size for copying data
                    var firstBufferRead = false;

                    int bytesRead;
                    while ((bytesRead = await streamToReadFrom.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        // Write only the minimum of buffer.Length and bytesRead bytes to the file
                        await streamToWriteTo.WriteAsync(buffer, 0, Math.Min(buffer.Length, bytesRead));

                        // Calculate download speed
                        totalBytesRead += bytesRead;
                        var speed = totalBytesRead / 1024d / 1024d / stopwatch.Elapsed.TotalSeconds;

                        // Update the downloadSpeedLabel with the current speed at download start and then max. every 100 ms, with 3 decimal places
                        if (!firstBufferRead || stopwatch.ElapsedMilliseconds >= 200)
                        {
                            _updateUiDownloadSpeed.Invoke($"Downloading... {speed:F3} MB/s");
                        }

                        firstBufferRead = true;
                    }
                }
            }

            // After download completes successfully
            _updateUiDownloadSpeed.Invoke("Idle");
        }

        private async Task<bool> DownloadTrackAsync(CancellationToken cancellationToken, Track qobuzTrack, string basePath, bool isPartOfTracklist, bool isPartOfAlbum, bool removeTagArtFileAfterDownload = false, string albumPathSuffix = "")
        {
            // Just for good measure...
            // User requested task cancellation!
            cancellationToken.ThrowIfCancellationRequested();

            var trackIdString = qobuzTrack.Id.GetValueOrDefault().ToString();

            DownloadInfo.SetTrackTaggingInfo(qobuzTrack);

            // If track is downloaded as part of Album, Album related processing should already be done.
            // Only handle Album related processing when downloading a single track.
            if (!isPartOfAlbum)
            {
                // Get all album information and update UI fields via callback
                DownloadInfo.SetAlbumDownloadInfo(qobuzTrack.Album);
                _updateAlbumUiTags.Invoke(DownloadInfo);
            }

            // Check if available for streaming.
            if (!IsStreamable(qobuzTrack))
            {
                return false;
            }

            // Create directories if they don't exist yet
            // Add Album ID to Album Path if requested (to avoid conflicts for similar albums with trimmed long names)
            CreateTrackDirectories(basePath, albumPathSuffix, isPartOfTracklist);

            // Set trackPath to the created directories
            var trackPath = DownloadInfo.CurrentDownloadPaths.Path3Full;

            // Create padded track number string with minimum of 2 integer positions based on number of total tracks
            var paddedTrackNumber = DownloadInfo.TrackNumber.ToString().PadLeft(Math.Max(2, (int)Math.Floor(Math.Log10(DownloadInfo.TrackTotal) + 1)), '0');

            // Create full track filename
            DownloadPaths.FinalTrackNamePath = isPartOfTracklist
                ? string.Concat(DownloadPaths.PerformerNamePath, Globals.FileNameTemplateString, DownloadPaths.TrackNamePath).TrimEnd()
                : string.Concat(paddedTrackNumber, Globals.FileNameTemplateString, DownloadPaths.TrackNamePath).TrimEnd();

            // Shorten full filename if over MaxLength to avoid errors with file names being too long
            DownloadPaths.FinalTrackNamePath = StringTools.TrimToMaxLength(DownloadPaths.FinalTrackNamePath, Globals.MaxLength);

            // Construct Full filename & file path
            DownloadPaths.FullTrackFileName = DownloadPaths.FinalTrackNamePath + Globals.AudioFileType;
            DownloadPaths.FullTrackFilePath = Path.Combine(trackPath, DownloadPaths.FullTrackFileName);

            // Check if the file already exists
            if (File.Exists(DownloadPaths.FullTrackFilePath))
            {
                var message = $"File for \"{DownloadPaths.FinalTrackNamePath}\" already exists. Skipping.\r\n";
                _logger.AddDownloadLogLine(message, true, true);

                return false;
            }

            // Notify UI of starting track download.
            _logger.AddDownloadLogLine($"Downloading - {DownloadPaths.FinalTrackNamePath} ...... ", true, true);

            // Get track streaming URL, abort if failed.
            var streamUrl = ExecuteApiCall(apiService => apiService.GetTrackFileUrl(trackIdString, Globals.FormatIdString))?.Url;

            if (string.IsNullOrEmpty(streamUrl))
            {
                // Can happen with free accounts trying to download non-previewable tracks (or if API call failed). 
                _logger.AddDownloadLogLine($"Couldn't get streaming URL for Track \"{DownloadPaths.FinalTrackNamePath}\". Skipping.\r\n", true, true);

                return false;
            }

            try
            {
                // Create file path strings
                var coverArtFilePath = Path.Combine(DownloadPaths.Path3Full, "Cover.jpg");
                var coverArtTagFilePath = Path.Combine(DownloadPaths.Path3Full, Globals.TaggingOptions.ArtSize + ".jpg");

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", Globals.USER_AGENT);

                    // Save streamed file from link
                    await DownloadFileAsync(httpClient, streamUrl, DownloadPaths.FullTrackFilePath);

                    // Download selected cover art size for tagging files (if not exists)
                    if (!File.Exists(coverArtTagFilePath))
                    {
                        try
                        {
                            await DownloadFileAsync(httpClient, DownloadInfo.FrontCoverImgTagUrl, coverArtTagFilePath);
                        }
                        catch (Exception ex)
                        {
                            // Qobuz servers throw a 404 as if the image doesn't exist.
                            _logger.AddDownloadErrorLogLines(new[] { "Error downloading image file for tagging.", ex.Message, Environment.NewLine });
                        }
                    }

                    // Download max quality Cover Art to "Cover.jpg" file in chosen path (if not exists & not part of playlist).
                    if (!isPartOfTracklist && !File.Exists(coverArtFilePath))
                    {
                        try
                        {
                            await DownloadFileAsync(httpClient, DownloadInfo.FrontCoverImgUrl, coverArtFilePath);
                        }
                        catch (Exception ex)
                        {
                            // Qobuz servers throw a 404 as if the image doesn't exist.
                            _logger.AddDownloadErrorLogLines(new[] { "Error downloading full size cover image file.", ex.Message, Environment.NewLine });
                        }
                    }
                }

                // Tag metadata to downloaded track.
                AudioFileTagger.AddMetaDataTags(DownloadInfo, DownloadPaths.FullTrackFilePath, coverArtTagFilePath, _logger);

                // Remove temp tagging art file if requested and exists.
                if (removeTagArtFileAfterDownload && File.Exists(coverArtTagFilePath))
                {
                    File.Delete(coverArtTagFilePath);
                }

                _logger.AddDownloadLogLine("Track Download Done!\r\n", true, true);
                Thread.Sleep(100);
            }
            catch (AggregateException ae)
            {
                // When a Task fails, an AggregateException is thrown. Could be a HttpClient timeout or network error.
                _logger.AddDownloadLogErrorLine($"Track Download canceled, probably due to network error or request timeout. Details saved to error log.{Environment.NewLine}", true, true);

                _logger.AddDownloadErrorLogLine("Track Download canceled, probably due to network error or request timeout.");
                _logger.AddDownloadErrorLogLine(ae.ToString());
                _logger.AddDownloadErrorLogLine(Environment.NewLine);

                return false;
            }
            catch (Exception downloadEx)
            {
                // If there is an unknown issue trying to, or during the download, show and log error info.
                _logger.AddDownloadLogErrorLine($"Unknown error during Track Download. Details saved to error log.{Environment.NewLine}", true, true);

                _logger.AddDownloadErrorLogLine("Unknown error during Track Download.");
                _logger.AddDownloadErrorLogLine(downloadEx.ToString());
                _logger.AddDownloadErrorLogLine(Environment.NewLine);

                return false;
            }

            return true;
        }

        private async Task<bool> DownloadAlbumAsync(CancellationToken cancellationToken, Album qobuzAlbum, string basePath, string albumPathSuffix = "")
        {
            var noErrorsOccurred = true;

            // Get Album model object with first batch of tracks
            const int tracksLimit = 50;
            qobuzAlbum = ExecuteApiCall(apiService => apiService.GetAlbum(qobuzAlbum.Id, true, null, tracksLimit, 0));

            // If API call failed, abort Album Download
            if (string.IsNullOrEmpty(qobuzAlbum.Id))
            {
                return false;
            }

            // Get all album information and update UI fields via callback
            DownloadInfo.SetAlbumDownloadInfo(qobuzAlbum);
            _updateAlbumUiTags.Invoke(DownloadInfo);

            // Download all tracks of the Album in batches of {tracksLimit}, clean albumArt tag file after last track
            var tracksTotal = qobuzAlbum.Tracks.Total ?? 0;
            var tracksPageOffset = qobuzAlbum.Tracks.Offset ?? 0;
            var tracksLoaded = qobuzAlbum.Tracks.Items?.Count ?? 0;

            var i = 0
			while (i < tracksLoaded)
            {
                // User requested task cancellation!
                cancellationToken.ThrowIfCancellationRequested();

                var isLastTrackOfAlbum = i + tracksPageOffset == tracksTotal - 1;
                var qobuzTrack = qobuzAlbum.Tracks.Items![i];

                // Nested Album objects in Tracks are not always fully populated, inject current qobuzAlbum in Track to be downloaded
                qobuzTrack.Album = qobuzAlbum;

                if (!await DownloadTrackAsync(cancellationToken, qobuzTrack, basePath, false, true, isLastTrackOfAlbum, albumPathSuffix))
                {
                    noErrorsOccurred = false;
                }
				
				i++;

                if (i != tracksLoaded - 1 || tracksTotal <= i + tracksPageOffset)
                {
                    continue;
                }

                // load next page of tracks
                tracksPageOffset += tracksLimit;
                qobuzAlbum = ExecuteApiCall(apiService => apiService.GetAlbum(qobuzAlbum.Id, true, null, tracksLimit, tracksPageOffset));

                // If API call failed, abort Album Download
                if (string.IsNullOrEmpty(qobuzAlbum.Id))
                {
                    return false;
                }

                // If Album Track Items is empty, Qobuz max API offset might be reached
                if (qobuzAlbum.Tracks?.Items?.Any() != true)
                {
                    break;
                }

                // Reset 0-based counter for looping next batch of tracks
                i = 0;
                tracksLoaded = qobuzAlbum.Tracks.Items?.Count ?? 0;
            }

            // Look for digital booklet(s) in "Goodies"
            // Don't fail on failed "Goodies" downloads, just log...
            if (!await DownloadBookletsAsync(qobuzAlbum, DownloadPaths.Path3Full))
            {
                noErrorsOccurred = false;
            }

            return noErrorsOccurred;
        }

        private async Task<bool> DownloadBookletsAsync(Album qobuzAlbum, string basePath)
        {
            var noErrorsOccurred = true;

            var booklets = qobuzAlbum.Goodies?.Where(g => g.FileFormatId == (int)GoodiesFileType.BOOKLET).ToList();

            if (booklets == null || !booklets.Any())
            {
                // No booklets found, just return
                return true;
            }

            _logger.AddDownloadLogLine($"Goodies found, downloading...{Environment.NewLine}", true, true);

            using var httpClient = new HttpClient();

            var counter = 1;

            foreach (var booklet in booklets)
            {
                var bookletFileName = counter == 1 ? "Digital Booklet.pdf" : $"Digital Booklet {counter}.pdf";
                var bookletFilePath = Path.Combine(basePath, bookletFileName);

                // Download booklet if file doesn't exist yet
                if (File.Exists(bookletFilePath))
                {
                    _logger.AddDownloadLogLine($"Booklet file for \"{bookletFileName}\" already exists. Skipping.{Environment.NewLine}", true, true);
                }
                else
                {
                    // When a booklet download fails, mark error occurred but continue downloading others if they exist 
                    if (!await DownloadBookletAsync(booklet, httpClient, bookletFileName, bookletFilePath))
                    {
                        noErrorsOccurred = false;
                    }
                }

                counter++;
            }

            return noErrorsOccurred;
        }

        private async Task<bool> DownloadBookletAsync(Goody booklet, HttpClient httpClient, string fileName, string filePath)
        {
            var noErrorsOccurred = true;

            try
            {
                // Download booklet
                await DownloadFileAsync(httpClient, booklet.Url, filePath);

                _logger.AddDownloadLogLine($"Booklet \"{fileName}\" download complete!{Environment.NewLine}", true, true);
            }
            catch (AggregateException ae)
            {
                // When a Task fails, an AggregateException is thrown. Could be a HttpClient timeout or network error.
                _logger.AddDownloadLogErrorLine($"Goodies Download canceled, probably due to network error or request timeout. Details saved to error log.{Environment.NewLine}", true, true);

                _logger.AddDownloadErrorLogLine("Goodies Download canceled, probably due to network error or request timeout.");
                _logger.AddDownloadErrorLogLine(ae.ToString());
                _logger.AddDownloadErrorLogLine(Environment.NewLine);
                noErrorsOccurred = false;
            }
            catch (Exception downloadEx)
            {
                // If there is an unknown issue trying to, or during the download, show and log error info.
                _logger.AddDownloadLogErrorLine($"Unknown error during Goodies Download. Details saved to error log.{Environment.NewLine}", true, true);

                _logger.AddDownloadErrorLogLine("Unknown error during Goodies Download.");
                _logger.AddDownloadErrorLogLine(downloadEx.ToString());
                _logger.AddDownloadErrorLogLine(Environment.NewLine);
                noErrorsOccurred = false;
            }

            return noErrorsOccurred;
        }

        private async Task<bool> DownloadAlbumsAsync(CancellationToken cancellationToken, string basePath, List<Album> albums, bool isEndOfDownloadJob)
        {
            var noAlbumErrorsOccurred = true;

            foreach (var qobuzAlbum in albums)
            {
                // User requested task cancellation!
                cancellationToken.ThrowIfCancellationRequested();

                // Empty output, then say Starting Downloads.
                _logger.ClearUiLogComponent();
                _logger.AddEmptyDownloadLogLine(true, false);
                _logger.AddDownloadLogLine($"Starting Downloads for album \"{qobuzAlbum.Title}\" with ID: <{qobuzAlbum.Id}>...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);

                var albumDownloadOk = await DownloadAlbumAsync(cancellationToken, qobuzAlbum, basePath, $" [{qobuzAlbum.Id}]");

                // If album download failed, mark error occurred and continue
                if (!albumDownloadOk)
                {
                    noAlbumErrorsOccurred = false;
                }
            }

            if (isEndOfDownloadJob)
            {
                _logger.LogFinishedDownloadJob(noAlbumErrorsOccurred);
            }

            return noAlbumErrorsOccurred;
        }

        // Convert Release to Album for download.
        private async Task<bool> DownloadReleasesAsync(CancellationToken cancellationToken, string basePath, List<Release> releases)
        {
            var noAlbumErrorsOccurred = true;

            foreach (var qobuzRelease in releases)
            {
                // User requested task cancellation!
                cancellationToken.ThrowIfCancellationRequested();

                // Fetch Album object corresponding to release
                var qobuzAlbum = ExecuteApiCall(apiService => apiService.GetAlbum(qobuzRelease.Id, true, null, 0, 0));

                // If API call failed, mark error occurred and continue with next album
                if (string.IsNullOrEmpty(qobuzAlbum.Id))
                {
                    noAlbumErrorsOccurred = false;
                    continue;
                }

                // Empty output, then say Starting Downloads.
                _logger.ClearUiLogComponent();
                _logger.AddEmptyDownloadLogLine(true, false);
                _logger.AddDownloadLogLine($"Starting Downloads for album \"{qobuzAlbum.Title}\" with ID: <{qobuzAlbum.Id}>...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);

                var albumDownloadOk = await DownloadAlbumAsync(cancellationToken, qobuzAlbum, basePath, $" [{qobuzAlbum.Id}]");

                // If album download failed, mark error occurred and continue
                if (!albumDownloadOk)
                {
                    noAlbumErrorsOccurred = false;
                }
            }

            return noAlbumErrorsOccurred;
        }

        private async Task<bool> DownloadArtistReleasesAsync(CancellationToken cancellationToken, Artist qobuzArtist, string basePath, string releaseType, bool isEndOfDownloadJob)
        {
            var noErrorsOccurred = true;

            // Get ReleasesList model object with first batch of releases
            const int releasesLimit = 100;
            var releasesOffset = 0;
            var releasesList = ExecuteApiCall(apiService => apiService.GetReleaseList(qobuzArtist.Id.ToString(), true, releaseType, "release_date", "desc", 0, releasesLimit, releasesOffset));

            // If API call failed, abort Artist Download
            if (releasesList == null)
            {
                return false;
            }

            var continueDownload = true;

            while (continueDownload)
            {
                // User requested task cancellation!
                cancellationToken.ThrowIfCancellationRequested();

                // If releases download failed, mark artist error occurred and continue with next artist
                if (!await DownloadReleasesAsync(cancellationToken, basePath, releasesList.Items))
                {
                    noErrorsOccurred = false;
                }

                if (releasesList.HasMore)
                {
                    // Fetch next batch of releases
                    releasesOffset += releasesLimit;
                    releasesList = ExecuteApiCall(apiService => apiService.GetReleaseList(qobuzArtist.Id.ToString(), true, releaseType, "release_date", "desc", 0, releasesLimit, releasesOffset));
                }
                else
                {
                    continueDownload = false;
                }
            }

            if (isEndOfDownloadJob)
            {
                _logger.LogFinishedDownloadJob(noErrorsOccurred);
            }

            return noErrorsOccurred;
        }

        public async Task StartDownloadItemTaskAsync(DownloadItem downloadItem, DownloadTaskStatusChanged downloadStartedCallback, DownloadTaskStatusChanged downloadStoppedCallback)
        {
            // Create new cancellation token source.
            using (_cancellationTokenSource = new CancellationTokenSource())
            {
                IsBusy = true;

                try
                {
                    downloadStartedCallback?.Invoke();

                    // Link should be valid here, start new download log
                    _logger.DownloadLogPath = Path.Combine(Globals.LoggingDir, $"Download_Log_{DateTime.Now:yyyy-MM-dd_HH.mm.ss.fff}.log");

                    var logLine = $"Downloading <{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(downloadItem.Type)}> from {downloadItem.Url}";
                    _logger.AddDownloadLogLine(new string('=', logLine.Length).PadRight(logLine.Length), true);
                    _logger.AddDownloadLogLine(logLine, true);
                    _logger.AddDownloadLogLine(new string('=', logLine.Length).PadRight(logLine.Length), true);
                    _logger.AddEmptyDownloadLogLine(true);

                    DownloadInfo = new DownloadItemInfo
                    {
                        DowloadItemID = downloadItem.Id
                    };
                    DownloadPaths = DownloadInfo.CurrentDownloadPaths;

                    switch (downloadItem.Type)
                    {
                        case "track":
                            await StartDownloadTrackTaskAsync(_cancellationTokenSource.Token);
                            break;
                        case "album":
                            await StartDownloadAlbumTaskAsync(_cancellationTokenSource.Token);
                            break;

                        case "artist":
                            await StartDownloadArtistDiscogTaskAsync(_cancellationTokenSource.Token);
                            break;

                        case "label":
                            await StartDownloadLabelTaskAsync(_cancellationTokenSource.Token);
                            break;

                        case "user":
                            switch (DownloadInfo.DowloadItemID)
                            {
                                case @"library/favorites/albums":
                                    await StartDownloadFaveAlbumsTaskAsync(_cancellationTokenSource.Token);

                                    break;

                                case @"library/favorites/artists":
                                    await StartDownloadFaveArtistsTaskAsync(_cancellationTokenSource.Token);

                                    break;

                                case @"library/favorites/tracks":
                                    await StartDownloadFaveTracksTaskAsync(_cancellationTokenSource.Token);

                                    break;

                                default:
                                    _logger.ClearUiLogComponent();
                                    _logger.AddDownloadLogLine($"You entered an invalid user favorites link.{Environment.NewLine}", true, true);
                                    _logger.AddDownloadLogLine($"Favorite Tracks, Albums & Artists are supported with the following links:{Environment.NewLine}", true, true);
                                    _logger.AddDownloadLogLine($"Tracks - https://play.qobuz.com/user/library/favorites/tracks{Environment.NewLine}", true, true);
                                    _logger.AddDownloadLogLine($"Albums - https://play.qobuz.com/user/library/favorites/albums{Environment.NewLine}", true, true);
                                    _logger.AddDownloadLogLine($"Artists - https://play.qobuz.com/user/library/favorites/artists{Environment.NewLine}", true, true);

                                    break;
                            }
                            break;

                        case "playlist":
                            await StartDownloadPlaylistTaskAsync(_cancellationTokenSource.Token);
                            break;
                        default:
                            // We shouldn't get here?!? I'll leave this here just in case...
                            _logger.ClearUiLogComponent();
                            _logger.AddDownloadLogLine("URL not understood. Is there a typo?", true, true);

                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Handle cancellation.
                    _logger.AddEmptyDownloadLogLine(true, true);
                    _logger.AddDownloadLogLine("Download stopped by user!", true, true);
                }
                finally
                {
                    downloadStoppedCallback?.Invoke();
                    IsBusy = false;
                }
            }
        }

        // For downloading "track" links
        private async Task StartDownloadTrackTaskAsync(CancellationToken cancellationToken)
        {
            // Empty screen output, then say Grabbing info.
            _logger.ClearUiLogComponent();
            _logger.AddDownloadLogLine($"Grabbing Track info...{Environment.NewLine}", true, true);

            // Set "basePath" as the selected path.
            var downloadBasePath = Settings.Default.savedFolder;

            try
            {
                var qobuzTrack = ExecuteApiCall(apiService => apiService.GetTrack(DownloadInfo.DowloadItemID, true));

                // If API call failed, abort
                if (qobuzTrack == null) { return; }

                _logger.AddDownloadLogLine($"Track \"{qobuzTrack.Title}\" found. Starting Download...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);

                var fileDownloaded = await DownloadTrackAsync(cancellationToken, qobuzTrack, downloadBasePath, true, false, true);

                // If download failed, abort
                if (!fileDownloaded)
                {
                    return;
                }

                // Say that downloading is completed.
                _logger.AddEmptyDownloadLogLine(true, true);
                _logger.AddDownloadLogLine("Download job completed! All downloaded files will be located in your chosen path.", true, true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Rethrow when cancellation requested by user.
                throw;
            }
            catch (Exception downloadEx)
            {
                _logger.LogDownloadTaskException("Track", downloadEx);
            }
        }

        // For downloading "album" links
        private async Task StartDownloadAlbumTaskAsync(CancellationToken cancellationToken)
        {
            // Empty screen output, then say Grabbing info.
            _logger.ClearUiLogComponent();

            _logger.AddDownloadLogLine($"Grabbing Album info...{Environment.NewLine}", true, true);

            // Set "basePath" as the selected path.
            var downloadBasePath = Settings.Default.savedFolder;

            try
            {
                // Get Album model object without tracks (tracks are loaded in batches later)
                var qobuzAlbum = ExecuteApiCall(apiService => apiService.GetAlbum(DownloadInfo.DowloadItemID, true, null, 0));

                // If API call failed, abort
                if (qobuzAlbum == null) { return; }

                _logger.AddDownloadLogLine($"Album \"{qobuzAlbum.Title}\" found. Starting Downloads...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);

                _logger.LogFinishedDownloadJob(await DownloadAlbumAsync(cancellationToken, qobuzAlbum, downloadBasePath));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Rethrow when cancellation requested by user.
                throw;
            }
            catch (Exception downloadEx)
            {
                _logger.LogDownloadTaskException("Album", downloadEx);
            }
        }

        // For downloading "artist" links
        private async Task StartDownloadArtistDiscogTaskAsync(CancellationToken cancellationToken)
        {
            // Set "basePath" as the selected path.
            var artistBasePath = Settings.Default.savedFolder;

            // Empty output, then say Grabbing IDs.
            _logger.ClearUiLogComponent();
            _logger.AddDownloadLogLine("Grabbing Artist info...", true, true);

            try
            {
                // Get Artist model object
                var qobuzArtist = ExecuteApiCall(apiService => apiService.GetArtist(DownloadInfo.DowloadItemID, true));

                // If API call failed, abort
                if (qobuzArtist == null) { return; }

                _logger.AddDownloadLogLine($"Starting Downloads for artist \"{qobuzArtist.Name}\" with ID: <{qobuzArtist.Id}>...", true, true);

                await DownloadArtistReleasesAsync(cancellationToken, qobuzArtist, artistBasePath, "all", true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Rethrow when cancellation requested by user.
                throw;
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Artist", downloadEx);
            }
        }

        // For downloading "label" links
        private async Task StartDownloadLabelTaskAsync(CancellationToken cancellationToken)
        {
            // Set "basePath" as the selected path + "/- Labels".
            var labelBasePath = Path.Combine(Settings.Default.savedFolder, "- Labels");

            // Empty output, then say Grabbing IDs.
            _logger.ClearUiLogComponent();
            _logger.AddDownloadLogLine("Grabbing Label albums...", true, true);

            try
            {
                // Initialise full Album list
                Label qobuzLabel = null;
                var labelAlbums = new List<Album>();
                const int albumLimit = 500;
                var albumsOffset = 0;

                while (true)
                {
                    // Get Label model object with albums
                    qobuzLabel = ExecuteApiCall(apiService => apiService.GetLabel(DownloadInfo.DowloadItemID, true, "albums", albumLimit, albumsOffset));

                    // If API call failed, abort
                    if (qobuzLabel == null) { return; }

                    // If resulting Label has no Album Items, Qobuz API maximum offset is reached
                    if (qobuzLabel.Albums?.Items?.Any() != true)
                    {
                        break;
                    }

                    labelAlbums.AddRange(qobuzLabel.Albums.Items);

                    // Exit loop when all albums are loaded or the Qobuz imposed limit of 10000 is reached
                    if ((qobuzLabel.Albums?.Total ?? 0) == labelAlbums.Count)
                    {
                        break;
                    }

                    albumsOffset += albumLimit;
                }

                // If label has no albums, log and abort
                if (!labelAlbums.Any())
                {
                    _logger.AddDownloadLogLine($"No albums found for label \"{qobuzLabel.Name}\" with ID: <{qobuzLabel.Id}>, nothing to download.", true, true);
                    return;
                }

                _logger.AddDownloadLogLine($"Starting Downloads for label \"{qobuzLabel.Name}\" with ID: <{qobuzLabel.Id}>...", true, true);

                // Add Label name to basePath
                var safeLabelName = StringTools.GetSafeFilename(StringTools.DecodeEncodedNonAsciiCharacters(qobuzLabel.Name));
                labelBasePath = Path.Combine(labelBasePath, safeLabelName);

                await DownloadAlbumsAsync(cancellationToken, labelBasePath, labelAlbums, true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Rethrow when cancellation requested by user.
                throw;
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Label", downloadEx);
            }
        }

        // For downloading "favorites"

        // Favorite Albums
        private async Task StartDownloadFaveAlbumsTaskAsync(CancellationToken cancellationToken)
        {
            // Set "basePath" as the selected path + "/- Favorites".
            var favoritesBasePath = Path.Combine(Settings.Default.savedFolder, "- Favorites");

            // Empty output, then say Grabbing IDs.
            _logger.ClearUiLogComponent();
            _logger.AddDownloadLogLine("Grabbing Favorite Albums...", true, true);

            try
            {
                // Initialise full Album list
                var favoriteAlbums = new List<Album>();
                const int albumLimit = 500;
                var albumsOffset = 0;

                while (true)
                {
                    // Get UserFavorites model object with albums
                    var qobuzUserFavorites = ExecuteApiCall(apiService => apiService.GetUserFavorites(DownloadInfo.DowloadItemID, "albums", albumLimit, albumsOffset));

                    // If API call failed, abort
                    if (qobuzUserFavorites == null) { return; }

                    // If resulting UserFavorites has no Album Items, Qobuz API maximum offset is reached
                    if (qobuzUserFavorites.Albums?.Items?.Any() != true)
                    {
                        break;
                    }

                    favoriteAlbums.AddRange(qobuzUserFavorites.Albums.Items);

                    // Exit loop when all albums are loaded
                    if ((qobuzUserFavorites.Albums?.Total ?? 0) == favoriteAlbums.Count)
                    {
                        break;
                    }

                    albumsOffset += albumLimit;
                }

                // If user has no favorite albums, log and abort
                if (!favoriteAlbums.Any())
                {
                    _logger.AddDownloadLogLine("No favorite albums found, nothing to download.", true, true);
                    return;
                }

                // Download all favorite albums
                await DownloadAlbumsAsync(cancellationToken, favoritesBasePath, favoriteAlbums, true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Rethrow when cancellation requested by user.
                throw;
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Favorite Albums", downloadEx);
            }
        }

        // Favorite Artists
        private async Task StartDownloadFaveArtistsTaskAsync(CancellationToken cancellationToken)
        {
            // Set "basePath" as the selected path + "/- Favorites".
            var favoritesBasePath = Path.Combine(Settings.Default.savedFolder, "- Favorites");

            // Empty output, then say Grabbing IDs.
            _logger.ClearUiLogComponent();
            _logger.AddDownloadLogLine("Grabbing Favorite Artists...", true, true);

            try
            {
                var noArtistErrorsOccurred = true;

                // Get UserFavoritesIds model object, getting Id's allows all results at once.
                var qobuzUserFavoritesIds = ExecuteApiCall(apiService => apiService.GetUserFavoriteIds(DownloadInfo.DowloadItemID));

                // If API call failed, abort
                if (qobuzUserFavoritesIds == null) { return; }

                // If user has no favorite artists, log and abort
                if (qobuzUserFavoritesIds.Artists?.Any() != true)
                {
                    _logger.AddDownloadLogLine("No favorite artists found, nothing to download.", true, true);
                    return;
                }

                // Download favorite artists
                foreach (var favoriteArtistId in qobuzUserFavoritesIds.Artists)
                {
                    // User requested task cancellation!
                    cancellationToken.ThrowIfCancellationRequested();

                    // Get Artist model object
                    var qobuzArtist = ExecuteApiCall(apiService => apiService.GetArtist(favoriteArtistId.ToString(), true));

                    // If API call failed, mark artist error occurred and continue with next artist
                    if (qobuzArtist == null) { noArtistErrorsOccurred = false; continue; }

                    _logger.AddEmptyDownloadLogLine(true, true);
                    _logger.AddDownloadLogLine($"Starting Downloads for artist \"{qobuzArtist.Name}\" with ID: <{qobuzArtist.Id}>...", true, true);

                    // If albums download failed, mark artist error occurred and continue with next artist
                    if (!await DownloadArtistReleasesAsync(cancellationToken, qobuzArtist, favoritesBasePath, "all", false))
                    {
                        noArtistErrorsOccurred = false;
                    }
                }

                _logger.LogFinishedDownloadJob(noArtistErrorsOccurred);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Rethrow when cancellation requested by user.
                throw;
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Favorite Albums", downloadEx);
            }
        }

        // Favorite Tracks
        private async Task StartDownloadFaveTracksTaskAsync(CancellationToken cancellationToken)
        {
            // Set "basePath" as the selected path + "/- Favorites".
            var favoriteTracksBasePath = Path.Combine(Settings.Default.savedFolder, "- Favorites");

            // Empty screen output, then say Grabbing info.
            _logger.ClearUiLogComponent();
            _logger.AddDownloadLogLine("Grabbing Favorite Tracks...", true, true);
            _logger.AddEmptyDownloadLogLine(true, true);

            try
            {
                var noTrackErrorsOccurred = true;

                // Get UserFavoritesIds model object, getting Id's allows all results at once.
                var qobuzUserFavoritesIds = ExecuteApiCall(apiService => apiService.GetUserFavoriteIds(DownloadInfo.DowloadItemID));

                // If API call failed, abort
                if (qobuzUserFavoritesIds == null) { return; }

                // If user has no favorite tracks, log and abort
                if (qobuzUserFavoritesIds.Tracks?.Any() != true)
                {
                    _logger.AddDownloadLogLine("No favorite tracks found, nothing to download.", true, true);
                    return;
                }

                _logger.AddDownloadLogLine("Favorite tracks found. Starting Downloads...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);

                // Download favorite tracks
                foreach (var favoriteTrackId in qobuzUserFavoritesIds.Tracks)
                {
                    // User requested task cancellation!
                    cancellationToken.ThrowIfCancellationRequested();

                    var qobuzTrack = ExecuteApiCall(apiService => apiService.GetTrack(favoriteTrackId.ToString(), true));

                    // If API call failed, log and continue with next track
                    if (qobuzTrack == null)
                    {
                        noTrackErrorsOccurred = false;

                        continue;
                    }

                    if (!await DownloadTrackAsync(cancellationToken, qobuzTrack, favoriteTracksBasePath, true, false, true))
                    {
                        noTrackErrorsOccurred = false;
                    }
                }

                _logger.LogFinishedDownloadJob(noTrackErrorsOccurred);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Rethrow when cancellation requested by user.
                throw;
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Playlist", downloadEx);
            }
        }

        // For downloading "playlist" links
        private async Task StartDownloadPlaylistTaskAsync(CancellationToken cancellationToken)
        {
            // Set "basePath" as the selected path.
            var playlistBasePath = Settings.Default.savedFolder;

            // Empty screen output, then say Grabbing info.
            _logger.ClearUiLogComponent();
            _logger.AddDownloadLogLine("Grabbing Playlist tracks...", true, true);
            _logger.AddEmptyDownloadLogLine(true, true);

            try
            {
                // Get Playlist model object with all track_ids
                var qobuzPlaylist = ExecuteApiCall(apiService => apiService.GetPlaylist(DownloadInfo.DowloadItemID, true, "track_ids", 10000));

                // If API call failed, abort
                if (qobuzPlaylist == null) { return; }

                // If playlist empty, log and abort
                if (qobuzPlaylist.TrackIds?.Any() != true)
                {
                    _logger.AddDownloadLogLine($"Playlist \"{qobuzPlaylist.Name}\" is empty, nothing to download.", true, true);

                    return;
                }

                _logger.AddDownloadLogLine($"Playlist \"{qobuzPlaylist.Name}\" found. Starting Downloads...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);

                // Create Playlist root directory.
                var playlistSafeName = StringTools.GetSafeFilename(StringTools.DecodeEncodedNonAsciiCharacters(qobuzPlaylist.Name));
                var playlistNamePath = StringTools.TrimToMaxLength(playlistSafeName, Globals.MaxLength);
                playlistBasePath = Path.Combine(playlistBasePath, "- Playlists", playlistNamePath);
                Directory.CreateDirectory(playlistBasePath);

                // Download Playlist cover art to "Playlist.jpg" in root directory (if not exists)
                var coverArtFilePath = Path.Combine(playlistBasePath, "Playlist.jpg");

                if (!File.Exists(coverArtFilePath))
                {
                    try
                    {
                        using var imgClient = new WebClient();

                        imgClient.DownloadFile(new Uri(qobuzPlaylist.ImageRectangle.FirstOrDefault()), coverArtFilePath);
                    }
                    catch (Exception ex)
                    {
                        // Qobuz servers throw a 404 as if the image doesn't exist.
                        _logger.AddDownloadErrorLogLines(new[] { "Error downloading full size playlist cover image file.", ex.Message, "\r\n" });
                    }
                }

                var noTrackErrorsOccurred = true;

                // Start new m3u Playlist file.
                var m3UPlaylist = new M3uPlaylist
                {
                    IsExtended = true
                };

                // Download Playlist tracks
                foreach (var trackId in qobuzPlaylist.TrackIds)
                {
                    // User requested task cancellation!
                    cancellationToken.ThrowIfCancellationRequested();

                    // Fetch full Track info
                    var qobuzTrack = ExecuteApiCall(apiService => apiService.GetTrack(trackId.ToString(), true));

                    // If API call failed, log and continue with next track
                    if (qobuzTrack == null) { noTrackErrorsOccurred = false; continue; }

                    if (!IsStreamable(qobuzTrack, true))
                    {
                        continue;
                    }

                    if (!await DownloadTrackAsync(cancellationToken, qobuzTrack, playlistBasePath, true, false, true))
                    {
                        noTrackErrorsOccurred = false;
                    }

                    AddTrackToPlaylistFile(m3UPlaylist, DownloadInfo, DownloadPaths);
                }

                // Write m3u playlist to file, override if exists
                var m3UPlaylistFile = Path.Combine(playlistBasePath, $"{playlistSafeName}.m3u8");
                File.WriteAllText(m3UPlaylistFile, PlaylistToTextHelper.ToText(m3UPlaylist), System.Text.Encoding.UTF8);

                _logger.LogFinishedDownloadJob(noTrackErrorsOccurred);

            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Rethrow when cancellation requested by user.
                throw;
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Playlist", downloadEx);
            }
        }

        public void AddTrackToPlaylistFile(M3uPlaylist m3UPlaylist, DownloadItemInfo downloadInfo, DownloadItemPaths downloadPaths)
        {
            // If the TrackFile doesn't exist, skip.
            if (!File.Exists(downloadPaths.FullTrackFilePath))
            {
                return;
            }

            // Add successfully downloaded file to m3u playlist
            m3UPlaylist.PlaylistEntries.Add(new M3uPlaylistEntry()
            {
                Path = downloadPaths.FullTrackFilePath,
                Duration = TimeSpan.FromSeconds(DownloadInfo.Duration),
                Title = $"{downloadInfo.PerformerName} - {downloadInfo.TrackName}"
            });
        }

        public void CreateTrackDirectories(string basePath, string albumPathSuffix = "", bool forTracklist = false)
        {
            if (forTracklist)
            {
                DownloadPaths.Path1Full = basePath;
                DownloadPaths.Path2Full = DownloadPaths.Path1Full;
                DownloadPaths.Path3Full = DownloadPaths.Path1Full;
            }
            else
            {
                DownloadPaths.Path1Full = Path.Combine(basePath, DownloadPaths.AlbumArtistPath);
                DownloadPaths.Path2Full = Path.Combine(basePath, DownloadPaths.AlbumArtistPath, DownloadPaths.AlbumNamePath + albumPathSuffix);
                DownloadPaths.Path3Full = DownloadPaths.Path2Full;

                // If more than 1 disc, create folders for discs. Otherwise, strings will remain null
                // Pad discnumber with minimum of 2 integer positions based on total number of disks
                if (DownloadInfo.DiscTotal > 1)
                {
                    // Create strings for disc folders
                    var discFolder = "CD " + DownloadInfo.DiscNumber.ToString().PadLeft(Math.Max(2, (int)Math.Floor(Math.Log10(DownloadInfo.DiscTotal) + 1)), '0');
                    DownloadPaths.Path3Full = Path.Combine(DownloadPaths.Path2Full, discFolder);
                }
            }

            Directory.CreateDirectory(DownloadPaths.Path3Full);
        }
    }
}