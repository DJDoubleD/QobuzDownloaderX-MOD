using QobuzDownloaderX.View;
using QobuzDownloaderX.Shared;
using QobuzApiSharp.Models.Content;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;


namespace QobuzDownloaderX
{
    public partial class CSVForm : HeadlessForm
    {

        string selectedPath = "";
        List<string> query = new List<string>();
        List<string> url = new List<string>();

        public CSVForm()
        {
            InitializeComponent();
        }

        private void CSVForm_Load(object sender, EventArgs e)
        {
            
        }

        private void csvInput_TextChanged(object sender, EventArgs e)
        {
            
        }

        private void browse_button_Click(object sender, EventArgs e)
        {
            Thread t = new Thread((ThreadStart)(() =>
            {
                OpenFileDialog choofdlog = new OpenFileDialog();
                choofdlog.Filter = "All Files (*.*)|*.*";
                choofdlog.FilterIndex = 1;

                if (choofdlog.ShowDialog() == DialogResult.OK)
                {
                    selectedPath = choofdlog.FileName;     
                }
            }));

            // Run your code from a thread that joins the STA Thread
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();

            csvInput.Text = selectedPath;
        }

        private void exitLabel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private async void confirm_button_ClickAsync(object sender, EventArgs e)
        {
            confirm_button.Enabled = false;
            browse_button.Enabled = false;

            using (TextFieldParser csvParser = new TextFieldParser(selectedPath))
            {
                int count = 0;
                csvParser.CommentTokens = new string[] { "#" };
                csvParser.SetDelimiters(new string[] { "," });
                csvParser.HasFieldsEnclosedInQuotes = false;

                // Skip the row with the column names
                csvParser.ReadLine();

                while (!csvParser.EndOfData)
                {
                    // Read current line fields, pointer moves to the next line.
                    string[] fields = csvParser.ReadFields();
                    string currentArtist = fields[0];
                    string currentSong = fields[1];

                    query.Add(currentArtist + " " + currentSong);
                    count++;

                }
                textBox1.Text = "Read " + count +" Tracks";

                bool searchEnd = false;
                int queryCount = 0;

                while (searchEnd == false)
                {

                    SearchResult tracksResult = QobuzApiServiceManager.GetApiService().SearchTracks(query[queryCount], 1, 0, true);

                    ItemSearchResult<Track> track = tracksResult.Tracks;

                    if (track.Total != 0)
                    {
                        Track firstTrack = track.Items[0];

                        string webPlayerUrl = $"{Globals.WEBPLAYER_BASE_URL}/track/{firstTrack.Id}";

                        url.Add(webPlayerUrl);
                    }

                    queryCount++;

                    if (queryCount == query.Count)
                    {
                        searchEnd = true;
                    }
                }

                int downloadCount = 0;
                bool downloadEnd = false;

                while (downloadEnd == false)
                {   
                    Globals.QbdlxForm.downloadUrl.Invoke(new Action(() => Globals.QbdlxForm.downloadUrl.Text = url[downloadCount]));
                    if (Globals.QbdlxForm.IsBusy() == 0)
                    {
                        await Globals.QbdlxForm.StartLinkItemDownloadAsync(url[downloadCount]);
                        downloadCount++;
                        textBox2.Text = "Tracks Downloaded: " + downloadCount.ToString();
                    }
                    else
                    {
                        Globals.QbdlxForm.wait(5000);
                    }

                    if (downloadCount == url.Count)
                    {
                        downloadEnd = true;
                        
                    }
                }
                    
                confirm_button.Enabled = true;
                browse_button.Enabled = true;
                textBox2.Text = textBox2.Text + " !!! FINISHED !!! ";
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void minimizeLabel_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }
    }
}
