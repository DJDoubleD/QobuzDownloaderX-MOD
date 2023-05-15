﻿using QobuzApiSharp.Models.Content;
using QobuzDownloaderX.Properties;
using QobuzDownloaderX.Shared;
using QobuzDownloaderX.View;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TagLib;

namespace QobuzDownloaderX
{
    public partial class QobuzDownloaderX : HeadlessForm
    {
        public readonly string downloadErrorLogPath = Path.Combine(Globals.LoggingDir, "Download_Errors.log");

        public QobuzDownloaderX()
        {
            InitializeComponent();

            // Remove previous download error log
            if (System.IO.File.Exists(downloadErrorLogPath))
            {
                System.IO.File.Delete(downloadErrorLogPath);
            }
        }

        public string DownloadLogPath { get; set; }

        public string ArtSize { get; set; }
        public string FileNameTemplateString { get; set; }
        public string FinalTrackNamePath { get; set; }
        public string FinalTrackNameVersionPath { get; set; }
        public string QualityPath { get; set; }
        public int MaxLength { get; set; }
        public int DevClickEggThingValue { get; set; }
        public int DebugMode { get; set; }

        // Important strings
        public string DowloadItemID { get; set; }

        public string Stream { get; set; }

        // Info strings for creating paths
        public string AlbumArtistPath { get; set; }
        public string PerformerNamePath { get; set; }
        public string AlbumNamePath { get; set; }
        public string TrackNamePath { get; set; }
        public string VersionNamePath { get; set; }
        public string Path1Full { get; set; }
        public string Path2Full { get; set; }
        public string Path3Full { get; set; }
        public string Path4Full { get; set; }

        // Info / Tagging strings
        public string TrackVersionName { get; set; }
        public bool? Advisory { get; set; }
        public string AlbumArtist { get; set; }
        public string AlbumName { get; set; }
        public string PerformerName { get; set; }
        public string ComposerName { get; set; }
        public string TrackName { get; set; }
        public string Copyright { get; set; }
        public string Genre { get; set; }
        public string ReleaseDate { get; set; }
        public string Isrc { get; set; }
        public string Upc { get; set; }
        public string FrontCoverImgUrl { get; set; }
        public string FrontCoverImgTagUrl { get; set; }
        public string FrontCoverImgBoxUrl { get; set; }
        public string MediaType { get; set; }

        // Info / Tagging ints
        public int DiscNumber { get; set; }
        public int DiscTotal { get; set; }
        public int TrackNumber { get; set; }
        public int TrackTotal { get; set; }

        // Button color download inactive
        private Color ReadyButtonBackColor = Color.FromArgb(0, 112, 239); // Windows Blue (Azure Blue)

        // Button color download active
        private Color BuzyButtonBackColor = Color.FromArgb(200, 30, 0); // Blue

