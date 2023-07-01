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
            this.exitLabel = new System.Windows.Forms.Label();
            this.searchTypeSelect = new System.Windows.Forms.ComboBox();
            this.logoPictureBox = new System.Windows.Forms.PictureBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.searchButton = new System.Windows.Forms.Button();
            this.searchInput = new System.Windows.Forms.TextBox();
            this.containerScrollPanel = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.logoPictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // exitLabel
            // 
            this.exitLabel.AutoSize = true;
            this.exitLabel.BackColor = System.Drawing.Color.Transparent;
            this.exitLabel.Font = new System.Drawing.Font("Calibri", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.exitLabel.ForeColor = System.Drawing.Color.White;
            this.exitLabel.Location = new System.Drawing.Point(906, 8);
            this.exitLabel.Name = "exitLabel";
            this.exitLabel.Size = new System.Drawing.Size(20, 23);
            this.exitLabel.TabIndex = 89;
            this.exitLabel.Text = "X";
            this.exitLabel.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.exitLabel.Click += new System.EventHandler(this.ExitLabel_Click);
            this.exitLabel.MouseLeave += new System.EventHandler(this.ExitLabel_MouseLeave);
            this.exitLabel.MouseHover += new System.EventHandler(this.ExitLabel_MouseHover);
            // 
            // searchTypeSelect
            // 
            this.searchTypeSelect.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.searchTypeSelect.FormattingEnabled = true;
            this.searchTypeSelect.Items.AddRange(new object[] {
            "Album",
            "Track"});
            this.searchTypeSelect.Location = new System.Drawing.Point(12, 43);
            this.searchTypeSelect.Name = "searchTypeSelect";
            this.searchTypeSelect.Size = new System.Drawing.Size(100, 21);
            this.searchTypeSelect.TabIndex = 3;
            // 
            // logoPictureBox
            // 
            this.logoPictureBox.BackColor = System.Drawing.Color.Transparent;
            this.logoPictureBox.Image = global::QobuzDownloaderX.Properties.Resources.qbdlx_white;
            this.logoPictureBox.Location = new System.Drawing.Point(12, 9);
            this.logoPictureBox.Name = "logoPictureBox";
            this.logoPictureBox.Size = new System.Drawing.Size(105, 26);
            this.logoPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.logoPictureBox.TabIndex = 90;
            this.logoPictureBox.TabStop = false;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(88)))), ((int)(((byte)(92)))), ((int)(((byte)(102)))));
            this.panel1.Location = new System.Drawing.Point(118, 63);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(680, 1);
            this.panel1.TabIndex = 87;
            // 
            // searchButton
            // 
            this.searchButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(112)))), ((int)(((byte)(239)))));
            this.searchButton.FlatAppearance.BorderSize = 0;
            this.searchButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.searchButton.ForeColor = System.Drawing.Color.White;
            this.searchButton.Location = new System.Drawing.Point(806, 40);
            this.searchButton.Name = "searchButton";
            this.searchButton.Size = new System.Drawing.Size(120, 23);
            this.searchButton.TabIndex = 1;
            this.searchButton.Text = "Search";
            this.searchButton.UseVisualStyleBackColor = false;
            this.searchButton.Click += new System.EventHandler(this.SearchButton_Click);
            // 
            // searchInput
            // 
            this.searchInput.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(20)))), ((int)(((byte)(20)))));
            this.searchInput.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.searchInput.ForeColor = System.Drawing.Color.White;
            this.searchInput.Location = new System.Drawing.Point(118, 45);
            this.searchInput.Multiline = true;
            this.searchInput.Name = "searchInput";
            this.searchInput.Size = new System.Drawing.Size(682, 20);
            this.searchInput.TabIndex = 0;
            this.searchInput.WordWrap = false;
            this.searchInput.KeyDown += new System.Windows.Forms.KeyEventHandler(this.SearchInput_KeyDown);
            // 
            // containerScrollPanel
            // 
            this.containerScrollPanel.AutoScroll = true;
            this.containerScrollPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(33)))), ((int)(((byte)(33)))));
            this.containerScrollPanel.Location = new System.Drawing.Point(12, 70);
            this.containerScrollPanel.Name = "containerScrollPanel";
            this.containerScrollPanel.Size = new System.Drawing.Size(914, 451);
            this.containerScrollPanel.TabIndex = 4;
            // 
            // SearchForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(20)))), ((int)(((byte)(20)))));
            this.ClientSize = new System.Drawing.Size(938, 533);
            this.Controls.Add(this.exitLabel);
            this.Controls.Add(this.searchTypeSelect);
            this.Controls.Add(this.containerScrollPanel);
            this.Controls.Add(this.logoPictureBox);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.searchButton);
            this.Controls.Add(this.searchInput);
            this.Cursor = System.Windows.Forms.Cursors.Default;
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "SearchForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "QobuzDLX | Search";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.SearchForm_FormClosed);
            this.Load += new System.EventHandler(this.SearchForm_Load);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.SearchForm_MouseMove);
            ((System.ComponentModel.ISupportInitialize)(this.logoPictureBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox searchInput;
        private System.Windows.Forms.Button searchButton;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label exitLabel;
        private System.Windows.Forms.PictureBox logoPictureBox;
        private System.Windows.Forms.ComboBox searchTypeSelect;
        private System.Windows.Forms.Panel containerScrollPanel;
    }
}