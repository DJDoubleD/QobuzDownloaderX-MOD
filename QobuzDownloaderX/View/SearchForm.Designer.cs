using System.Drawing;
using System.Windows.Forms;

namespace QobuzDownloaderX
{
    partial class SearchForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SearchForm));
            searchInput = new TextBox();
            searchButton = new Button();
            panel1 = new Panel();
            exitLabel = new Label();
            logoPictureBox = new PictureBox();
            containerScrollPanel = new Panel();
            downloadAllButton = new Button();
            searchTypeSelect = new ComboBox();
            ((System.ComponentModel.ISupportInitialize)logoPictureBox).BeginInit();
            SuspendLayout();
            // 
            // searchInput
            // 
            searchInput.BackColor = Color.FromArgb(20, 20, 20);
            searchInput.BorderStyle = BorderStyle.None;
            searchInput.ForeColor = Color.White;
            searchInput.Location = new Point(157, 69);
            searchInput.Margin = new Padding(4, 5, 4, 5);
            searchInput.Multiline = true;
            searchInput.Name = "searchInput";
            searchInput.Size = new Size(909, 31);
            searchInput.TabIndex = 0;
            searchInput.WordWrap = false;
            searchInput.KeyDown += SearchInput_KeyDown;
            // 
            // searchButton
            // 
            searchButton.BackColor = Color.FromArgb(0, 112, 239);
            searchButton.FlatAppearance.BorderSize = 0;
            searchButton.FlatStyle = FlatStyle.Flat;
            searchButton.ForeColor = Color.White;
            searchButton.Location = new Point(1075, 62);
            searchButton.Margin = new Padding(4, 5, 4, 5);
            searchButton.Name = "searchButton";
            searchButton.Size = new Size(160, 35);
            searchButton.TabIndex = 1;
            searchButton.Text = "Search";
            searchButton.UseVisualStyleBackColor = false;
            searchButton.Click += SearchButton_Click;
            // 
            // panel1
            // 
            panel1.BackColor = Color.FromArgb(88, 92, 102);
            panel1.Location = new Point(157, 97);
            panel1.Margin = new Padding(4, 5, 4, 5);
            panel1.Name = "panel1";
            panel1.Size = new Size(907, 2);
            panel1.TabIndex = 87;
            // 
            // exitLabel
            // 
            exitLabel.AutoSize = true;
            exitLabel.BackColor = Color.Transparent;
            exitLabel.Font = new Font("Calibri", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            exitLabel.ForeColor = Color.White;
            exitLabel.Location = new Point(1208, 12);
            exitLabel.Margin = new Padding(4, 0, 4, 0);
            exitLabel.Name = "exitLabel";
            exitLabel.Size = new Size(26, 29);
            exitLabel.TabIndex = 89;
            exitLabel.Text = "X";
            exitLabel.TextAlign = ContentAlignment.TopCenter;
            exitLabel.Click += ExitLabel_Click;
            exitLabel.MouseLeave += ExitLabel_MouseLeave;
            exitLabel.MouseHover += ExitLabel_MouseHover;
            // 
            // logoPictureBox
            // 
            logoPictureBox.BackColor = Color.Transparent;
            logoPictureBox.Image = Properties.Resources.qbdlx_white;
            logoPictureBox.Location = new Point(16, 14);
            logoPictureBox.Margin = new Padding(4, 5, 4, 5);
            logoPictureBox.Name = "logoPictureBox";
            logoPictureBox.Size = new Size(140, 40);
            logoPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            logoPictureBox.TabIndex = 90;
            logoPictureBox.TabStop = false;
            // 
            // containerScrollPanel
            // 
            containerScrollPanel.AutoScroll = true;
            containerScrollPanel.BackColor = Color.FromArgb(33, 33, 33);
            containerScrollPanel.Location = new Point(16, 108);
            containerScrollPanel.Margin = new Padding(4, 5, 4, 5);
            containerScrollPanel.Name = "containerScrollPanel";
            containerScrollPanel.Size = new Size(1219, 694);
            containerScrollPanel.TabIndex = 4;
            // 
            // downloadAllButton
            // 
            downloadAllButton.BackColor = Color.FromArgb(0, 202, 255);
            downloadAllButton.Enabled = false;
            downloadAllButton.FlatAppearance.BorderSize = 0;
            downloadAllButton.FlatStyle = FlatStyle.Flat;
            downloadAllButton.ForeColor = Color.White;
            downloadAllButton.Location = new Point(1075, 19);
            downloadAllButton.Margin = new Padding(4, 5, 4, 5);
            downloadAllButton.Name = "downloadAllButton";
            downloadAllButton.Size = new Size(112, 35);
            downloadAllButton.TabIndex = 91;
            downloadAllButton.Text = "Download All";
            downloadAllButton.UseVisualStyleBackColor = false;
            downloadAllButton.Visible = false;
            downloadAllButton.Click += DownloadAllButton_Click;
            // 
            // searchTypeSelect
            // 
            searchTypeSelect.DropDownStyle = ComboBoxStyle.DropDownList;
            searchTypeSelect.FormattingEnabled = true;
            searchTypeSelect.Items.AddRange(new object[] { "Album", "Track" });
            searchTypeSelect.Location = new Point(16, 66);
            searchTypeSelect.Margin = new Padding(4, 5, 4, 5);
            searchTypeSelect.Name = "searchTypeSelect";
            searchTypeSelect.Size = new Size(132, 28);
            searchTypeSelect.TabIndex = 3;
            // 
            // SearchForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(20, 20, 20);
            ClientSize = new Size(1251, 820);
            Controls.Add(downloadAllButton);
            Controls.Add(exitLabel);
            Controls.Add(searchTypeSelect);
            Controls.Add(containerScrollPanel);
            Controls.Add(logoPictureBox);
            Controls.Add(panel1);
            Controls.Add(searchButton);
            Controls.Add(searchInput);
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.None;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(4, 5, 4, 5);
            Name = "SearchForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "QobuzDLX | Search";
            FormClosed += SearchForm_FormClosed;
            Load += SearchForm_Load;
            MouseMove += SearchForm_MouseMove;
            ((System.ComponentModel.ISupportInitialize)logoPictureBox).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TextBox searchInput;
        private System.Windows.Forms.Button searchButton;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label exitLabel;
        private System.Windows.Forms.PictureBox logoPictureBox;
        private System.Windows.Forms.Panel containerScrollPanel;
        private System.Windows.Forms.ComboBox searchTypeSelect;
        private Button downloadAllButton;
    }
}