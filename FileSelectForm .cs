using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FileCompressor
{
    public partial class FileSelectForm : Form
    {
        public string SelectedFile { get; private set; }
        private List<string> fileList;

        public FileSelectForm(List<string> files)
        {
            fileList = files;
            InitializeComponent();
            LoadFileList();
        }

        private void InitializeComponent()
        {
            this.listBoxFiles = new ListBox();
            this.btnOK = new Button();
            this.btnCancel = new Button();
            this.lblInstruction = new Label();
            this.lblFileCount = new Label();
            this.SuspendLayout();

            // Form
            this.Text = "📄 Select File to Extract";
            this.Size = new Size(500, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 9F);

            // Instruction Label
            this.lblInstruction.Text = "📋 Select a file to extract from the archive:";
            this.lblInstruction.Location = new Point(12, 12);
            this.lblInstruction.Size = new Size(460, 25);
            this.lblInstruction.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.lblInstruction.ForeColor = Color.DarkBlue;

            // File Count Label
            this.lblFileCount.Text = "";
            this.lblFileCount.Location = new Point(12, 40);
            this.lblFileCount.Size = new Size(460, 20);
            this.lblFileCount.Font = new Font("Segoe UI", 8.5F);
            this.lblFileCount.ForeColor = Color.Gray;

            // Files ListBox
            this.listBoxFiles.Location = new Point(12, 65);
            this.listBoxFiles.Size = new Size(460, 250);
            this.listBoxFiles.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.listBoxFiles.SelectionMode = SelectionMode.One;
            this.listBoxFiles.Font = new Font("Consolas", 9F);
            this.listBoxFiles.DoubleClick += ListBoxFiles_DoubleClick;
            this.listBoxFiles.SelectedIndexChanged += ListBoxFiles_SelectedIndexChanged;

            // OK Button
            this.btnOK.Text = "✅ Extract";
            this.btnOK.Location = new Point(316, 330);
            this.btnOK.Size = new Size(80, 30);
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.btnOK.BackColor = Color.LightGreen;
            this.btnOK.DialogResult = DialogResult.OK;
            this.btnOK.Click += BtnOK_Click;

            // Cancel Button
            this.btnCancel.Text = "❌ Cancel";
            this.btnCancel.Location = new Point(402, 330);
            this.btnCancel.Size = new Size(70, 30);
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Font = new Font("Segoe UI", 9F);
            this.btnCancel.BackColor = Color.LightCoral;
            this.btnCancel.DialogResult = DialogResult.Cancel;

            // Add controls to form
            this.Controls.Add(this.lblInstruction);
            this.Controls.Add(this.lblFileCount);
            this.Controls.Add(this.listBoxFiles);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);

            this.AcceptButton = this.btnOK;
            this.CancelButton = this.btnCancel;

            this.ResumeLayout(false);
        }

        private void LoadFileList()
        {
            listBoxFiles.Items.Clear();
            foreach (string file in fileList)
            {
                listBoxFiles.Items.Add(file);
            }

            lblFileCount.Text = $"📊 Found {fileList.Count} files in archive";

            if (listBoxFiles.Items.Count > 0)
            {
                listBoxFiles.SelectedIndex = 0;
            }

            UpdateButtonState();
        }

        private void ListBoxFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtonState();
        }

        private void UpdateButtonState()
        {
            btnOK.Enabled = listBoxFiles.SelectedItem != null;
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (listBoxFiles.SelectedItem != null)
            {
                SelectedFile = listBoxFiles.SelectedItem.ToString();
            }
            else
            {
                MessageBox.Show("Please select a file to extract.", "No File Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        private void ListBoxFiles_DoubleClick(object sender, EventArgs e)
        {
            if (listBoxFiles.SelectedItem != null)
            {
                SelectedFile = listBoxFiles.SelectedItem.ToString();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private ListBox listBoxFiles;
        private Button btnOK;
        private Button btnCancel;
        private Label lblInstruction;
        private Label lblFileCount;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose components if needed
            }
            base.Dispose(disposing);
        }
    }
}