        private void MainForm_Load(object sender, EventArgs e)
        {
            // Set main form size on launch and bring to center.
            this.Height = 533;
            this.CenterToScreen();

            // Grab profile image
            string profilePic = Convert.ToString(Globals.Login.User.Avatar);
            profilePictureBox.ImageLocation = profilePic.Replace(@"\", null).Replace("s=50", "s=20");

            // Welcome the user after successful login.
            output.Invoke(new Action(() => output.Text = String.Empty));
            output.Invoke(new Action(() => output.AppendText("Welcome " + Globals.Login.User.DisplayName + " (" + Globals.Login.User.Email + ") !\r\n")));
            output.Invoke(new Action(() => output.AppendText("User Zone - " + Globals.Login.User.Zone + "\r\n\r\n")));
            output.Invoke(new Action(() => output.AppendText("Qobuz Credential Description - " + Globals.Login.User.Credential.Description + "\r\n")));
            output.Invoke(new Action(() => output.AppendText("\r\n")));
            output.Invoke(new Action(() => output.AppendText("Qobuz Subscription Details\r\n")));
            output.Invoke(new Action(() => output.AppendText("==========================\r\n")));

            if (Globals.Login.User.Subscription != null)
            {
                output.Invoke(new Action(() => output.AppendText("Offer Type - " + Globals.Login.User.Subscription.Offer + "\r\n")));
                output.Invoke(new Action(() => output.AppendText("Start Date - ")));
                output.Invoke(new Action(() => output.AppendText(Globals.Login.User.Subscription.StartDate != null ? ((DateTimeOffset)Globals.Login.User.Subscription.StartDate).ToString("dd-MM-yyyy") : "?")));
                output.Invoke(new Action(() => output.AppendText("\r\n")));
                output.Invoke(new Action(() => output.AppendText("End Date - ")));
                output.Invoke(new Action(() => output.AppendText(Globals.Login.User.Subscription.StartDate != null ? ((DateTimeOffset)Globals.Login.User.Subscription.EndDate).ToString("dd-MM-yyyy") : "?")));
                output.Invoke(new Action(() => output.AppendText("\r\n")));
                output.Invoke(new Action(() => output.AppendText("Periodicity - " + Globals.Login.User.Subscription.Periodicity + "\r\n")));
                output.Invoke(new Action(() => output.AppendText("==========================\r\n\r\n")));
            }
            else
            {
                output.Invoke(new Action(() => output.AppendText("No active subscriptions, only sample downloads possible!\r\n")));
                output.Invoke(new Action(() => output.AppendText("==========================\r\n\r\n")));
            }

            output.Invoke(new Action(() => output.AppendText("Your user_auth_token has been set for this session!")));

            // Get and display version number.
            verNumLabel.Text = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            // Set a placeholder image for Cover Art box.
            albumArtPicBox.ImageLocation = Globals.DEFAULT_COVER_ART_URL;

            // Change account info for logout button
            string oldText = logoutLabel.Text;
            logoutLabel.Text = oldText.Replace("%name%", Globals.Login.User.DisplayName);

            // Set saved settings to correct places.
            folderBrowserDialog.SelectedPath = Settings.Default.savedFolder;
            albumCheckbox.Checked = Settings.Default.albumTag;
            albumArtistCheckbox.Checked = Settings.Default.albumArtistTag;
            artistCheckbox.Checked = Settings.Default.artistTag;
            commentCheckbox.Checked = Settings.Default.commentTag;
            commentTextbox.Text = Settings.Default.commentText;
            composerCheckbox.Checked = Settings.Default.composerTag;
            copyrightCheckbox.Checked = Settings.Default.copyrightTag;
            discNumberCheckbox.Checked = Settings.Default.discTag;
            discTotalCheckbox.Checked = Settings.Default.totalDiscsTag;
            genreCheckbox.Checked = Settings.Default.genreTag;
            isrcCheckbox.Checked = Settings.Default.isrcTag;
            typeCheckbox.Checked = Settings.Default.typeTag;
            explicitCheckbox.Checked = Settings.Default.explicitTag;
            trackTitleCheckbox.Checked = Settings.Default.trackTitleTag;
            trackNumberCheckbox.Checked = Settings.Default.trackTag;
            trackTotalCheckbox.Checked = Settings.Default.totalTracksTag;
            upcCheckbox.Checked = Settings.Default.upcTag;
            releaseCheckbox.Checked = Settings.Default.yearTag;
            imageCheckbox.Checked = Settings.Default.imageTag;
            mp3Checkbox.Checked = Settings.Default.quality1;
            flacLowCheckbox.Checked = Settings.Default.quality2;
            flacMidCheckbox.Checked = Settings.Default.quality3;
            flacHighCheckbox.Checked = Settings.Default.quality4;
            Globals.FormatIdString = Settings.Default.qualityFormat;
            Globals.AudioFileType = Settings.Default.audioType;
            artSizeSelect.SelectedIndex = Settings.Default.savedArtSize;
            filenameTempSelect.SelectedIndex = Settings.Default.savedFilenameTemplate;
            FileNameTemplateString = Settings.Default.savedFilenameTemplateString;
            MaxLength = Settings.Default.savedMaxLength;

            customFormatIDTextbox.Text = Globals.FormatIdString;

            ArtSize = artSizeSelect.Text;
            maxLengthTextbox.Text = MaxLength.ToString();

            // Check if there's no selected path saved.
            if (folderBrowserDialog.SelectedPath == null || folderBrowserDialog.SelectedPath == "")
            {
                // If there is NOT a saved path.
                output.Invoke(new Action(() => output.AppendText("\r\n\r\n")));
                output.Invoke(new Action(() => output.AppendText("No default path has been set! Remember to Choose a Folder!\r\n")));
            }
            else
            {
                // If there is a saved path.
                output.Invoke(new Action(() => output.AppendText("\r\n\r\n")));
                output.Invoke(new Action(() => output.AppendText("Using the last folder you've selected as your selected path!\r\n")));
                output.Invoke(new Action(() => output.AppendText("\r\n")));
                output.Invoke(new Action(() => output.AppendText("Default Folder:\r\n")));
                output.Invoke(new Action(() => output.AppendText(folderBrowserDialog.SelectedPath + "\r\n")));
            }

            // Run anything put into the debug events (For Testing)
            debuggingEvents(sender, e);
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

            if (logToScreen) output?.Invoke(new Action(() => output.AppendText(logEntry)));

            if (logToFile)
            {
                var logEntries = logEntry.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                    .Select(logLine => string.IsNullOrWhiteSpace(logLine) ? logLine : $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} : {logLine}");

                // Filter out all empty lines exept if the logEntry started with an empty line to avoid blank lines for each newline in UI
                var filteredLogEntries = logEntries.Aggregate(new List<string>(), (accumulator, current) =>
                {
                    if (accumulator.Count == 0 || !string.IsNullOrWhiteSpace(current))
                    {
                        accumulator.Add(current);
                    }

                    return accumulator;
                });

                System.IO.File.AppendAllLines(DownloadLogPath, filteredLogEntries);
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

            System.IO.File.AppendAllLines(downloadErrorLogPath, logEntries);
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
            output.Invoke(new Action(() => output.Text = String.Empty));
            AddDownloadLogErrorLine($"{downloadTaskType} Download Task ERROR. Details saved to error log.{Environment.NewLine}", true, true);

            AddDownloadErrorLogLine($"{downloadTaskType} Download Task ERROR.");
            AddDownloadErrorLogLine(downloadEx.ToString());
            AddDownloadErrorLogLine(Environment.NewLine);
            UpdateControlsDownloadDone();
        }

        public void FinishDownloadJob(bool noErrorsOccured)
        {
            AddEmptyDownloadLogLine(true, true);

            // Say that downloading is completed.
            if (noErrorsOccured)
            {
                AddDownloadLogLine("Download job completed! All downloaded files will be located in your chosen path.", true, true);
            }
            else
            {
                AddDownloadLogLine("Download job completed with warnings and/or errors! Some or all files could be missing!", true, true);
            }

            UpdateControlsDownloadDone();
        }

        private void debuggingEvents(object sender, EventArgs e)
        {
            DevClickEggThingValue = 0;

            // Debug mode for things that are only for testing, or shouldn't be on public releases. At the moment, does nothing.
            if (!Debugger.IsAttached)
            {
                DebugMode = 0;
            }
            else
            {
                DebugMode = 1;
            }

            // Show app_secret value.
            //output.Invoke(new Action(() => output.AppendText("\r\n\r\napp_secret = " + Globals.AppSecret)));

            // Show format_id value.
            //output.Invoke(new Action(() => output.AppendText("\r\n\r\nformat_id = " + Globals.FormatIdString)));
        }

        private void OpenSearch_Click(object sender, EventArgs e)
        {
            Globals.SearchForm.ShowDialog(this);
        }

        private async void DownloadButton_Click(object sender, EventArgs e)
        {
            if (!Globals.DownloadManager.Buzy)
            {
                // Run the StartLinkDownloadTaskAsync method on a background thread & Wait for the task to complete
                await Task.Run(() => Globals.DownloadManager.StartLinkDownloadTaskAsync(sender, e));
            } else
            {
                Globals.DownloadManager.StopDownloadTask();
            }
        }

        private async void DownloadUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;

                // Run the StartLinkDownloadTaskAsync method on a background thread & Wait for the task to complete
                await Task.Run(() => Globals.DownloadManager.StartLinkDownloadTaskAsync(sender, e));
            }
        }

