namespace NetworkScannerApp
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnScan = new Button();
            dataGridViewDevices = new DataGridView();
            ((System.ComponentModel.ISupportInitialize)dataGridViewDevices).BeginInit();
            SuspendLayout();
            // 
            // btnScan
            // 
            btnScan.Location = new Point(295, 28);
            btnScan.Name = "btnScan";
            btnScan.Size = new Size(218, 58);
            btnScan.TabIndex = 0;
            btnScan.Text = "Start";
            btnScan.UseVisualStyleBackColor = true;
            btnScan.Click += btnScan_Click_1;
            // 
            // dataGridViewDevices
            // 
            dataGridViewDevices.AllowUserToAddRows = false;
            dataGridViewDevices.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewDevices.Location = new Point(12, 108);
            dataGridViewDevices.Name = "dataGridViewDevices";
            dataGridViewDevices.Size = new Size(776, 330);
            dataGridViewDevices.TabIndex = 1;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(800, 450);
            Controls.Add(dataGridViewDevices);
            Controls.Add(btnScan);
            MaximizeBox = false;
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Network Scanner App - Eng Maged Ali";
            ((System.ComponentModel.ISupportInitialize)dataGridViewDevices).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Button btnScan;
        private DataGridView dataGridViewDevices;
    }
}
