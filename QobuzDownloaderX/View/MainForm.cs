using DownloadAssistant.Requests;
using QobuzDownloaderX.Models;
using QobuzDownloaderX.Properties;
using QobuzDownloaderX.Shared;
using QobuzDownloaderX.View;
using Requests;
using Requests.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace QobuzDownloaderX
{
    public partial class QobuzDownloaderX : HeadlessForm
    {
        private readonly DownloadLogger logger;

        public QobuzDownloaderX()
        {
            // this.AutoSize = true;
            InitializeComponent();
            this.Height = 750;
            this.Width = 1086;
            logger = new DownloadLogger(output, "MainForm");
            RequestHandler.MainRequestHandlers[0].StaticDegreeOfParallelism = 2;
            // Remove previous download error log
            logger.RemovePreviousErrorLog();
            _requests.SpeedReporter.Timeout = 1000;
            _requests.SpeedReporter.SpeedChanged += (obj, speed) =>
            {
                double speedInMB = ((double)speed) / (1024 * 1024);
                UpdateDownloadSpeedLabel($"{speedInMB:F2} MB/s");
            };
        }

        private ExtendedContainer<DownloadRequest> _requests = [];
        public string DownloadLogPath { get; set; }

        public int DevClickEggThingValue { get; set; }
        public int DebugMode { get; set; }

        // Button color download inactive
        private readonly Color ReadyButtonBackColor = Color.FromArgb(0, 112, 239); // Windows Blue (Azure Blue)

        // Button color download active
        private readonly Color BuzyButtonBackColor = Color.FromArgb(200, 30, 0); // Red

        private void MainForm_Load(object sender, EventArgs e)
        {
            // Set main form size on launch and bring to center.
            this.CenterToScreen();

            // Grab profile image
            string profilePic = Convert.ToString(Globals.Login.User.Avatar);
            profilePictureBox.ImageLocation = profilePic.Replace(@"\", null).Replace("s=50", "s=20");

            // Welcome the user after successful login.
            logger.ClearUiLogComponent();
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
                output.Invoke(new Action(() => output.AppendText(Globals.Login.User.Subscription.EndDate != null ? ((DateTimeOffset)Globals.Login.User.Subscription.EndDate).ToString("dd-MM-yyyy") : "?")));
                output.Invoke(new Action(() => output.AppendText("\r\n")));
                output.Invoke(new Action(() => output.AppendText("Periodicity - " + Globals.Login.User.Subscription.Periodicity + "\r\n")));
                output.Invoke(new Action(() => output.AppendText("==========================\r\n\r\n")));
            }
            else if (Globals.Login.User?.Credential?.Parameters?.Source == "household" && Globals.Login.User.Credential.Parameters?.HiresStreaming == true)
            {
                output.Invoke(new Action(() => output.AppendText("Active Family sub-account, unknown End Date \r\n")));
                output.Invoke(new Action(() => output.AppendText("Credential Label - " + Globals.Login.User.Credential.Label + "\r\n")));
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

            // Initialize Global Tagging options. Selected ArtSize is automatically set in artSizeSelect change event listener.
            Globals.TaggingOptions = new TaggingOptions()
            {
                WriteAlbumNameTag = Settings.Default.albumTag,
                WriteAlbumArtistTag = Settings.Default.albumArtistTag,
                WriteTrackArtistTag = Settings.Default.artistTag,
                WriteCommentTag = Settings.Default.commentTag,
                CommentTag = Settings.Default.commentText,
                WriteComposerTag = Settings.Default.composerTag,
                WriteProducerTag = Settings.Default.producerTag,
                WriteLabelTag = Settings.Default.labelTag,
                WriteInvolvedPeopleTag = Settings.Default.involvedPeopleTag,
                MergePerformers = Settings.Default.mergePerformers,
                PrimaryListSeparator = Settings.Default.initialListSeparator,
                ListEndSeparator = Settings.Default.listEndSeparator,
                WriteCopyrightTag = Settings.Default.copyrightTag,
                WriteDiskNumberTag = Settings.Default.discTag,
                WriteDiskTotalTag = Settings.Default.totalDiscsTag,
                WriteGenreTag = Settings.Default.genreTag,
                WriteIsrcTag = Settings.Default.isrcTag,
                WriteMediaTypeTag = Settings.Default.typeTag,
                WriteExplicitTag = Settings.Default.explicitTag,
                WriteTrackTitleTag = Settings.Default.trackTitleTag,
                WriteTrackNumberTag = Settings.Default.trackTag,
                WriteTrackTotalTag = Settings.Default.totalTracksTag,
                WriteUpcTag = Settings.Default.upcTag,
                WriteReleaseYearTag = Settings.Default.yearTag,
                WriteReleaseDateTag = Settings.Default.releaseDateTag,
                WriteCoverImageTag = Settings.Default.imageTag,
                WriteUrlTag = Settings.Default.urlTag
            };

            // Set saved settings to correct places.
            folderBrowserDialog.SelectedPath = Settings.Default.savedFolder;
            albumCheckbox.Checked = Settings.Default.albumTag;
            albumArtistCheckbox.Checked = Settings.Default.albumArtistTag;
            artistCheckbox.Checked = Settings.Default.artistTag;
            commentCheckbox.Checked = Settings.Default.commentTag;
            commentTextbox.Text = Settings.Default.commentText;
            composerCheckbox.Checked = Settings.Default.composerTag;
            producerCheckbox.Checked = Settings.Default.producerTag;
            labelCheckbox.Checked = Settings.Default.labelTag;
            involvedPeopleCheckBox.Checked = Settings.Default.involvedPeopleTag;
            mergePerformersCheckBox.Checked = Settings.Default.mergePerformers;
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
            releasYearCheckbox.Checked = Settings.Default.yearTag;
            releaseDateCheckbox.Checked = Settings.Default.releaseDateTag;
            imageCheckbox.Checked = Settings.Default.imageTag;
            urlCheckBox.Checked = Settings.Default.urlTag;
            mp3Checkbox.Checked = Settings.Default.quality1;
            flacLowCheckbox.Checked = Settings.Default.quality2;
            flacMidCheckbox.Checked = Settings.Default.quality3;
            flacHighCheckbox.Checked = Settings.Default.quality4;
            Globals.FormatIdString = Settings.Default.qualityFormat;
            Globals.AudioFileType = Settings.Default.audioType;
            artSizeSelect.SelectedIndex = Settings.Default.savedArtSize;
            filenameTempSelect.SelectedIndex = Settings.Default.savedFilenameTemplate;
            Globals.FileNameTemplateString = Settings.Default.savedFilenameTemplateString;
            Globals.MaxLength = Settings.Default.savedMaxLength;

            customFormatIDTextbox.Text = Globals.FormatIdString;
            maxLengthTextbox.Text = Globals.MaxLength.ToString();
            InitialListSeparatorTextbox.Text = Settings.Default.initialListSeparator;
            ListEndSeparatorTextbox.Text = Settings.Default.listEndSeparator;

            // Check if there's no selected path saved.
            if (string.IsNullOrEmpty(folderBrowserDialog.SelectedPath))
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

        public void UpdateDownloadSpeedLabel(string speed)
        {
            downloadSpeedLabel.Invoke(new Action(() => downloadSpeedLabel.Text = speed));
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
            StartLinkItemDownload(downloadUrl.Text);
            _requests.Remove(_requests.Where(x => x.State is RequestState.Failed or RequestState.Compleated or RequestState.Cancelled)?.ToArray());
        }

        private async void DownloadUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;

                StartLinkItemDownload(downloadUrl.Text);
            }
        }

        public void StartLinkItemDownload(params string[] downloadLinks)
        {
            // Check if there's no selected path.
            if (string.IsNullOrEmpty(Settings.Default.savedFolder))
            {
                // If there is NOT a saved path.
                logger.ClearUiLogComponent();
                output.Invoke(new Action(() => output.AppendText($"No path has been set! Remember to Choose a Folder!{Environment.NewLine}")));
                return;
            }
            if (downloadLinks.All(string.IsNullOrWhiteSpace))
            {
                if (_requests.State == RequestState.Paused && _requests.Length > 0)
                {
                    output.Invoke(new Action(() => output.AppendText("Starting all paused downloads.\r\n")));
                    _requests.Start();
                }
                else if (_requests.State == RequestState.Running)
                    _requests.ToList().ForEach(x => x.Start());
                else
                    output.Invoke(new Action(() => output.AppendText("The download links were empty.\r\n")));
                return;
            }

            List<DownloadItem> downloadItems = new(downloadLinks.Length);
            foreach (string downloadLink in downloadLinks)
            {
                var request = _requests.FirstOrDefault(x => x.StartOptions.DownloadItem.Url == downloadLink);

                switch (request)
                {
                    case null:
                        downloadItems.Add(DownloadUrlParser.ParseDownloadUrl(downloadLink));
                        break;
                    case var r when r.State == RequestState.Paused:
                        r.Start();
                        output.Invoke(new Action(() => output.AppendText($"Restarting download for URL: {downloadLink}\r\n")));
                        break;
                    default:
                        output.Invoke(new Action(() => output.AppendText($"URL: {downloadLink} is already being downloaded.\r\n")));
                        break;
                }
            }

            foreach (var downloadItem in downloadItems)
                if (downloadItem.IsEmpty())
                    output.Invoke(new Action(() => output.AppendText($"URL >{downloadItem.Url}< not understood. Is there a typo?\r\n")));

                else
                    _requests.Add(new DownloadRequest(new()
                    {
                        DownloadItem = downloadItem,
                        UpdateAlbumTagsUi = UpdateAlbumTagsUI,
                        CheckIfStreamable = streamableCheckbox.Checked,
                        Handler = RequestHandler.MainRequestHandlers[0],
                        RequestStarted = (_) => UpdateControlsDownloadStart(),
                        RequestCancelled = (_) => UpdateControlsDownloadEnd(),
                        RequestFailed = (_, _) => UpdateControlsDownloadEnd(),
                        RequestCompleated = (_, _) => UpdateControlsDownloadEnd(),
                        Logger = new DownloadLogger(output, $"Download_Log_Item_{downloadItem.Id}_Time_{DateTime.Now:yyyy-MM-dd_HH.mm.ss.fff}")
                    }));

            // Run the StartDownloadItemTaskAsync method on a background thread & Wait for the task to complete
        }

        public void UpdateControlsDownloadStart()
        {
            mp3Checkbox.Invoke(new Action(() => mp3Checkbox.AutoCheck = false));
            flacLowCheckbox.Invoke(new Action(() => flacLowCheckbox.AutoCheck = false));
            flacMidCheckbox.Invoke(new Action(() => flacMidCheckbox.AutoCheck = false));
            flacHighCheckbox.Invoke(new Action(() => flacHighCheckbox.AutoCheck = false));
            //downloadUrl.Invoke(new Action(() => downloadUrl.Enabled = false));
            selectFolderButton.Invoke(new Action(() => selectFolderButton.Enabled = false));
            //openSearchButton.Invoke(new Action(() => openSearchButton.Enabled = false));

            //downloadButton.Invoke(new Action(() => {
            //    downloadButton.Text = "Stop Download";
            //    downloadButton.BackColor = BuzyButtonBackColor;
            //}));
        }

        public void UpdateControlsDownloadEnd()
        {
            mp3Checkbox.Invoke(new Action(() => mp3Checkbox.AutoCheck = true));
            flacLowCheckbox.Invoke(new Action(() => flacLowCheckbox.AutoCheck = true));
            flacMidCheckbox.Invoke(new Action(() => flacMidCheckbox.AutoCheck = true));
            flacHighCheckbox.Invoke(new Action(() => flacHighCheckbox.AutoCheck = true));

            downloadUrl.Invoke(new Action(() => downloadUrl.Enabled = true));

            selectFolderButton.Invoke(new Action(() => selectFolderButton.Enabled = true));
            openSearchButton.Invoke(new Action(() => openSearchButton.Enabled = true));

            downloadButton.Invoke(new Action(() =>
            {
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
                UpdateControlsDownloadEnd();
            }
            else
            {
                // If selected path doesn't exist, create it. (Will be ignored if it does)
                System.IO.Directory.CreateDirectory(folderBrowserDialog.SelectedPath);

                // Open selected folder
                try
                {
                    ProcessStartInfo startInfo = new()
                    {
                        FileName = folderBrowserDialog.SelectedPath,
                        UseShellExecute = true,
                        WorkingDirectory = folderBrowserDialog.SelectedPath
                    };
                    Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred: {ex.Message}", "ERROR",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OpenLogFolderButton_Click(object sender, EventArgs e)
        {
            // Open log folder. Folder should exist here so no extra check
            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = Globals.LoggingDir,
                    UseShellExecute = true,
                    WorkingDirectory = Globals.LoggingDir
                };
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "ERROR",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Update UI for downloading album
        public void UpdateAlbumTagsUI(DownloadItemInfo downloadInfo)
        {
            //  Display album art
            albumArtPicBox.Invoke(new Action(() => albumArtPicBox.ImageLocation = downloadInfo.FrontCoverImgBoxUrl));

            // Display album quality in Quality textbox.
            qualityTextbox.Invoke(new Action(() => qualityTextbox.Text = downloadInfo.DisplayQuality));

            // Display album info textfields
            albumArtistTextBox.Invoke(new Action(() => albumArtistTextBox.Text = downloadInfo.AlbumArtist));
            albumTextBox.Invoke(new Action(() => albumTextBox.Text = downloadInfo.AlbumName));
            releaseDateTextBox.Invoke(new Action(() => releaseDateTextBox.Text = downloadInfo.ReleaseDate));
            upcTextBox.Invoke(new Action(() => upcTextBox.Text = downloadInfo.Upc));
            totalTracksTextbox.Invoke(new Action(() => totalTracksTextbox.Text = downloadInfo.TrackTotal.ToString()));
        }

        private void tagsLabel_Click(object sender, EventArgs e)
        {

            if (this.Height == 610)
            {
                // New Height
                this.Height = 750;
                tagsLabel.Text = "🠉 Choose which tags to save (click me) 🠉";
            }
            else if (this.Height == 750)
            {
                // New Height
                this.Height = 610;
                tagsLabel.Text = "🠋 Choose which tags to save (click me) 🠋";
            }

            Debug.WriteLine($"After setting Height: {this.Height}");
        }

        private void AlbumCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.albumTag = albumCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteAlbumNameTag = albumCheckbox.Checked;
        }

        private void AlbumArtistCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.albumArtistTag = albumArtistCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteAlbumArtistTag = albumArtistCheckbox.Checked;
        }

        private void TrackTitleCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.trackTitleTag = trackTitleCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteTrackTitleTag = trackTitleCheckbox.Checked;
        }

        private void ArtistCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.artistTag = artistCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteTrackArtistTag = artistCheckbox.Checked;
        }

        private void TrackNumberCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.trackTag = trackNumberCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteTrackNumberTag = trackNumberCheckbox.Checked;
        }

        private void TrackTotalCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.totalTracksTag = trackTotalCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteTrackTotalTag = trackTotalCheckbox.Checked;
        }

        private void DiscNumberCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.discTag = discNumberCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteDiskNumberTag = discNumberCheckbox.Checked;
        }

        private void DiscTotalCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.totalDiscsTag = discTotalCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteDiskTotalTag = discTotalCheckbox.Checked;
        }

        private void ReleaseYearCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.yearTag = releasYearCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteReleaseYearTag = releasYearCheckbox.Checked;
        }

        private void ReleaseDateCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.releaseDateTag = releaseDateCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteReleaseDateTag = releaseDateCheckbox.Checked;
        }

        private void GenreCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.genreTag = genreCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteGenreTag = genreCheckbox.Checked;
        }

        private void ComposerCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.composerTag = composerCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteComposerTag = composerCheckbox.Checked;
        }

        private void CopyrightCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.copyrightTag = copyrightCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteCopyrightTag = copyrightCheckbox.Checked;
        }

        private void IsrcCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.isrcTag = isrcCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteIsrcTag = isrcCheckbox.Checked;
        }

        private void TypeCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.typeTag = typeCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteMediaTypeTag = typeCheckbox.Checked;
        }

        private void UpcCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.upcTag = upcCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteUpcTag = upcCheckbox.Checked;
        }

        private void ExplicitCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.explicitTag = explicitCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteExplicitTag = explicitCheckbox.Checked;
        }

        private void CommentCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.commentTag = commentCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteCommentTag = commentCheckbox.Checked;
        }

        private void ImageCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.imageTag = imageCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteCoverImageTag = imageCheckbox.Checked;
        }

        private void CommentTextbox_TextChanged(object sender, EventArgs e)
        {
            Settings.Default.commentText = commentTextbox.Text;
            Settings.Default.Save();
            Globals.TaggingOptions.CommentTag = commentTextbox.Text;
        }

        private void ArtSizeSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Set ArtSize to selected value, and save selected option to settings.
            Globals.TaggingOptions.ArtSize = artSizeSelect.Text;
            Settings.Default.savedArtSize = artSizeSelect.SelectedIndex;
            Settings.Default.Save();
        }

        private void filenameTempSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Set filename template to selected value, and save selected option to settings.
            if (filenameTempSelect.SelectedIndex == 0)
            {
                Globals.FileNameTemplateString = " ";
            }
            else if (filenameTempSelect.SelectedIndex == 1)
            {
                Globals.FileNameTemplateString = " - ";
            }
            else
            {
                Globals.FileNameTemplateString = " ";
            }

            Settings.Default.savedFilenameTemplate = filenameTempSelect.SelectedIndex;
            Settings.Default.savedFilenameTemplateString = Globals.FileNameTemplateString;
            Settings.Default.Save();
        }

        private void maxLengthTextbox_TextChanged(object sender, EventArgs e)
        {
            if (maxLengthTextbox.Text != null)
            {
                try
                {
                    if (Convert.ToInt32(maxLengthTextbox.Text) > 150)
                    {
                        maxLengthTextbox.Text = "150";
                    }
                    Settings.Default.savedMaxLength = Convert.ToInt32(maxLengthTextbox.Text);
                    Settings.Default.Save();

                    Globals.MaxLength = Convert.ToInt32(maxLengthTextbox.Text);
                }
                catch (Exception)
                {
                    Globals.MaxLength = 100;
                }
            }
            else
            {
                Globals.MaxLength = 100;
            }
        }

        private void ProducerCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.producerTag = producerCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteProducerTag = producerCheckbox.Checked;
        }

        private void LabelCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.labelTag = labelCheckbox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteLabelTag = labelCheckbox.Checked;
        }

        private void InvolvedPeopleCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.involvedPeopleTag = involvedPeopleCheckBox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteInvolvedPeopleTag = involvedPeopleCheckBox.Checked;
        }

        private void MergePerformersCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.mergePerformers = mergePerformersCheckBox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.MergePerformers = mergePerformersCheckBox.Checked;
        }

        private void InitialListSeparatorTextbox_TextChanged(object sender, EventArgs e)
        {
            if (InitialListSeparatorTextbox.Text != null)
            {
                Settings.Default.initialListSeparator = InitialListSeparatorTextbox.Text;
                Settings.Default.Save();
                Globals.TaggingOptions.PrimaryListSeparator = InitialListSeparatorTextbox.Text;
            }
            else
            {
                Settings.Default.initialListSeparator = ", ";
                Settings.Default.Save();
                Globals.TaggingOptions.PrimaryListSeparator = InitialListSeparatorTextbox.Text;
            }
        }

        private void ListEndSeparatorTextbox_TextChanged(object sender, EventArgs e)
        {
            if (ListEndSeparatorTextbox.Text != null)
            {
                Settings.Default.listEndSeparator = ListEndSeparatorTextbox.Text;
                Settings.Default.Save();
                Globals.TaggingOptions.ListEndSeparator = ListEndSeparatorTextbox.Text;
            }
            else
            {
                Settings.Default.listEndSeparator = " & ";
                Settings.Default.Save();
                Globals.TaggingOptions.ListEndSeparator = ListEndSeparatorTextbox.Text;
            }
        }

        private void UrlCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.urlTag = urlCheckBox.Checked;
            Settings.Default.Save();
            Globals.TaggingOptions.WriteUrlTag = urlCheckBox.Checked;
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
            UpdateControlsDownloadEnd();
        }

        private void StreamableCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            // Not implemented
        }

        private void stopButton_Click(object sender, EventArgs e)
        {
            if (downloadUrl.Text == "cancel")
            {
                _requests.Cancel();
                output.AppendText("Canceling all downloads.\r\n");
                _requests = new();
            }
            else if (string.IsNullOrWhiteSpace(downloadUrl.Text))
            {
                _requests.Pause();
                output.AppendText("Pausing all downloads.\r\n");
            }
            else
            {
                var request = _requests.FirstOrDefault(x => x.StartOptions.DownloadItem.Url == downloadUrl.Text);
                if (request != null)
                {
                    request.Pause();
                    output.AppendText($"Pausing download for URL: {downloadUrl.Text}\r\n");
                }
                else
                {
                    output.AppendText($"No active download found to pause URL: {downloadUrl.Text}\r\n");
                }
            }
        }

    }
}