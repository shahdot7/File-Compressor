using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileCompressor
{
    public partial class MainForm : Form
    {
        private HuffmanCompressor compressor;
        private ShannonFanoCompressor shannonCompressor;
        private BackgroundWorker backgroundWorker;
        private bool isOperationRunning = false;
        private List<string> selectedFiles = new List<string>();
        private string initialMode;
        private string initialPath;

        public MainForm(string[] args = null)
        {
            InitializeComponent();
            compressor = new HuffmanCompressor();
            shannonCompressor = new ShannonFanoCompressor();
            InitializeBackgroundWorker();
            UpdateButtonStates();
            // 🟡 إذا في ملفات جاي من الزر اليمين → أضفها تلقائيًا
            if (args != null && args.Length > 0)
            {
                foreach (var path in args)
                {
                    if (File.Exists(path) || Directory.Exists(path))
                    {
                        selectedFiles.Add(path);
                        listBoxFiles.Items.Add($"{Path.GetFileName(path)} ({GetFileSizeString(path)}) - {path}");
                    }
                }
                UpdateFileCount();
                UpdateButtonStates();
                lblStatus.Text = "✅ Files added from context menu.";
            }
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.btnSelectFiles = new System.Windows.Forms.Button();
            this.btnCompress = new System.Windows.Forms.Button();
            this.btnDecompress = new System.Windows.Forms.Button();
            this.btnExtractSingle = new System.Windows.Forms.Button();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.lblStatus = new System.Windows.Forms.Label();
            this.listBoxFiles = new System.Windows.Forms.ListBox();
            this.lblCompressionRatio = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.groupBoxFiles = new System.Windows.Forms.GroupBox();
            this.groupBoxOperations = new System.Windows.Forms.GroupBox();
            this.groupBoxProgress = new System.Windows.Forms.GroupBox();
            this.btnRemoveFile = new System.Windows.Forms.Button();
            this.btnClearAll = new System.Windows.Forms.Button();
            this.lblFileCount = new System.Windows.Forms.Label();

            this.SuspendLayout();

            // Main Form
            this.Text = "File Compression Application - Easy Compress & Extract";
            this.Size = new System.Drawing.Size(900, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(700, 500);
            this.Icon = SystemIcons.Application;

            // Group Box - Files
            this.groupBoxFiles.Text = "📁 Selected Files";
            this.groupBoxFiles.Location = new Point(12, 12);
            this.groupBoxFiles.Size = new Size(860, 220);
            this.groupBoxFiles.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.groupBoxFiles.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            // Select Files Button
            this.btnSelectFiles.Text = "📑 Add Files / Folders ▼";
            this.btnSelectFiles.Location = new Point(20, 25);
            this.btnSelectFiles.Size = new Size(130, 35);
            this.btnRemoveFile.UseVisualStyleBackColor = true;
          
            this.btnSelectFiles.Font = new Font("Segoe UI", 9F);
            this.btnSelectFiles.BackColor = Color.LightSkyBlue;
            this.btnSelectFiles.Click += new EventHandler(this.btnSelectFiles_Click);

            // Remove File Button
            this.btnRemoveFile.Text = "🗑️ Remove";
            this.btnRemoveFile.Location = new Point(150, 25);
            this.btnRemoveFile.Size = new Size(100, 35);
            this.btnRemoveFile.UseVisualStyleBackColor = true;
            this.btnRemoveFile.Font = new Font("Segoe UI", 9F);
            this.btnRemoveFile.BackColor = Color.LightCoral;
            this.btnRemoveFile.Click += new EventHandler(this.btnRemoveFile_Click);

            // Clear All Button
            this.btnClearAll.Text = "🧹 Clear All";
            this.btnClearAll.Location = new Point(260, 25);
            this.btnClearAll.Size = new Size(100, 35);
            this.btnClearAll.UseVisualStyleBackColor = true;
            this.btnClearAll.Font = new Font("Segoe UI", 9F);
            this.btnClearAll.BackColor = Color.LightYellow;
            this.btnClearAll.Click += new EventHandler(this.btnClearAll_Click);

            // File Count Label
            this.lblFileCount.Text = "Files: 0";
            this.lblFileCount.Location = new Point(380, 35);
            this.lblFileCount.Size = new Size(200, 20);
            this.lblFileCount.Font = new Font("Segoe UI", 9F);
            this.lblFileCount.ForeColor = Color.Blue;

            // إضافة زر الإيقاف
            this.btnPause = new Button();
            this.btnPause.Name = "btnPause";
            this.btnPause.Text = "⏸️ Pause";
            this.btnPause.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            this.btnPause.Size = new Size(80, 35);
            this.btnPause.Location = new Point(580, 30); // بجانب زر Cancel
            this.btnPause.BackColor = Color.FromArgb(255, 193, 7); // أصفر
            this.btnPause.ForeColor = Color.Black;
            this.btnPause.FlatStyle = FlatStyle.Flat;
            this.btnPause.FlatAppearance.BorderSize = 0;
            this.btnPause.UseVisualStyleBackColor = false;
            this.btnPause.Cursor = Cursors.Hand;
            this.btnPause.Click += btnPause_Click;
            this.btnPause.Enabled = false; // معطل في البداية
            
            // إضافة زر الاستئناف
            this.btnResume = new Button();
            this.btnResume.Name = "btnResume";
            this.btnResume.Text = "▶️ Resume";
            this.btnResume.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            this.btnResume.Size = new Size(80, 35);
            this.btnResume.Location = new Point(670, 30); // بجانب زر Pause
            this.btnResume.BackColor = Color.FromArgb(40, 167, 69); // أخضر
            this.btnResume.ForeColor = Color.White;
            this.btnResume.FlatStyle = FlatStyle.Flat;
            this.btnResume.FlatAppearance.BorderSize = 0;
            this.btnResume.UseVisualStyleBackColor = false;
            this.btnResume.Cursor = Cursors.Hand;
            this.btnResume.Click += btnResume_Click;
            this.btnResume.Enabled = false; // معطل في البداية
            // Files ListBox
            this.listBoxFiles.Location = new Point(20, 70);
            this.listBoxFiles.Size = new Size(820, 130);
            this.listBoxFiles.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.listBoxFiles.HorizontalScrollbar = true;
            this.listBoxFiles.Font = new Font("Consolas", 8.5F);
            this.listBoxFiles.SelectionMode = SelectionMode.MultiExtended;

            // Group Box - Operations
            this.groupBoxOperations.Text = "⚙️ Operations";
            this.groupBoxOperations.Location = new Point(12, 250);
            this.groupBoxOperations.Size = new Size(860, 90);
            this.groupBoxOperations.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.groupBoxOperations.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            // Compress Button
            this.btnCompress.Text = "🗜️ Compress Files";
            this.btnCompress.Location = new Point(20, 30);
            this.btnCompress.Size = new Size(140, 40);
            this.btnCompress.UseVisualStyleBackColor = true;
            this.btnCompress.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.btnCompress.BackColor = Color.LightGreen;
            this.btnCompress.Click += new EventHandler(this.btnCompress_Click);

            // Decompress Button
            this.btnDecompress.Text = "📦 Extract Archive";
            this.btnDecompress.Location = new Point(180, 30);
            this.btnDecompress.Size = new Size(140, 40);
            this.btnDecompress.UseVisualStyleBackColor = true;
            this.btnDecompress.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.btnDecompress.BackColor = Color.LightSalmon;
            this.btnDecompress.Click += new EventHandler(this.btnDecompress_Click);

            // Extract Single File Button
            this.btnExtractSingle.Text = "📄 Extract Single";
            this.btnExtractSingle.Location = new Point(340, 30);
            this.btnExtractSingle.Size = new Size(140, 40);
            this.btnExtractSingle.UseVisualStyleBackColor = true;
            this.btnExtractSingle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.btnExtractSingle.BackColor = Color.LightCyan;
            this.btnExtractSingle.Click += new EventHandler(this.btnExtractSingle_Click);

            // Cancel Button
            this.btnCancel.Text = "❌ Cancel";
            this.btnCancel.Location = new Point(500, 30);
            this.btnCancel.Size = new Size(100, 40);
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.btnCancel.BackColor = Color.MistyRose;
            this.btnCancel.Enabled = false;
            this.btnCancel.Click += new EventHandler(this.btnCancel_Click);

            // Group Box - Progress
            this.groupBoxProgress.Text = "📊 Progress";
            this.groupBoxProgress.Location = new Point(12, 360);
            this.groupBoxProgress.Size = new Size(860, 140);
            this.groupBoxProgress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.groupBoxProgress.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            // Progress Bar
            this.progressBar.Location = new Point(20, 30);
            this.progressBar.Size = new Size(820, 30);
            this.progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.progressBar.Style = ProgressBarStyle.Continuous;

            // Status Label
            this.lblStatus.Text = "✅ Ready - Select files to compress or choose an archive to extract";
            this.lblStatus.Location = new Point(20, 70);
            this.lblStatus.Size = new Size(820, 25);
            this.lblStatus.AutoSize = false;
            this.lblStatus.Font = new Font("Segoe UI", 9F);
            this.lblStatus.ForeColor = Color.DarkGreen;

            // Compression Ratio Label
            this.lblCompressionRatio.Text = "📈 Compression Ratio: N/A";
            this.lblCompressionRatio.Location = new Point(20, 100);
            this.lblCompressionRatio.Size = new Size(820, 25);
            this.lblCompressionRatio.AutoSize = false;
            this.lblCompressionRatio.Font = new Font("Segoe UI", 9F);
            this.lblCompressionRatio.ForeColor = Color.Blue;
            // Algorithm Selection Dropdown
            var lblAlgorithm = new Label();
            lblAlgorithm.Text = "Algorithm:";
            lblAlgorithm.Location = new Point(620, 60);
            lblAlgorithm.Size = new Size(70, 20);
            lblAlgorithm.Font = new Font("Segoe UI", 9F);
            lblAlgorithm.ForeColor =Color.Crimson;

          var comboAlgorithm = new ComboBox();
            comboAlgorithm.Name = "comboAlgorithm";
            comboAlgorithm.Location = new Point(700, 60);
            comboAlgorithm.Size = new Size(140, 28);
            comboAlgorithm.Font = new Font("Segoe UI", 9F);
            comboAlgorithm.DropDownStyle = ComboBoxStyle.DropDownList;
            comboAlgorithm.Items.AddRange(new string[] { "Huffman", "Shannon-Fano" });
            comboAlgorithm.SelectedIndexChanged += ComboAlgorithm_SelectedIndexChanged;

           

            // Add to Operations group box
            this.groupBoxOperations.Controls.Add(lblAlgorithm);
            this.groupBoxOperations.Controls.Add(comboAlgorithm);

            // Add controls to groups
            this.groupBoxFiles.Controls.Add(this.btnSelectFiles);
            this.groupBoxFiles.Controls.Add(this.btnRemoveFile);
            this.groupBoxFiles.Controls.Add(this.btnClearAll);
            this.groupBoxFiles.Controls.Add(this.lblFileCount);
            this.groupBoxFiles.Controls.Add(this.listBoxFiles);
            this.groupBoxFiles.Controls.Add(this.btnPause);
            this.groupBoxFiles.Controls.Add(this.btnResume);

            this.groupBoxOperations.Controls.Add(this.btnCompress);
            this.groupBoxOperations.Controls.Add(this.btnDecompress);
            this.groupBoxOperations.Controls.Add(this.btnExtractSingle);
            this.groupBoxOperations.Controls.Add(this.btnCancel);

            this.groupBoxProgress.Controls.Add(this.progressBar);
            this.groupBoxProgress.Controls.Add(this.lblStatus);
            this.groupBoxProgress.Controls.Add(this.lblCompressionRatio);
            ////////////////////////////////////////////////////////
            // Compare Button
            this.btnCompare = new System.Windows.Forms.Button();
            this.btnCompare.Text = "📊 Compare Algorithms";
            this.btnCompare.Location = new Point(650, 15);
            this.btnCompare.Size = new Size(170, 40);
            this.btnCompare.UseVisualStyleBackColor = true;
            this.btnCompare.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.btnCompare.BackColor = Color.LightSkyBlue;
            this.btnCompare.Click += new EventHandler(this.btnCompare_Click);
            this.groupBoxOperations.Controls.Add(this.btnCompare);

            // Add groups to form
            this.Controls.Add(this.groupBoxFiles);
            this.Controls.Add(this.groupBoxOperations);
            this.Controls.Add(this.groupBoxProgress);

            btnCompress.Enabled = false;
            btnDecompress.Enabled = false;
            btnExtractSingle.Enabled = false;
            btnRemoveFile.Enabled = false;
            btnClearAll.Enabled = false;



            this.ResumeLayout(false);
        }

        private void InitializeBackgroundWorker()
        {
            backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerReportsProgress = true;
            backgroundWorker.WorkerSupportsCancellation = true;
            backgroundWorker.DoWork += BackgroundWorker_DoWork;
            backgroundWorker.ProgressChanged += BackgroundWorker_ProgressChanged;
            backgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;

        }
        private void ComboAlgorithm_SelectedIndexChanged(object sender, EventArgs e)
        {
            var combo = sender as ComboBox;

            if (combo.SelectedItem != null)
            {
                btnCompress.Enabled = selectedFiles.Count > 0;
                btnDecompress.Enabled = true;
                btnExtractSingle.Enabled = true;
                btnRemoveFile.Enabled = selectedFiles.Count > 0;
                btnClearAll.Enabled = selectedFiles.Count > 0;
            }
            else
            {
                btnCompress.Enabled = false;
                btnDecompress.Enabled = false;
                btnExtractSingle.Enabled = false;
                btnRemoveFile.Enabled = false;
                btnClearAll.Enabled = false;
            }
        }
        private void HandleCommandLineAction()
        {
            if (!File.Exists(initialPath) && !Directory.Exists(initialPath))
            {
                MessageBox.Show("Invalid path from context menu.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (initialMode == "compress")
            {
                selectedFiles.Clear();
                if (File.Exists(initialPath))
                {
                    selectedFiles.Add(initialPath);
                }
                else if (Directory.Exists(initialPath))
                {
                    var files = Directory.GetFiles(initialPath, "*", SearchOption.AllDirectories);
                    selectedFiles.AddRange(files);
                }
                UpdateFileCount();

                string algorithm = "huffman";

                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "Huffman Archive (*.huf)|*.huf";
                    saveFileDialog.DefaultExt = "huf";
                    saveFileDialog.FileName = "compressed_from_context.huf";

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        StartOperation("compress", new
                        {
                            Files = selectedFiles.ToArray(),
                            OutputPath = saveFileDialog.FileName,
                            Algorithm = algorithm,
                            Password = (string)null
                        });
                    }
                }
            }
            else if (initialMode == "decompress")
            {
                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Select folder to extract files to:";
                    if (folderDialog.ShowDialog() == DialogResult.OK)
                    {
                        string algorithm = "huffman"; 
                        StartOperation("decompress", new
                        {
                            ArchivePath = initialPath,
                            OutputPath = folderDialog.SelectedPath,
                            Algorithm = algorithm,
                            Password = (string)null
                        });
                    }
                }
            }
        }
        private string GetSelectedAlgorithm()
{
    var combo = this.groupBoxOperations.Controls.Find("comboAlgorithm", true).FirstOrDefault() as ComboBox;
    return combo?.SelectedItem?.ToString().ToLower(); // "huffman" أو "shannon-fano"
}

        private void btnSelectFiles_Click(object sender, EventArgs e)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("📂 Select Files", null, (s, ea) => SelectFiles());
            menu.Items.Add("📁 Select Folder", null, (s, ea) => SelectFolder());
            menu.Show(btnSelectFiles, new Point(0, btnSelectFiles.Height));
        }
        private void SelectFiles()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Multiselect = true;
                openFileDialog.Filter = "All Files (*.*)|*.*";
                openFileDialog.Title = "Select Files";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    foreach (string file in openFileDialog.FileNames)
                    {
                        if (!selectedFiles.Contains(file))
                        {
                            selectedFiles.Add(file);
                            listBoxFiles.Items.Add($"{Path.GetFileName(file)} ({GetFileSizeString(file)}) - {file}");
                        }
                    }
                    UpdateFileCount();
                    UpdateButtonStates();
                    lblStatus.Text = $"✅ Added {openFileDialog.FileNames.Length} files";
                }
            }
        }
          private void SelectFolder()
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select Folder to Compress";

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    string folderPath = folderDialog.SelectedPath;

                    if (!selectedFiles.Contains(folderPath))
                    {
                        selectedFiles.Add(folderPath);
                        listBoxFiles.Items.Add($"📁 {Path.GetFileName(folderPath)} - {folderPath}");
                    }

                    UpdateFileCount();
                    UpdateButtonStates();
                    lblStatus.Text = $"✅ Added Folder: {Path.GetFileName(folderPath)}";
                }
            }
        }
        private void btnRemoveFile_Click(object sender, EventArgs e)
        {
            if (listBoxFiles.SelectedIndices.Count > 0)
            {
                var indicesToRemove = listBoxFiles.SelectedIndices.Cast<int>().OrderByDescending(i => i).ToList();
                foreach (int index in indicesToRemove)
                {
                    selectedFiles.RemoveAt(index);
                    listBoxFiles.Items.RemoveAt(index);
                }
                UpdateFileCount();
                UpdateButtonStates();
                lblStatus.Text = $"✅ Removed {indicesToRemove.Count} files";
            }
            else
            {
                MessageBox.Show("Please select files to remove.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        private void btnClearAll_Click(object sender, EventArgs e)
        {
            if (selectedFiles.Count > 0)
            {
                selectedFiles.Clear();
                listBoxFiles.Items.Clear();
                UpdateFileCount();
                UpdateButtonStates();
                lblStatus.Text = "✅ All files cleared";
            }
        }
        private void btnCompress_Click(object sender, EventArgs e)
        {
            if (selectedFiles.Count == 0)
            {
                MessageBox.Show("Please select files to compress first.", "No Files Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string algorithm = GetSelectedAlgorithm();
            if (string.IsNullOrEmpty(algorithm))
            {
                MessageBox.Show("Please select a compression algorithm.", "Algorithm Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                string extension = algorithm == "huffman" ? "huf" : "sfn";
                saveFileDialog.Filter = $"{algorithm.ToUpper()} Compressed Files (*.{extension})|*.{extension}";
                saveFileDialog.Title = "Save Compressed Archive";
                saveFileDialog.DefaultExt = extension;
                saveFileDialog.FileName = $"compressed_archive.{extension}";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string password = null;

                    using (var prompt = new PasswordPrompt())
                    {
                        prompt.Text = "Password Protection (Optional)";
                        if (prompt.ShowDialog() == DialogResult.OK)
                        {
                            password = string.IsNullOrWhiteSpace(prompt.Password) ? null : prompt.Password;
                        }
                        else
                        {
                            return; // ألغى العملية
                        }
                    }

                    StartOperation("compress", new
                    {
                        Files = selectedFiles.ToArray(),
                        OutputPath = saveFileDialog.FileName,
                        Algorithm = algorithm,
                        Password = password 
                    });
                }
            }
        }
        private void btnDecompress_Click(object sender, EventArgs e)
        {
            using var openFileDialog = new OpenFileDialog
            {
                Filter = "Compressed Files (*.huf;*.sfn)|*.huf;*.sfn|All Files (*.*)|*.*",
                Title = "Select Compressed Archive"
            };

            if (openFileDialog.ShowDialog() != DialogResult.OK)
                return;

            string archivePath = openFileDialog.FileName;
            string password = null;
            string algorithm = null;

            //  تحديد الخوارزمية أولاً
            try
            {
                algorithm = DetectAlgorithmFromFile(archivePath);
                if (algorithm == null)
                {
                    MessageBox.Show("⚠️ Unknown archive format!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("فشل في قراءة الملف:\n" + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            //  بس للهاف مان، تحقق من التشفير واطلب كلمة السر
            if (algorithm == "huffman")
            {
                bool isEncrypted = CheckIfEncrypted(archivePath);

                if (isEncrypted)
                {
                    using var prompt = new PasswordPrompt();
                    prompt.Text = "This archive is password protected";

                    if (prompt.ShowDialog() == DialogResult.OK)
                    {
                        password = prompt.Password;
                        if (string.IsNullOrWhiteSpace(password))
                        {
                            MessageBox.Show("كلمة السر مطلوبة لهذا الملف.", "كلمة السر مطلوبة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }
            }

            using var folderDialog = new FolderBrowserDialog
            {
                Description = "Select folder to extract files to:",
                ShowNewFolderButton = true
            };

            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                StartOperation("decompress", new
                {
                    ArchivePath = archivePath,
                    OutputPath = folderDialog.SelectedPath,
                    Algorithm = algorithm,
                    Password = password
                });
            }
        }
        private void btnExtractSingle_Click(object sender, EventArgs e)
        {
            using var openFileDialog = new OpenFileDialog
            {
                Filter = "Compressed Files (*.huf;*.sfn)|*.huf;*.sfn|All Files (*.*)|*.*",
                Title = "Select Compressed Archive"
            };

            if (openFileDialog.ShowDialog() != DialogResult.OK)
                return;

            string archivePath = openFileDialog.FileName;
            string password = null;
            string algorithm = null;

            //  تحديد الخوارزمية أولاً
            try
            {
                algorithm = DetectAlgorithmFromFile(archivePath);
                if (algorithm == null)
                {
                    MessageBox.Show("⚠️ Unknown archive format!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to read archive:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // بس للهاف مان، تحقق من التشفير واطلب كلمة السر
            if (algorithm == "huffman")
            {
                bool isEncrypted = CheckIfEncrypted(archivePath);

                if (isEncrypted)
                {
                    using var prompt = new PasswordPrompt();
                    prompt.Text = "This archive is password protected";

                    if (prompt.ShowDialog() == DialogResult.OK)
                    {
                        password = prompt.Password;
                        if (string.IsNullOrWhiteSpace(password))
                        {
                            MessageBox.Show("Password is required for this archive.", "Password Required",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }
            }

            //  قراءة الملفات داخل الأرشيف
            List<string> fileList;
            try
            {
                fileList = algorithm == "huffman"
                    ? compressor.GetFileList(archivePath, password)
                    : shannonCompressor.GetFileList(archivePath);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("❌ كلمة السر غير صحيحة!", "خطأ في كلمة السر", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read archive contents: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (fileList.Count == 0)
            {
                MessageBox.Show("No files found in archive.", "Empty Archive",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var selectForm = new FileSelectForm(fileList);
            if (selectForm.ShowDialog() == DialogResult.OK)
            {
                using var saveDialog = new SaveFileDialog
                {
                    FileName = Path.GetFileName(selectForm.SelectedFile),
                    Title = "Save Extracted File",
                    Filter = "All Files (*.*)|*.*"
                };

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    StartOperation("extractSingle", new
                    {
                        ArchivePath = archivePath,
                        FileName = selectForm.SelectedFile,
                        OutputPath = saveDialog.FileName,
                        Algorithm = algorithm,
                        Password = password
                    });
                }
            }
        }
        private string DetectAlgorithmFromFile(string archivePath)
        {
            try
            {
                using var fileStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(fileStream);

                string signature = reader.ReadString();
                return signature switch
                {
                    "HUF1" => "huffman",
                    "SHF1" => "shannon-fano",
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }
        private bool CheckIfEncrypted(string archivePath)
        {
            try
            {
                using var fileStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(fileStream);

                string signature = reader.ReadString();
                if (signature != "HUF1") return false;

                return reader.ReadBoolean(); 
            }
            catch
            {
                return false;
            }
        }
        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (backgroundWorker.IsBusy)
            {
                backgroundWorker.CancelAsync();
                lblStatus.Text = "⏸️ Cancelling operation...";
            }
        }
        private string currentOperation = "";
        private void StartOperation(string operation, object parameters)
        {
            currentOperation = operation;

            if (!backgroundWorker.IsBusy)
            {
                pauseController.Resume();
                isOperationRunning = true;
                SetControlsEnabled(false);
                progressBar.Value = 0;
                lblCompressionRatio.Text = "📈 Compression Ratio: Calculating...";

                backgroundWorker.RunWorkerAsync(new { Operation = operation, Parameters = parameters });
            }
        }
 
        private void btnPause_Click(object sender, EventArgs e)
        {
            if (backgroundWorker.IsBusy)
            {
                pauseController.Pause();
                btnPause.Enabled = false;
                btnResume.Enabled = true;
                lblStatus.Text = "⏸️ Operation paused - Click Resume to continue";
                progressBar.ForeColor = Color.Orange;
            }
        }
        private void btnResume_Click(object sender, EventArgs e)
        {
            if (backgroundWorker.IsBusy && pauseController.IsPaused)
            {
                pauseController.Resume();
                btnPause.Enabled = true;
                btnResume.Enabled = false;
                lblStatus.Text = "▶️ Operation resumed...";
                progressBar.ForeColor = Color.FromArgb(0, 123, 255);
            }
        }
        private void SetControlsEnabled(bool enabled)
        {
            btnSelectFiles.Enabled = enabled;
            btnRemoveFile.Enabled = enabled;
            btnClearAll.Enabled = enabled;
            btnCompress.Enabled = enabled;
            btnDecompress.Enabled = enabled;
            btnExtractSingle.Enabled = enabled;
            btnCancel.Enabled = !enabled;

            if (enabled)
            {
                btnPause.Enabled = false;
                btnResume.Enabled = false;
                pauseController.Resume(); 
            }
            else
            {
                btnPause.Enabled = true;
                btnResume.Enabled = false;
            }
        }
        private void UpdateButtonStates()
        {
            if (!isOperationRunning)
            {
                btnRemoveFile.Enabled = selectedFiles.Count > 0;
                btnClearAll.Enabled = selectedFiles.Count > 0;
                btnCompress.Enabled = selectedFiles.Count > 0;
            }
        }
        private void UpdateFileCount()
        {
            lblFileCount.Text = $"Files: {selectedFiles.Count}"; 
            long totalSize = 0;

            foreach (string file in selectedFiles)
            {
                if (File.Exists(file))
                {
                    totalSize += new FileInfo(file).Length; 
                }
                else if (Directory.Exists(file))
                {
                    try
                    {
                        var files = Directory.GetFiles(file, "*", SearchOption.AllDirectories);
                        foreach (string subFile in files)
                        {
                            totalSize += new FileInfo(subFile).Length;
                        }
                    }
                    catch (Exception ex)
                    {
                 
                        Console.WriteLine($"Error reading directory {file}: {ex.Message}");
                    }
                }
            }

            lblFileCount.Text += $" | Total Size: {GetFileSizeString(totalSize)}"; 
        }

        private string GetFileSizeString(string filePath)
        {
            if (File.Exists(filePath))
            {
                long size = new FileInfo(filePath).Length;
                return GetFileSizeString(size);
            }
            return "Unknown";
        }

        private string GetFileSizeString(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = sender as BackgroundWorker;
            dynamic args = e.Argument;
            string operation = args.Operation;
            dynamic parameters = args.Parameters;
            

            try
            {
                switch (operation)
                {
                    case "compress":
                        if (parameters.Algorithm == "huffman")
                        {
                            compressor.pauseController = this.pauseController;
                            var ratio = compressor.CompressFiles(parameters.Files, parameters.OutputPath, worker, parameters.Password);
                            e.Result = new { Success = true, CompressionRatio = ratio };
                        }
                        else if (parameters.Algorithm == "shannon-fano")
                        {
                            var ratio = shannonCompressor.CompressFiles(parameters.Files, parameters.OutputPath, worker);
                            e.Result = new { Success = true, CompressionRatio = ratio };
                        }
                        break;

                    case "decompress":
                        if (parameters.Algorithm == "huffman")
                        {
                            compressor.pauseController = this.pauseController;
                            compressor.DecompressArchive(parameters.ArchivePath, parameters.OutputPath, worker, parameters.Password);
                        }
                        else if (parameters.Algorithm == "shannon-fano")
                            shannonCompressor.DecompressArchive(parameters.ArchivePath, parameters.OutputPath, worker);

                        e.Result = new { Success = true };
                        break;

                    case "extractSingle":
                        if (parameters.Algorithm == "huffman")
                        {
                            compressor.pauseController = this.pauseController;
                            compressor.ExtractSingleFile(parameters.ArchivePath, parameters.FileName, parameters.OutputPath, worker, parameters.Password);
                        }
                        else if (parameters.Algorithm == "shannon-fano")
                            shannonCompressor.ExtractSingleFile(parameters.ArchivePath, parameters.FileName, parameters.OutputPath, worker);

                        e.Result = new { Success = true };
                        break;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                e.Result = new { Success = false, Error = "❌ كلمة السر غير صحيحة!" };
            }
            catch (OperationCanceledException)
            {
                e.Cancel = true;
            }
            catch (Exception ex)
            {
                e.Result = new { Success = false, Error = ex.Message };
            }
        }

        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar.Value = Math.Min(e.ProgressPercentage, 100);
            if (e.UserState != null)
            {
                string status = e.UserState.ToString();

                if (pauseController.IsPaused)
                {
                    lblStatus.Text = "⏸️ Paused - Click Resume to continue";
                    progressBar.ForeColor = Color.Orange;
                }
                else
                {
                    lblStatus.Text = $"⚙️ {status}";
                    progressBar.ForeColor = Color.FromArgb(0, 123, 255);
                }
            }
        }
        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            isOperationRunning = false;
            SetControlsEnabled(true);
            UpdateButtonStates();
            progressBar.ForeColor = Color.FromArgb(0, 123, 255);

            if (e.Cancelled)
            {
                lblStatus.Text = "❌ Operation cancelled";
                progressBar.Value = 0;
                MessageBox.Show("Operation was cancelled.", "Cancelled",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (e.Error != null)
            {
                lblStatus.Text = $"❌ Error: {e.Error.Message}";
                progressBar.Value = 0;
                MessageBox.Show($"Operation failed: {e.Error.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                dynamic result = e.Result;
                if (result.Success)
                {
                    lblStatus.Text = "✅ Operation completed successfully!";
                    progressBar.Value = 100;

                    if (currentOperation == "compress" && result.GetType().GetProperty("CompressionRatio") != null)
                    {
                        double ratio = result.CompressionRatio;
                        if (ratio < 0)
                        {
                            lblCompressionRatio.Text = $"⚠️ لم يتم ضغط الملف، الحجم زاد بنسبة {Math.Abs(ratio):F2}%";
                        }
                        else
                        {
                            lblCompressionRatio.Text = $"📈 Compression Ratio: {ratio:F2}%";
                        }
                    }

                    else
                    {
                        lblCompressionRatio.Text = "📈 Compression Ratio: N/A";
                    }

                    MessageBox.Show("Operation completed successfully! 🎉", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    lblStatus.Text = $"❌ Error: {result.Error}";
                    MessageBox.Show($"Operation failed: {result.Error}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnCompare_Click(object sender, EventArgs e)
        {
            if (selectedFiles.Count == 0)
            {
                MessageBox.Show("Please select at least one file to compare.", "No Files Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (selectedFiles.Count > 1)
            {
                MessageBox.Show("Please select only one file for accurate comparison.", "Multiple Files Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string file = selectedFiles[0];
            string tempHuffman = Path.Combine(Path.GetTempPath(), "temp_huffman.huf");
            string tempShannon = Path.Combine(Path.GetTempPath(), "temp_shannon.sfn");

            var sw = new System.Diagnostics.Stopwatch();

            // Huffman
            sw.Start();
            double hRatio = compressor.CompressFiles(new[] { file }, tempHuffman);
            sw.Stop();
            long hTime = sw.ElapsedMilliseconds;
            sw.Reset();

            // Shannon-Fano
            sw.Start();
            double sRatio = shannonCompressor.CompressFiles(new[] { file }, tempShannon);
            sw.Stop();
            long sTime = sw.ElapsedMilliseconds;

            MessageBox.Show(
                $"📌 Comparison for: {Path.GetFileName(file)}\n\n" +
                $"🔸 Huffman:\n   Ratio: {hRatio:F2}%\n   Time: {hTime} ms\n\n" +
                $"🔹 Shannon-Fano:\n   Ratio: {sRatio:F2}%\n   Time: {sTime} ms",
                "📊 Algorithm Comparison",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }


        private System.ComponentModel.IContainer components = null;
        private Button btnSelectFiles;
        private Button btnCompress;
        private Button btnDecompress;
        private Button btnExtractSingle;
        private Button btnCancel;
        private Button btnRemoveFile;
        private Button btnClearAll;
        private ProgressBar progressBar;
        private Label lblStatus;
        private Label lblCompressionRatio;
        private Label lblFileCount;
        private ListBox listBoxFiles;
        private GroupBox groupBoxFiles;
        private GroupBox groupBoxOperations;
        private GroupBox groupBoxProgress;
        private Button btnPause;
        private Button btnResume;
        private PauseController pauseController = new PauseController();
        private Button btnCompare;

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            pauseController?.Dispose();
            compressor?.Dispose();
            base.OnFormClosed(e);
        }
    }
}