        public void UpdateControlsDownloadBuzy()
        {
            mp3Checkbox.Invoke(new Action(() => mp3Checkbox.AutoCheck = false));
            flacLowCheckbox.Invoke(new Action(() => flacLowCheckbox.AutoCheck = false));
            flacMidCheckbox.Invoke(new Action(() => flacMidCheckbox.AutoCheck = false));
            flacHighCheckbox.Invoke(new Action(() => flacHighCheckbox.AutoCheck = false));

            downloadUrl.Invoke(new Action(() => downloadUrl.Enabled = false));

            selectFolderButton.Invoke(new Action(() => selectFolderButton.Enabled = false));
            openSearchButton.Invoke(new Action(() => openSearchButton.Enabled = false));

            downloadButton.Invoke(new Action(() => {
                downloadButton.Text = "Stop Download";
                downloadButton.BackColor = BuzyButtonBackColor;
            }));
        }

        public void UpdateControlsDownloadDone()
        {
            mp3Checkbox.Invoke(new Action(() => mp3Checkbox.AutoCheck = true));
            flacLowCheckbox.Invoke(new Action(() => flacLowCheckbox.AutoCheck = true));
            flacMidCheckbox.Invoke(new Action(() => flacMidCheckbox.AutoCheck = true));
            flacHighCheckbox.Invoke(new Action(() => flacHighCheckbox.AutoCheck = true));

            downloadUrl.Invoke(new Action(() => downloadUrl.Enabled = true));

            selectFolderButton.Invoke(new Action(() => selectFolderButton.Enabled = true));
            openSearchButton.Invoke(new Action(() => openSearchButton.Enabled = true));

            downloadButton.Invoke(new Action(() => {
                downloadButton.Text = "Download";
                downloadButton.BackColor = ReadyButtonBackColor;
            }));
        }

        private void SelectFolder_Click(object sender, EventArgs e)
        {
            Thread t = new Thread((ThreadStart)(() =>
            {
                // Open Folder Browser to select path & Save the selection
                folderBrowserDialog.ShowDialog();
                Settings.Default.savedFolder = folderBrowserDialog.SelectedPath;
                Settings.Default.Save();
            }));

            // Run your code from a thread that joins the STA Thread
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
        }

