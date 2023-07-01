namespace QobuzDownloaderX
{
    partial class CSVForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CSVForm));
            this.logoPictureBox = new System.Windows.Forms.PictureBox();
            this.csvInput = new System.Windows.Forms.TextBox();
            this.browse_button = new System.Windows.Forms.Button();
            this.exitLabel = new System.Windows.Forms.Label();
            this.confirm_button = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.minimizeLabel = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.logoPictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // logoPictureBox
            // 
            this.logoPictureBox.BackColor = System.Drawing.Color.Transparent;
            this.logoPictureBox.Image = global::QobuzDownloaderX.Properties.Resources.qbdlx_white;
            this.logoPictureBox.Location = new System.Drawing.Point(12, 12);
            this.logoPictureBox.Name = "logoPictureBox";
            this.logoPictureBox.Size = new System.Drawing.Size(105, 26);
            this.logoPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.logoPictureBox.TabIndex = 91;
            this.logoPictureBox.TabStop = false;
            // 
            // csvInput
            // 
            this.csvInput.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(20)))), ((int)(((byte)(20)))));
            this.csvInput.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.csvInput.ForeColor = System.Drawing.Color.White;
            this.csvInput.Location = new System.Drawing.Point(12, 44);
            this.csvInput.Multiline = true;
            this.csvInput.Name = "csvInput";
            this.csvInput.Size = new System.Drawing.Size(788, 20);
            this.csvInput.TabIndex = 92;
            this.csvInput.WordWrap = false;
            this.csvInput.TextChanged += new System.EventHandler(this.csvInput_TextChanged);
            // 
            // browse_button
            // 
            this.browse_button.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(112)))), ((int)(((byte)(239)))));
            this.browse_button.FlatAppearance.BorderSize = 0;
            this.browse_button.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.browse_button.ForeColor = System.Drawing.Color.White;
            this.browse_button.Location = new System.Drawing.Point(806, 44);
            this.browse_button.Name = "browse_button";
            this.browse_button.Size = new System.Drawing.Size(120, 23);
            this.browse_button.TabIndex = 111;
            this.browse_button.Text = "Browse";
            this.browse_button.UseVisualStyleBackColor = false;
            this.browse_button.Click += new System.EventHandler(this.browse_button_Click);
            // 
            // exitLabel
            // 
            this.exitLabel.AutoSize = true;
            this.exitLabel.BackColor = System.Drawing.Color.Transparent;
            this.exitLabel.Font = new System.Drawing.Font("Calibri", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.exitLabel.ForeColor = System.Drawing.Color.White;
            this.exitLabel.Location = new System.Drawing.Point(906, 12);
            this.exitLabel.Name = "exitLabel";
            this.exitLabel.Size = new System.Drawing.Size(20, 23);
            this.exitLabel.TabIndex = 112;
            this.exitLabel.Text = "X";
            this.exitLabel.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.exitLabel.Click += new System.EventHandler(this.exitLabel_Click);
            // 
            // confirm_button
            // 
            this.confirm_button.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(112)))), ((int)(((byte)(239)))));
            this.confirm_button.FlatAppearance.BorderSize = 0;
            this.confirm_button.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.confirm_button.ForeColor = System.Drawing.Color.White;
            this.confirm_button.Location = new System.Drawing.Point(806, 73);
            this.confirm_button.Name = "confirm_button";
            this.confirm_button.Size = new System.Drawing.Size(120, 23);
            this.confirm_button.TabIndex = 113;
            this.confirm_button.Text = "Confirm";
            this.confirm_button.UseVisualStyleBackColor = false;
            this.confirm_button.Click += new System.EventHandler(this.confirm_button_ClickAsync);
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(60, 138);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(531, 20);
            this.textBox1.TabIndex = 114;
            this.textBox1.TextChanged += new System.EventHandler(this.textBox1_TextChanged);
            // 
            // textBox2
            // 
            this.textBox2.Location = new System.Drawing.Point(60, 164);
            this.textBox2.Name = "textBox2";
            this.textBox2.Size = new System.Drawing.Size(531, 20);
            this.textBox2.TabIndex = 115;
            this.textBox2.TextChanged += new System.EventHandler(this.textBox2_TextChanged);
            // 
            // minimizeLabel
            // 
            this.minimizeLabel.AutoSize = true;
            this.minimizeLabel.BackColor = System.Drawing.Color.Transparent;
            this.minimizeLabel.Font = new System.Drawing.Font("Calibri", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.minimizeLabel.ForeColor = System.Drawing.Color.White;
            this.minimizeLabel.Location = new System.Drawing.Point(881, 12);
            this.minimizeLabel.Name = "minimizeLabel";
            this.minimizeLabel.Size = new System.Drawing.Size(19, 23);
            this.minimizeLabel.TabIndex = 116;
            this.minimizeLabel.Text = "_";
            this.minimizeLabel.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.minimizeLabel.Click += new System.EventHandler(this.minimizeLabel_Click);
            // 
            // CSVForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(20)))), ((int)(((byte)(20)))));
            this.ClientSize = new System.Drawing.Size(938, 533);
            this.Controls.Add(this.minimizeLabel);
            this.Controls.Add(this.textBox2);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.confirm_button);
            this.Controls.Add(this.exitLabel);
            this.Controls.Add(this.browse_button);
            this.Controls.Add(this.csvInput);
            this.Controls.Add(this.logoPictureBox);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "CSVForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "QobuzDLX | CSV";
            ((System.ComponentModel.ISupportInitialize)(this.logoPictureBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox logoPictureBox;
        private System.Windows.Forms.TextBox csvInput;
        private System.Windows.Forms.Button browse_button;
        private System.Windows.Forms.Label exitLabel;
        private System.Windows.Forms.Button confirm_button;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.Label minimizeLabel;
    }
}