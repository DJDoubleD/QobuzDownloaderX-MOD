using QobuzApiSharp.Exceptions;
using QobuzApiSharp.Models.Content;
using QobuzDownloaderX.Models.UI;
using QobuzDownloaderX.Shared;
using QobuzDownloaderX.View;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QobuzDownloaderX
{
    public partial class SearchForm : HeadlessForm
    {
        private readonly string errorLog = Path.Combine(Globals.LoggingDir, "Search_Errors.log");
        TableLayoutPanel resultsTableLayoutPanel;

        public SearchForm()
        {
            InitializeComponent();

            // Remove previous search error log
            if (System.IO.File.Exists(errorLog))
            {
                System.IO.File.Delete(errorLog);
            }

            ControlTools.SetDoubleBuffered(containerScrollPanel);

            searchTypeSelect.SelectedItem = "Album";
        }

        private void DownloadAllButton_Click(object sender, EventArgs e)
        {
            try
            {
                downloadAllButton.Enabled = false;
                List<string> downloadLinks = [];
                for (int row = 0; row < resultsTableLayoutPanel.RowCount; row++)
                {
                    if (resultsTableLayoutPanel.GetControlFromPosition(3, row) is Button downloadButton)
                    {
                        // Get the download link from the button's Tag property
                        string downloadLink = downloadButton.Tag?.ToString();
                        if (!string.IsNullOrEmpty(downloadLink))
                            downloadLinks.Add(downloadLink);
                    }
                }
                Globals.QbdlxForm.downloadUrl.Invoke(new Action(() => Globals.QbdlxForm.downloadUrl.Text = downloadLinks.FirstOrDefault()));
                this.Close();
                Globals.QbdlxForm.StartLinkItemDownload([.. downloadLinks]);
            }
            catch (Exception ex)
            {
                List<string> errorLines = ["Error starting download for all items", ex.ToString(), ""];
                System.IO.File.AppendAllLines(errorLog, errorLines);
                MessageBox.Show("Error starting download for all items\nlog saved to " + errorLog, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                downloadAllButton.Enabled = true;
            }
        }

        private void ResetResultsTableLayoutPanel()
        {
            if (resultsTableLayoutPanel != null)
            {
                containerScrollPanel.Controls.Clear();
                ControlTools.RemoveControls(resultsTableLayoutPanel);
                resultsTableLayoutPanel.Dispose();
            }

            resultsTableLayoutPanel = new TableLayoutPanel
            {
                Name = "resultsTableLayoutPanel",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.FromArgb(33, 33, 33),
                Location = new Point(0, 0),
                Size = new Size(894, 70),
                TabIndex = 4
            };
            ControlTools.SetDoubleBuffered(resultsTableLayoutPanel);
            resultsTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
            resultsTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 613F));
            resultsTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            resultsTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 101F));
            resultsTableLayoutPanel.CellPaint += ResultsTableLayoutPanel_CellPaint;

            containerScrollPanel.Controls.Add(resultsTableLayoutPanel);
            downloadAllButton.Visible = false;
            downloadAllButton.Enabled = false;
        }

        private void ShowAndLogSearchResultError(Exception ex)
        {
            ResetResultsTableLayoutPanel();

            resultsTableLayoutPanel.RowCount++;
            resultsTableLayoutPanel.ColumnStyles.Clear();
            resultsTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 914F));
            resultsTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            TextBox ErrorMesage = CreateTextBox($"{ex.Message}", true, GetResultRowColor(0), Color.OrangeRed, FontManager.CreateFont("Hanken Grotesk Medium", 10, FontStyle.Bold | FontStyle.Italic), BorderStyle.None);
            ResizeControlForText(ErrorMesage, 5);
            resultsTableLayoutPanel.Controls.Add(ErrorMesage, 0, 0);

            TextBox ErrorSavedMesage = CreateTextBox($"Error log saved to {errorLog}.", true, GetResultRowColor(0), Color.OrangeRed, FontManager.CreateFont("Hanken Grotesk Medium", 10, FontStyle.Bold | FontStyle.Italic), BorderStyle.None);
            ResizeControlForText(ErrorSavedMesage, 5);
            resultsTableLayoutPanel.Controls.Add(ErrorSavedMesage, 0, 1);

            List<string> errorLines = new List<string> { ex.Message };

            switch (ex)
            {
                case ApiErrorResponseException erEx:
                    errorLines.Add($"Failed API request: {erEx.RequestContent}");
                    errorLines.Add($"Api response code: {erEx.ResponseStatusCode}");
                    errorLines.Add($"Api response status: {erEx.ResponseStatus}");
                    errorLines.Add($"Api response reason: {erEx.ResponseReason}");
                    errorLines.Add("");
                    break;
                case ApiResponseParseErrorException pEx:
                    errorLines.Add($"Api response content: {pEx.ResponseContent}");
                    errorLines.Add("");
                    break;
            }

            errorLines.Add(ex.ToString());

            // Write detailed info to log
            System.IO.File.AppendAllLines(errorLog, errorLines);
        }

        private void ExitLabel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void ExitLabel_MouseHover(object sender, EventArgs e)
        {
            exitLabel.ForeColor = Color.FromArgb(0, 112, 239);
        }

        private void ExitLabel_MouseLeave(object sender, EventArgs e)
        {
            exitLabel.ForeColor = Color.White;
        }

        private void Fill_AlbumResultsTablePanel(SearchResult searchResult)
        {
            FillResultsTablePanel(searchResult?.Albums?.Items, album => new SearchResultRow
            {
                ThumbnailUrl = album.Image.Thumbnail,
                Artist = album.Artist.Name,
                Title = StringTools.DecodeEncodedNonAsciiCharacters(album.Version != null ? $"{album.Title.TrimEnd()} ({album.Version})" : album.Title.TrimEnd()),
                Explicit = album.ParentalWarning.GetValueOrDefault(),
                FormattedDuration = StringTools.FormatDurationInSeconds(album.Duration.GetValueOrDefault()),
                FormattedQuality = $"{album.MaximumBitDepth}-Bit / {album.MaximumSamplingRate} kHz",
                WebPlayerUrl = $"{Globals.WEBPLAYER_BASE_URL}/album/{album.Id}",
                StoreUrl = album.Url,
                ReleaseDate = album.ReleaseDateOriginal.GetValueOrDefault().ToString("yyyy-MM-dd"),
                HiRes = album.Hires.GetValueOrDefault(),
                TrackCount = album.TracksCount.GetValueOrDefault()
            });
            // Show and enable the "Download All" button if there are rows
            if (resultsTableLayoutPanel.RowCount > 0)
            {
                downloadAllButton.Visible = true;
                downloadAllButton.Enabled = true;
            }
        }

        private void Fill_TrackResultsTablePanel(SearchResult searchResult)
        {
            FillResultsTablePanel(searchResult?.Tracks?.Items, track => new SearchResultRow
            {
                ThumbnailUrl = track.Album.Image.Thumbnail,
                Artist = track.Performer.Name,
                Title = StringTools.DecodeEncodedNonAsciiCharacters(track.Version != null ? $"{track.Title.TrimEnd()} ({track.Version})" : track.Title.TrimEnd()),
                Explicit = track.ParentalWarning.GetValueOrDefault(),
                FormattedDuration = StringTools.FormatDurationInSeconds(track.Duration.GetValueOrDefault()),
                FormattedQuality = $"{track.MaximumBitDepth}-Bit / {track.MaximumSamplingRate} kHz",
                WebPlayerUrl = $"{Globals.WEBPLAYER_BASE_URL}/track/{track.Id}",
                StoreUrl = track.Album.Url,
                ReleaseDate = track.Album.ReleaseDateOriginal.GetValueOrDefault().ToString("yyyy-MM-dd"),
                HiRes = track.Hires.GetValueOrDefault()
            });
            // Show and enable the "Download All" button if there are rows
            if (resultsTableLayoutPanel.RowCount > 0)
            {
                downloadAllButton.Visible = true;
                downloadAllButton.Enabled = true;
            }
        }

        private void FillResultsTablePanel<T>(IEnumerable<T> items, Func<T, SearchResultRow> createSearchResultRow)
        {
            if (items != null)
            {
                foreach (var item in items)
                {
                    CreateResultRow(createSearchResultRow(item));
                }
            }
        }

        public void CreateResultRow(SearchResultRow result)
        {
            resultsTableLayoutPanel.RowCount++;
            resultsTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            int currentRow = resultsTableLayoutPanel.RowCount - 1;

            Color rowColor = GetResultRowColor(currentRow);

            PictureBox thumbnail = CreateThumbnail(result.ThumbnailUrl, rowColor);
            resultsTableLayoutPanel.Controls.Add(thumbnail, 0, currentRow);

            TableLayoutPanel secondColumnPanel = CreateReleaseInfoColumn(result, rowColor);
            resultsTableLayoutPanel.Controls.Add(secondColumnPanel, 1, currentRow);

            TableLayoutPanel thirdColumnPanel = CreateQualityColumn(result, rowColor);
            resultsTableLayoutPanel.Controls.Add(thirdColumnPanel, 2, currentRow);

            Button selectButton = CreateDownloadButton(result.WebPlayerUrl);
            resultsTableLayoutPanel.Controls.Add(selectButton, 3, currentRow);
        }

        private Color GetResultRowColor(int currentRow)
        {
            return currentRow % 2 == 1 ? Color.FromArgb(33, 33, 33) : Color.FromArgb(45, 45, 45);
        }

        private PictureBox CreateThumbnail(string thumbnailUrl, Color rowColor)
        {
            return new PictureBox
            {
                ImageLocation = thumbnailUrl,
                SizeMode = PictureBoxSizeMode.CenterImage,
                Size = new Size(50, 50),
                Anchor = AnchorStyles.None,
                Dock = DockStyle.Fill,
                BackColor = rowColor
            };
        }

        private TableLayoutPanel CreateReleaseInfoColumn(SearchResultRow result, Color rowColor)
        {
            // Create fonts
            Font detailsFont = FontManager.CreateFont("Hanken Grotesk Medium", 10);
            Font titleFont = FontManager.CreateFont("Hanken Grotesk ExtraBold", 13, FontStyle.Bold);
            Font artistFont = FontManager.CreateFont("Hanken Grotesk", 11, FontStyle.Bold | FontStyle.Italic);

            // Create TextBox controls
            TextBox titleTextBox = CreateTextBox(result.Title, true, rowColor, Color.White, titleFont, BorderStyle.None);
            TextBox durationTextBox = CreateTextBox(result.FormattedDuration, true, rowColor, Color.WhiteSmoke, detailsFont, BorderStyle.None, HorizontalAlignment.Right);
            TextBox artistTextBox = CreateTextBox(result.Artist, true, rowColor, Color.WhiteSmoke, artistFont, BorderStyle.None);
            TextBox releaseDateTextBox = CreateTextBox(result.ReleaseDate, true, rowColor, Color.WhiteSmoke, detailsFont, BorderStyle.None, HorizontalAlignment.Right);
            TextBox tracks = CreateTracksTextBox(result.TrackCount, rowColor, detailsFont);

            // Resize TextBox controls fitting text in given font (estimation)
            ResizeControlForText(titleTextBox, 0, 500);
            ResizeControlForText(artistTextBox, 5);

            // Create titlePanel
            FlowLayoutPanel titlePanel = CreateTitlePanel(titleTextBox, result.Explicit);

            // Create storeLink and webLink
            LinkLabel storeLink = CreateLinkLabel("Preview in Store", result.StoreUrl, Color.FromArgb(0, 112, 239), Color.Blue, rowColor);
            LinkLabel webLink = CreateLinkLabel("Preview in Web Player", result.WebPlayerUrl, Color.FromArgb(0, 112, 239), Color.Blue, rowColor);

            // Add storeLink and webLink functionality
            AddLinkLabelFunctionality(storeLink);
            AddLinkLabelFunctionality(webLink);

            // Create releaseInfoColumnPanel Control and add child Controls
            return CreateReleaseInfoPanel(rowColor, titlePanel, durationTextBox, artistTextBox, releaseDateTextBox, tracks, webLink, storeLink);
        }

        private TextBox CreateTracksTextBox(int trackCount, Color rowColor, Font detailsFont)
        {
            if (trackCount > 0)
            {
                string trackText = trackCount != 1 ? $"{trackCount} Tracks" : $"{trackCount} Track";
                return CreateTextBox(trackText, true, rowColor, Color.WhiteSmoke, detailsFont, BorderStyle.None, HorizontalAlignment.Right);
            }
            return null;
        }

        private FlowLayoutPanel CreateTitlePanel(TextBox titleTextBox, bool isExplicit)
        {
            FlowLayoutPanel titlePanel = new FlowLayoutPanel
            {
                AutoSize = true,
                Margin = new Padding(0),
                Controls = { titleTextBox },
                //BorderStyle = BorderStyle.FixedSingle
            };
            ControlTools.SetDoubleBuffered(titlePanel);

            if (isExplicit)
            {
                System.Windows.Forms.Label explicitLabel = CreateLabel("E", Color.FromArgb(75, 75, 75), Color.OrangeRed, FontManager.CreateFont("Hanken Grotesk ExtraBold", 8, FontStyle.Bold), BorderStyle.None, new Padding(5, 0, 0, 0), AnchorStyles.None);

                // Add tooltip for "Explicit"
                ToolTip toolTip = new ToolTip();
                toolTip.SetToolTip(explicitLabel, "Explicit");

                // Resize titleTextBox to make room for "Explicit"
                int finalExplicitLabelWidth = explicitLabel.GetPreferredSize(new Size(0, 0)).Width + explicitLabel.Margin.Left + explicitLabel.Margin.Right;
                titleTextBox.Width = 500 - finalExplicitLabelWidth;
                titlePanel.Controls.Add(explicitLabel);
            }

            return titlePanel;
        }

        private FlowLayoutPanel CreateLinksPanel(Color rowColor, LinkLabel webLink, LinkLabel storeLink)
        {
            FlowLayoutPanel linksPanel = new FlowLayoutPanel
            {
                BackColor = rowColor,
                AutoSize = true,
                Margin = new Padding(0),
            };
            ControlTools.SetDoubleBuffered(linksPanel);

            if (webLink != null) linksPanel.Controls.Add(webLink);
            if (storeLink != null) linksPanel.Controls.Add(storeLink);

            return linksPanel;
        }

        private TableLayoutPanel CreateReleaseInfoPanel(Color rowColor, FlowLayoutPanel titlePanel, TextBox durationTextBox, TextBox artistTextBox, TextBox releaseDateTextBox, TextBox tracks, LinkLabel webLink, LinkLabel storeLink)
        {
            TableLayoutPanel releaseInfoColumnPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = rowColor,
                ColumnStyles = { new ColumnStyle(SizeType.Absolute, 500), new ColumnStyle(SizeType.Absolute, 80) }
            };
            ControlTools.SetDoubleBuffered(releaseInfoColumnPanel);

            // Add controls to releaseInfoColumnPanel
            releaseInfoColumnPanel.Controls.Add(titlePanel, 0, 0);
            releaseInfoColumnPanel.Controls.Add(durationTextBox, 1, 0);
            releaseInfoColumnPanel.Controls.Add(artistTextBox, 0, 1);
            releaseInfoColumnPanel.Controls.Add(releaseDateTextBox, 1, 1);

            // Add tracks TextBox if it exists
            if (tracks != null)
            {
                releaseInfoColumnPanel.Controls.Add(tracks, 1, 2);
            }

            // Create linksPanel when at least 1 link exists
            if (webLink != null || storeLink != null)
            {
                FlowLayoutPanel linksPanel = CreateLinksPanel(rowColor, webLink, storeLink);
                releaseInfoColumnPanel.Controls.Add(linksPanel, 0, 2);
            }

            return releaseInfoColumnPanel;
        }

        private TableLayoutPanel CreateQualityColumn(SearchResultRow result, Color rowColor)
        {
            TextBox quality = CreateTextBox(result.FormattedQuality, true, rowColor, Color.White, FontManager.CreateFont("Hanken Grotesk Medium", 10), BorderStyle.None);
            PictureBox hiResIcon = null;

            if (result.HiRes)
            {
                hiResIcon = new PictureBox
                {
                    Image = Properties.Resources.smr_audio,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Size = new Size(30, 30),
                    Margin = new Padding(0, 0, 0, 0),
                    Anchor = AnchorStyles.Top
                };
            }

            TableLayoutPanel qualityColumnPanel = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = hiResIcon == null ? 1 : 2,
                BackColor = rowColor,
                ColumnStyles = { new ColumnStyle(SizeType.Percent, 100) },
                RowStyles = { new RowStyle(SizeType.AutoSize), new RowStyle(SizeType.AutoSize) }
            };
            ControlTools.SetDoubleBuffered(qualityColumnPanel);

            qualityColumnPanel.Controls.Add(quality, 0, 0);
            if (hiResIcon != null) qualityColumnPanel.Controls.Add(hiResIcon, 0, 1);

            return qualityColumnPanel;
        }

        private Button CreateDownloadButton(string webPlayerUrl)
        {
            Button downloadButton = new Button
            {
                Text = "Download",
                BackColor = Color.FromArgb(0, 112, 239),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Height = 23,
                Anchor = AnchorStyles.None,
                Tag = webPlayerUrl,
                TextAlign = ContentAlignment.MiddleCenter
            };
            downloadButton.FlatAppearance.BorderSize = 0;
            downloadButton.Click += (sender, e) =>
            {
                _ = HandleDownloadAsync(webPlayerUrl);
            };

            return downloadButton;
        }

        private async Task HandleDownloadAsync(string webPlayerUrl)
        {
            try
            {
                // Copy selected download link to the main form link field for convenience
                Globals.QbdlxForm.downloadUrl.Invoke(new Action(() => Globals.QbdlxForm.downloadUrl.Text = webPlayerUrl));
                this.Close();
                // Start download from the main form
                Globals.QbdlxForm.StartLinkItemDownload(webPlayerUrl);
            }
            catch (Exception ex)
            {
                // Handle the exception (e.g., log it, show an error message, etc.)
                List<string> errorLines = new List<string>
                {
                    $"Error starting download for {webPlayerUrl}",
                    ex.ToString(),
                    ""
                };

                // Write detailed info to log
                System.IO.File.AppendAllLines(errorLog, errorLines);

                // Show an error message to the user
                MessageBox.Show($"Error starting download for {webPlayerUrl}\nlog saved to {errorLog}.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private TextBox CreateTextBox(string text, bool readOnly, Color backColor, Color foreColor, Font font, BorderStyle borderStyle, HorizontalAlignment textAlign = HorizontalAlignment.Left)
        {
            return new TextBox
            {
                Text = text,
                ReadOnly = readOnly,
                BackColor = backColor,
                ForeColor = foreColor,
                Font = font,
                BorderStyle = borderStyle,
                Margin = new Padding(0),
                AutoSize = false,
                TextAlign = textAlign
            };
        }

        private System.Windows.Forms.Label CreateLabel(string text, Color backColor, Color foreColor, Font font, BorderStyle borderStyle, Padding margin = default, AnchorStyles anchor = AnchorStyles.None, ContentAlignment textAlign = ContentAlignment.TopLeft)
        {
            return new System.Windows.Forms.Label
            {
                Text = text,
                BackColor = backColor,
                ForeColor = foreColor,
                Font = font,
                BorderStyle = borderStyle,
                Margin = margin,
                Anchor = anchor,
                TextAlign = textAlign,
                AutoSize = true
            };
        }

        private LinkLabel CreateLinkLabel(string text, string url, Color linkColor, Color activeLinkColor, Color backColor)
        {
            if (string.IsNullOrEmpty(url)) return null;

            return new LinkLabel
            {
                Text = text,
                Tag = url,
                Font = FontManager.CreateFont("Hanken Grotesk Medium", 9),
                AutoSize = true,
                LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = linkColor,
                ActiveLinkColor = activeLinkColor,
                BackColor = backColor
            };
        }

        private void AddLinkLabelFunctionality(LinkLabel linkLabel)
        {
            if (linkLabel == null || linkLabel.Tag == null) return;

            // Get URL from Tag attribute
            string url = linkLabel.Tag.ToString();

            // Open the URL in the default browser when clicked
            linkLabel.LinkClicked += (sender, e) => Process.Start(url);

            // Add tooltip with the URL
            ToolTip toolTip = new ToolTip();
            toolTip.SetToolTip(linkLabel, url);

            // Add context menu with "Copy URL" option
            ContextMenuStrip contextMenu = new ContextMenuStrip();
            ToolStripMenuItem copyUrlItem = new ToolStripMenuItem("Copy URL");
            copyUrlItem.Click += (sender, e) => CopyToClipboard(url);
            contextMenu.Items.Add(copyUrlItem);
            linkLabel.ContextMenuStrip = contextMenu;
        }

        private void ResizeControlForText(Control control, int extraMargin = 0, int fixedWidth = 0)
        {
            Size preferredSize = TextRenderer.MeasureText(control.Text, control.Font);
            int newWidth = (fixedWidth == 0) ? preferredSize.Width + extraMargin : fixedWidth;
            control.Size = new Size(newWidth, preferredSize.Height);
        }

        private void SearchButton_Click(object sender, EventArgs e)
        {
            searchButton.Enabled = false;
            containerScrollPanel.Hide();

            // In case of an exception, a newline is added to the searchQuery string for some reason.
            // So we clone the object to prevent newline in TextBox on error
            string searchQuery = searchInput.Text.Clone().ToString();

            // Clear previous results before fetching new results
            ResetResultsTableLayoutPanel();

            if (string.IsNullOrEmpty(searchQuery))
            {
                containerScrollPanel.Show();
                searchButton.Enabled = true;
                return;
            }

            try
            {
                if (searchTypeSelect.Text == "Album")
                {
                    SearchResult albumsResult = QobuzApiServiceManager.GetApiService().SearchAlbums(searchQuery, 15, 0, true);
                    Fill_AlbumResultsTablePanel(albumsResult);
                }
                else if (searchTypeSelect.Text == "Track")
                {
                    SearchResult tracksResult = QobuzApiServiceManager.GetApiService().SearchTracks(searchQuery, 15, 0, true);
                    Fill_TrackResultsTablePanel(tracksResult);
                }
            }
            catch (Exception ex)
            {
                ShowAndLogSearchResultError(ex);
            }

            containerScrollPanel.Show();
            searchButton.Enabled = true;
        }

        private void SearchForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            // Nothing yet
        }

        private void SearchForm_Load(object sender, EventArgs e)
        {
            // Nothing yet
        }

        // Enable moving Form with mouse in absence of titlebar
        private void SearchForm_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void SearchInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SearchButton_Click(sender, e);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        // Alternating row colors for search results
        private void ResultsTableLayoutPanel_CellPaint(object sender, TableLayoutCellPaintEventArgs e)
        {
            e.Graphics.FillRectangle(new SolidBrush(GetResultRowColor(e.Row)), e.CellBounds);
        }

        private void CopyToClipboard(string text)
        {
            Task.Run(() =>
            {
                Thread thread = new Thread(() => Clipboard.SetText(text));
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
            });
        }
    }
}