        private void OpenFolderButton_Click(object sender, EventArgs e)
        {
            // Open selected folder
            if (string.IsNullOrEmpty(folderBrowserDialog.SelectedPath))
            {
                // If there's no selected path.
                MessageBox.Show("No path selected!", "ERROR",
                MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                UpdateControlsDownloadDone();
            }
            else
            {
                // If selected path doesn't exist, create it. (Will be ignored if it does)
                System.IO.Directory.CreateDirectory(folderBrowserDialog.SelectedPath);
                // Open selected folder
                Process.Start(@folderBrowserDialog.SelectedPath);
            }
        }

        private void OpenLogFolderButton_Click(object sender, EventArgs e)
        {
            // Open log folder. Folder should exist here so no extra check
            Process.Start(@Globals.LoggingDir);
        }

        // Add Metadata to audiofiles in ID3v2 for mp3 and Vorbis Comment for FLAC
        public void AddMetaDataTags(string tagFilePath, string tagCoverArtFilePath)
        {
            // Set file to tag
            var tfile = TagLib.File.Create(tagFilePath);
            tfile.RemoveTags(TagTypes.Id3v1);

            // Use ID3v2.4 as default mp3 tag version
            TagLib.Id3v2.Tag.DefaultVersion = 4;
            TagLib.Id3v2.Tag.ForceDefaultVersion = true;

            switch (Globals.AudioFileType)
            {
                case ".mp3":

                    // For custom / troublesome tags.
                    TagLib.Id3v2.Tag customId3v2 = (TagLib.Id3v2.Tag)tfile.GetTag(TagTypes.Id3v2, true);

                    // Saving cover art to file(s)
                    if (imageCheckbox.Checked)
                    {
                        try
                        {
                            // Define cover art to use for MP3 file(s)
                            TagLib.Id3v2.AttachmentFrame pic = new TagLib.Id3v2.AttachmentFrame
                            {
                                TextEncoding = TagLib.StringType.Latin1,
                                MimeType = System.Net.Mime.MediaTypeNames.Image.Jpeg,
                                Type = TagLib.PictureType.FrontCover,
                                Data = TagLib.ByteVector.FromPath(tagCoverArtFilePath)
                            };

                            // Save cover art to MP3 file.
                            tfile.Tag.Pictures = new TagLib.IPicture[1] { pic };
                            tfile.Save();
                        }
                        catch
                        {
                            AddDownloadLogErrorLine($"Cover art tag failed, .jpg still exists?...{Environment.NewLine}", true, true);
                        }
                    }

                    // Track Title tag, version is already added to name if available
                    if (trackTitleCheckbox.Checked) { tfile.Tag.Title = TrackName; }

                    // Album Title tag, version is already added to name if available
                    if (albumCheckbox.Checked) { tfile.Tag.Album = AlbumName; }

                    // Album Artits tag
                    if (albumArtistCheckbox.Checked) { tfile.Tag.AlbumArtists = new string[] { AlbumArtist }; }

                    // Track Artist tag
                    if (artistCheckbox.Checked) { tfile.Tag.Performers = new string[] { PerformerName }; }

                    // Composer tag
                    if (composerCheckbox.Checked) { tfile.Tag.Composers = new string[] { ComposerName }; }

                    // Release Date tag
                    if (releaseCheckbox.Checked) { ReleaseDate = ReleaseDate.Substring(0, 4); tfile.Tag.Year = UInt32.Parse(ReleaseDate); }

                    // Genre tag
                    if (genreCheckbox.Checked) { tfile.Tag.Genres = new string[] { Genre }; }

                    // Disc Number tag
                    if (discNumberCheckbox.Checked) { tfile.Tag.Disc = Convert.ToUInt32(DiscNumber); }

                    // Total Discs tag
                    if (discTotalCheckbox.Checked) { tfile.Tag.DiscCount = Convert.ToUInt32(DiscTotal); }

                    // Total Tracks tag
                    if (trackTotalCheckbox.Checked) { tfile.Tag.TrackCount = Convert.ToUInt32(TrackTotal); }

                    // Track Number tag
                    // !! Set Track Number after Total Tracks to prevent taglib-sharp from re-formatting the field to a "two-digit zero-filled value" !!
                    if (trackNumberCheckbox.Checked)
                    {
                        // Set TRCK tag manually to prevent using "two-digit zero-filled value"
                        // See https://github.com/mono/taglib-sharp/pull/240 where this change was introduced in taglib-sharp v2.3
                        // Original command: tfile.Tag.Track = Convert.ToUInt32(TrackNumber);
                        customId3v2.SetNumberFrame("TRCK", Convert.ToUInt32(TrackNumber), tfile.Tag.TrackCount);
                    }

                    // Comment tag
                    if (commentCheckbox.Checked) { tfile.Tag.Comment = commentTextbox.Text; }

                    // Copyright tag
                    if (copyrightCheckbox.Checked) { tfile.Tag.Copyright = Copyright; }

                    // ISRC tag
                    if (isrcCheckbox.Checked) { customId3v2.SetTextFrame("TSRC", Isrc); }

                    // Release Type tag
                    if (MediaType != null && typeCheckbox.Checked) { customId3v2.SetTextFrame("TMED", MediaType); }

                    // Save all selected tags to file
                    tfile.Save();

                    break;

                case ".flac":

                    // For custom / troublesome tags.
                    TagLib.Ogg.XiphComment custom = (TagLib.Ogg.XiphComment)tfile.GetTag(TagLib.TagTypes.Xiph);

                    // Saving cover art to file(s)
                    if (imageCheckbox.Checked)
                    {
                        try
                        {
                            // Define cover art to use for FLAC file(s)
                            TagLib.Id3v2.AttachmentFrame pic = new TagLib.Id3v2.AttachmentFrame
                            {
                                TextEncoding = TagLib.StringType.Latin1,
                                MimeType = System.Net.Mime.MediaTypeNames.Image.Jpeg,
                                Type = TagLib.PictureType.FrontCover,
                                Data = TagLib.ByteVector.FromPath(tagCoverArtFilePath)
                            };

                            // Save cover art to FLAC file.
                            tfile.Tag.Pictures = new TagLib.IPicture[1] { pic };
                            tfile.Save();
                        }
                        catch
                        {
                            AddDownloadLogErrorLine($"Cover art tag failed, .jpg still exists?...{Environment.NewLine}", true, true);
                        }
                    }

                    // Track Title tag, version is already added to name if available
                    if (trackTitleCheckbox.Checked) { tfile.Tag.Title = TrackName; }

                    // Album Title tag, version is already added to name if available
                    if (albumCheckbox.Checked) { tfile.Tag.Album = AlbumName; }

                    // Album Artist tag
                    if (albumArtistCheckbox.Checked) { custom.SetField("ALBUMARTIST", AlbumArtist); }

                    // Track Artist tag
                    if (artistCheckbox.Checked) { custom.SetField("ARTIST", PerformerName); }

                    // Composer tag
                    if (composerCheckbox.Checked) { custom.SetField("COMPOSER", ComposerName); }

                    // Release Date tag
                    if (releaseCheckbox.Checked) { custom.SetField("YEAR", ReleaseDate); }

                    // Genre tag
                    if (genreCheckbox.Checked) { tfile.Tag.Genres = new string[] { Genre }; }

                    // Track Number tag
                    if (trackNumberCheckbox.Checked)
                    {
                        tfile.Tag.Track = Convert.ToUInt32(TrackNumber);
                        // Override TRACKNUMBER tag again to prevent using "two-digit zero-filled value"
                        // See https://github.com/mono/taglib-sharp/pull/240 where this change was introduced in taglib-sharp v2.3
                        custom.SetField("TRACKNUMBER", Convert.ToUInt32(TrackNumber));
                    }

                    // Disc Number tag
                    if (discNumberCheckbox.Checked) { tfile.Tag.Disc = Convert.ToUInt32(DiscNumber); }

                    // Total Discs tag
                    if (discTotalCheckbox.Checked) { tfile.Tag.DiscCount = Convert.ToUInt32(DiscTotal); }

                    // Total Tracks tag
                    if (trackTotalCheckbox.Checked) { tfile.Tag.TrackCount = Convert.ToUInt32(TrackTotal); }

                    // Comment tag
                    if (commentCheckbox.Checked) { custom.SetField("COMMENT", commentTextbox.Text); }

                    // Copyright tag
                    if (copyrightCheckbox.Checked) { custom.SetField("COPYRIGHT", Copyright); }
                    // UPC tag
                    if (upcCheckbox.Checked) { custom.SetField("UPC", Upc); }

                    // ISRC tag
                    if (isrcCheckbox.Checked) { custom.SetField("ISRC", Isrc); }

                    // Release Type tag
                    if (MediaType != null && typeCheckbox.Checked)
                    {
                        custom.SetField("MEDIATYPE", MediaType);
                    }

                    // Explicit tag
                    if (explicitCheckbox.Checked)
                    {
                        if (Advisory == true) { custom.SetField("ITUNESADVISORY", "1"); } else { custom.SetField("ITUNESADVISORY", "0"); }
                    }

                    // Save all selected tags to file
                    tfile.Save();

                    break;
            }
        }

        public void CreateTrackDirectories(string basePath, string qualityPath, string albumPathSuffix = "", bool forTracklist = false)
        {
            if (forTracklist)
            {
                Path1Full = basePath;
                Path2Full = Path1Full;
                Path3Full = Path.Combine(basePath, qualityPath);
                Path4Full = Path3Full;
            }
            else
            {
                Path1Full = Path.Combine(basePath, AlbumArtistPath);
                Path2Full = Path.Combine(basePath, AlbumArtistPath, AlbumNamePath + albumPathSuffix);
                Path3Full = Path.Combine(basePath, AlbumArtistPath, AlbumNamePath + albumPathSuffix, qualityPath);

                // If more than 1 disc, create folders for discs. Otherwise, strings will remain null
                // Pad discnumber with minimum of 2 integer positions based on total number of disks
                if (DiscTotal > 1)
                {
                    // Create strings for disc folders
                    string discFolder = "CD " + DiscNumber.ToString().PadLeft(Math.Max(2, (int)Math.Floor(Math.Log10(DiscTotal) + 1)), '0');
                    Path4Full = Path.Combine(basePath, AlbumArtistPath, AlbumNamePath + albumPathSuffix, qualityPath, discFolder);
                }
                else
                {
                    Path4Full = Path3Full;
                }
            }

            System.IO.Directory.CreateDirectory(Path4Full);
        }

        // Set Track tagging info, tagbased Paths
        public void GetTrackTaggingInfo(Track qobuzTrack)
        {
            ClearTrackTaggingInfo();

            PerformerName = StringTools.DecodeEncodedNonAsciiCharacters(qobuzTrack.Performer?.Name);
            // If no performer name, use album artist
            if (string.IsNullOrEmpty(PerformerName))
            {
                PerformerName = StringTools.DecodeEncodedNonAsciiCharacters(qobuzTrack.Album?.Artist?.Name);
            }
            ComposerName = StringTools.DecodeEncodedNonAsciiCharacters(qobuzTrack.Composer?.Name);
            TrackName = StringTools.DecodeEncodedNonAsciiCharacters(qobuzTrack.Title.Trim());
            TrackVersionName = StringTools.DecodeEncodedNonAsciiCharacters(qobuzTrack.Version?.Trim());
            // Add track version to TrackName
            TrackName += (TrackVersionName == null ? "" : " (" + TrackVersionName + ")");

            Advisory = qobuzTrack.ParentalWarning;
            Copyright = StringTools.DecodeEncodedNonAsciiCharacters(qobuzTrack.Copyright);
            Isrc = qobuzTrack.Isrc;

            // Grab tag ints
            TrackNumber = qobuzTrack.TrackNumber.GetValueOrDefault();
            DiscNumber = qobuzTrack.MediaNumber.GetValueOrDefault();

            // Paths
            PerformerNamePath = StringTools.GetSafeFilename(PerformerName);
            TrackNamePath = StringTools.GetSafeFilename(TrackName);
        }

        // Set Album tagging info, tagbased Paths
        public void GetAlbumTaggingInfo(Album qobuzAlbum)
        {
            ClearAlbumTaggingInfo();

            AlbumArtist = StringTools.DecodeEncodedNonAsciiCharacters(qobuzAlbum.Artist.Name);
            AlbumName = StringTools.DecodeEncodedNonAsciiCharacters(qobuzAlbum.Title.Trim());
            string albumVersionName = StringTools.DecodeEncodedNonAsciiCharacters(qobuzAlbum.Version?.Trim());
            // Add album version to AlbumName if present
            AlbumName += (albumVersionName == null ? "" : " (" + albumVersionName + ")");

            Genre = StringTools.DecodeEncodedNonAsciiCharacters(qobuzAlbum.Genre.Name);
            ReleaseDate = StringTools.FormatDateTimeOffset(qobuzAlbum.ReleaseDateStream);
            Upc = qobuzAlbum.Upc;
            MediaType = qobuzAlbum.ReleaseType;

            // Grab tag ints
            TrackTotal = qobuzAlbum.TracksCount.GetValueOrDefault();
            DiscTotal = qobuzAlbum.MediaCount.GetValueOrDefault();

            // Paths
            AlbumArtistPath = StringTools.GetSafeFilename(AlbumArtist);
            AlbumNamePath = StringTools.GetSafeFilename(AlbumName);
        }

        public void UpdateAlbumTagsUI()
        {
            // Update UI
            albumArtistTextBox.Invoke(new Action(() => albumArtistTextBox.Text = AlbumArtist));
            albumTextBox.Invoke(new Action(() => albumTextBox.Text = AlbumName));
            releaseDateTextBox.Invoke(new Action(() => releaseDateTextBox.Text = ReleaseDate));
            upcTextBox.Invoke(new Action(() => upcTextBox.Text = Upc));
            totalTracksTextbox.Invoke(new Action(() => totalTracksTextbox.Text = TrackTotal.ToString()));
        }

        public void ClearTrackTaggingInfo()
        {
            // Clear tag strings
            PerformerName = null;
            ComposerName = null;
            TrackName = null;
            TrackVersionName = null;
            Advisory = null;
            Copyright = null;
            Isrc = null;

            // Clear tag ints
            TrackNumber = 0;
            DiscNumber = 0;

            // Clear tagbased Paths
            TrackNamePath = null;
        }

        public void ClearAlbumTaggingInfo()
        {
            // Clear tag strings
            AlbumArtist = null;
            AlbumName = null;
            Genre = null;
            ReleaseDate = null;
            Upc = null;
            MediaType = null;

            // Clear tag ints
            TrackTotal = 0;
            DiscTotal = 0;

            // Clear tagbased Paths
            AlbumArtistPath = null;
            AlbumNamePath = null;
        }

        public void GetAlbumCoverArtUrls(Album qobuzAlbum)
        {
            // Grab cover art link
            FrontCoverImgUrl = qobuzAlbum.Image.Large;
            // Get 150x150 artwork for cover art box
            FrontCoverImgBoxUrl = FrontCoverImgUrl.Replace("_600.jpg", "_150.jpg");
            // Get selected artwork size for tagging
            FrontCoverImgTagUrl = FrontCoverImgUrl.Replace("_600.jpg", $"_{ArtSize}.jpg");
            // Get max sized artwork ("_org.jpg" is compressed version of the original "_org.jpg")
            FrontCoverImgUrl = FrontCoverImgUrl.Replace("_600.jpg", "_org.jpg");

            albumArtPicBox.Invoke(new Action(() => albumArtPicBox.ImageLocation = FrontCoverImgBoxUrl));
        }

        public bool IsStreamable(Track qobuzTrack, bool inPlaylist = false)
        {
            bool tryToStream = true;

            if (qobuzTrack.Streamable == false)
            {
                switch (streamableCheckbox.Checked)
                {
                    case true:
                        string trackReference = inPlaylist ? $"{qobuzTrack.Performer?.Name} - {qobuzTrack.Title}" : qobuzTrack.TrackNumber.GetValueOrDefault().ToString();
                        TrackName = StringTools.DecodeEncodedNonAsciiCharacters(qobuzTrack.Title.Trim());
                        AddDownloadLogLine($"Track {trackReference} is not available for streaming. Unable to download.\r\n", tryToStream, tryToStream);
                        tryToStream = false;
                        break;

                    default:
                        AddDownloadLogLine("Track is not available for streaming. But streamable check is being ignored for debugging, or messed up releases. Attempting to download...\r\n", tryToStream, tryToStream);
                        break;
                }
            }

            return tryToStream;
        }

        public void PrepareAlbumDownload(Album qobuzAlbum)
        {
            // Grab sample rate and bit depth for album track is from.
            (string quality, string qualityPathLocal) = QualityStringMappings.GetQualityStrings(Globals.FormatIdString, qobuzAlbum);
            QualityPath = qualityPathLocal;

            // Display album quality in quality textbox.
            qualityTextbox.Invoke(new Action(() => qualityTextbox.Text = quality));

            GetAlbumCoverArtUrls(qobuzAlbum);
            GetAlbumTaggingInfo(qobuzAlbum);
            UpdateAlbumTagsUI();
        }

        private void tagsLabel_Click(object sender, EventArgs e)
        {
            if (this.Height == 533)
            {
                //New Height
                this.Height = 632;
                tagsLabel.Text = "🠉 Choose which tags to save (click me) 🠉";
            }
            else if (this.Height == 632)
            {
                //New Height
                this.Height = 533;
                tagsLabel.Text = "🠋 Choose which tags to save (click me) 🠋";
            }
        }

        private void albumCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.albumTag = albumCheckbox.Checked;
            Settings.Default.Save();
        }

        private void albumArtistCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.albumArtistTag = albumArtistCheckbox.Checked;
            Settings.Default.Save();
        }

        private void trackTitleCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.trackTitleTag = trackTitleCheckbox.Checked;
            Settings.Default.Save();
        }

        private void artistCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.artistTag = artistCheckbox.Checked;
            Settings.Default.Save();
        }

        private void trackNumberCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.trackTag = trackTitleCheckbox.Checked;
            Settings.Default.Save();
        }

        private void trackTotalCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.totalTracksTag = trackTotalCheckbox.Checked;
            Settings.Default.Save();
        }

        private void discNumberCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.discTag = discNumberCheckbox.Checked;
            Settings.Default.Save();
        }

        private void discTotalCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.totalDiscsTag = discTotalCheckbox.Checked;
            Settings.Default.Save();
        }

        private void releaseCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.yearTag = releaseCheckbox.Checked;
            Settings.Default.Save();
        }

        private void genreCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.genreTag = genreCheckbox.Checked;
            Settings.Default.Save();
        }

        private void composerCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.composerTag = composerCheckbox.Checked;
            Settings.Default.Save();
        }

        private void copyrightCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.copyrightTag = copyrightCheckbox.Checked;
            Settings.Default.Save();
        }

        private void isrcCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.isrcTag = isrcCheckbox.Checked;
            Settings.Default.Save();
        }

        private void typeCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.typeTag = typeCheckbox.Checked;
            Settings.Default.Save();
        }

        private void upcCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.upcTag = upcCheckbox.Checked;
            Settings.Default.Save();
        }

        private void explicitCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.explicitTag = explicitCheckbox.Checked;
            Settings.Default.Save();
        }

        private void commentCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.commentTag = commentCheckbox.Checked;
            Settings.Default.Save();
        }

        private void imageCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.imageTag = imageCheckbox.Checked;
            Settings.Default.Save();
        }

        private void commentTextbox_TextChanged(object sender, EventArgs e)
        {
            Settings.Default.commentText = commentTextbox.Text;
            Settings.Default.Save();
        }

        private void artSizeSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Set ArtSize to selected value, and save selected option to settings.
            ArtSize = artSizeSelect.Text;
            Settings.Default.savedArtSize = artSizeSelect.SelectedIndex;
            Settings.Default.Save();
        }

        private void filenameTempSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Set filename template to selected value, and save selected option to settings.
            if (filenameTempSelect.SelectedIndex == 0)
            {
                FileNameTemplateString = " ";
            }
            else if (filenameTempSelect.SelectedIndex == 1)
            {
                FileNameTemplateString = " - ";
            }
            else
            {
                FileNameTemplateString = " ";
            }

            Settings.Default.savedFilenameTemplate = filenameTempSelect.SelectedIndex;
            Settings.Default.savedFilenameTemplateString = FileNameTemplateString;
            Settings.Default.Save();
        }

        private void maxLengthTextbox_TextChanged(object sender, EventArgs e)
        {
            if (maxLengthTextbox.Text != null)
            {
                try
                {
                    if (Convert.ToInt32(maxLengthTextbox.Text) > 110)
                    {
                        maxLengthTextbox.Text = "110";
                    }
                    Settings.Default.savedMaxLength = Convert.ToInt32(maxLengthTextbox.Text);
                    Settings.Default.Save();

                    MaxLength = Convert.ToInt32(maxLengthTextbox.Text);
                }
                catch (Exception)
                {
                    MaxLength = 36;
                }
            }
            else
            {
                MaxLength = 36;
            }
        }

        private void flacHighCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.quality4 = flacHighCheckbox.Checked;
            Settings.Default.Save();

            if (flacHighCheckbox.Checked)
            {
                Globals.FormatIdString = "27";
                customFormatIDTextbox.Text = "27";
                Globals.AudioFileType = ".flac";
                Settings.Default.qualityFormat = Globals.FormatIdString;
                Settings.Default.audioType = Globals.AudioFileType;
                downloadButton.Enabled = true;
                flacMidCheckbox.Checked = false;
                flacLowCheckbox.Checked = false;
                mp3Checkbox.Checked = false;
            }
            else
            {
                if (!flacMidCheckbox.Checked && !flacLowCheckbox.Checked && !mp3Checkbox.Checked)
                {
                    downloadButton.Enabled = false;
                }
            }
        }

        private void flacMidCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.quality3 = flacMidCheckbox.Checked;
            Settings.Default.Save();

            if (flacMidCheckbox.Checked)
            {
                Globals.FormatIdString = "7";
                customFormatIDTextbox.Text = "7";
                Globals.AudioFileType = ".flac";
                Settings.Default.qualityFormat = Globals.FormatIdString;
                Settings.Default.audioType = Globals.AudioFileType;
                downloadButton.Enabled = true;
                flacHighCheckbox.Checked = false;
                flacLowCheckbox.Checked = false;
                mp3Checkbox.Checked = false;
            }
            else
            {
                if (!flacHighCheckbox.Checked && !flacLowCheckbox.Checked && !mp3Checkbox.Checked)
                {
                    downloadButton.Enabled = false;
                }
            }
        }

        private void flacLowCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.quality2 = flacLowCheckbox.Checked;
            Settings.Default.Save();

            if (flacLowCheckbox.Checked)
            {
                Globals.FormatIdString = "6";
                customFormatIDTextbox.Text = "6";
                Globals.AudioFileType = ".flac";
                Settings.Default.qualityFormat = Globals.FormatIdString;
                Settings.Default.audioType = Globals.AudioFileType;
                downloadButton.Enabled = true;
                flacHighCheckbox.Checked = false;
                flacMidCheckbox.Checked = false;
                mp3Checkbox.Checked = false;
            }
            else
            {
                if (!flacHighCheckbox.Checked && !flacMidCheckbox.Checked && !mp3Checkbox.Checked)
                {
                    downloadButton.Enabled = false;
                }
            }
        }

        private void mp3Checkbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.quality1 = mp3Checkbox.Checked;
            Settings.Default.Save();

            if (mp3Checkbox.Checked)
            {
                Globals.FormatIdString = "5";
                customFormatIDTextbox.Text = "5";
                Globals.AudioFileType = ".mp3";
                Settings.Default.qualityFormat = Globals.FormatIdString;
                Settings.Default.audioType = Globals.AudioFileType;
                downloadButton.Enabled = true;
                flacHighCheckbox.Checked = false;
                flacMidCheckbox.Checked = false;
                flacLowCheckbox.Checked = false;
            }
            else
            {
                if (!flacHighCheckbox.Checked && !flacMidCheckbox.Checked && !flacLowCheckbox.Checked)
                {
                    downloadButton.Enabled = false;
                }
            }
        }

        private void customFormatIDTextbox_TextChanged(object sender, EventArgs e)
        {
            if (Globals.FormatIdString != "5" || Globals.FormatIdString != "6" || Globals.FormatIdString != "7" || Globals.FormatIdString != "27")
            {
                Globals.FormatIdString = customFormatIDTextbox.Text;
            }
        }

        private void exitLabel_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void minimizeLabel_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void minimizeLabel_MouseHover(object sender, EventArgs e)
        {
            minimizeLabel.ForeColor = Color.FromArgb(0, 112, 239);
        }

        private void minimizeLabel_MouseLeave(object sender, EventArgs e)
        {
            minimizeLabel.ForeColor = Color.White;
        }

        private void aboutLabel_Click(object sender, EventArgs e)
        {
            Globals.AboutForm.ShowDialog();
        }

        private void aboutLabel_MouseHover(object sender, EventArgs e)
        {
            aboutLabel.ForeColor = Color.FromArgb(0, 112, 239);
        }

        private void aboutLabel_MouseLeave(object sender, EventArgs e)
        {
            aboutLabel.ForeColor = Color.White;
        }

        private void exitLabel_MouseHover(object sender, EventArgs e)
        {
            exitLabel.ForeColor = Color.FromArgb(0, 112, 239);
        }

        private void exitLabel_MouseLeave(object sender, EventArgs e)
        {
            exitLabel.ForeColor = Color.White;
        }

        private void QobuzDownloaderX_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void QobuzDownloaderX_FormClosed(object sender, FormClosedEventArgs e)
        {
            Application.Exit();
        }

        private void logoBox_Click(object sender, EventArgs e)
        {
            DevClickEggThingValue = DevClickEggThingValue + 1;

            if (DevClickEggThingValue >= 3)
            {
                streamableCheckbox.Visible = true;
                enableBtnsButton.Visible = true;
                hideDebugButton.Visible = true;
                displaySecretButton.Visible = true;
                secretTextbox.Visible = true;
                hiddenTextPanel.Visible = true;
                customFormatIDTextbox.Visible = true;
                customFormatPanel.Visible = true;
                formatIDLabel.Visible = true;
            }
            else
            {
                streamableCheckbox.Visible = false;
                displaySecretButton.Visible = false;
                secretTextbox.Visible = false;
                hiddenTextPanel.Visible = false;
                enableBtnsButton.Visible = false;
                hideDebugButton.Visible = false;
                customFormatIDTextbox.Visible = false;
                customFormatPanel.Visible = false;
                formatIDLabel.Visible = false;
            }
        }

        private void hideDebugButton_Click(object sender, EventArgs e)
        {
            streamableCheckbox.Visible = false;
            displaySecretButton.Visible = false;
            secretTextbox.Visible = false;
            hiddenTextPanel.Visible = false;
            enableBtnsButton.Visible = false;
            hideDebugButton.Visible = false;
            customFormatIDTextbox.Visible = false;
            customFormatPanel.Visible = false;
            formatIDLabel.Visible = false;

            DevClickEggThingValue = 0;
        }

        private void displaySecretButton_Click(object sender, EventArgs e)
        {
            secretTextbox.Text = QobuzApiServiceManager.GetApiService().AppSecret;
        }

        private void logoutLabel_MouseHover(object sender, EventArgs e)
        {
            logoutLabel.ForeColor = Color.FromArgb(0, 112, 239);
        }

        private void logoutLabel_MouseLeave(object sender, EventArgs e)
        {
            logoutLabel.ForeColor = Color.FromArgb(88, 92, 102);
        }

        private void logoutLabel_Click(object sender, EventArgs e)
        {
            // Could use some work, but this works.
            Process.Start("QobuzDownloaderX.exe");
            Application.Exit();
        }

        private void enableBtnsButton_Click(object sender, EventArgs e)
        {
            UpdateControlsDownloadDone();
        }
    }
}