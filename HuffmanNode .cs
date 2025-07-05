using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Threading;

namespace FileCompressor
{
    [Serializable]
    public class HuffmanNode : IComparable<HuffmanNode>
    {
        public byte? Value { get; set; }
        public int Frequency { get; set; }
        public HuffmanNode Left { get; set; }
        public HuffmanNode Right { get; set; }
        public bool IsLeaf => Left == null && Right == null;

        public int CompareTo(HuffmanNode other)
        {
            return Frequency.CompareTo(other.Frequency);
        }
    }

    [Serializable]
    public class CompressedFileInfo
    {
        public string FileName { get; set; }
        public long OriginalSize { get; set; }
        public long CompressedSize { get; set; }
        public byte[] CompressedData { get; set; }
        public Dictionary<byte, string> EncodingTable { get; set; }
        public HuffmanNode HuffmanTree { get; set; }
    }

    [Serializable]
    public class ArchiveInfo
    {
        public List<CompressedFileInfo> Files { get; set; }
        public DateTime CreatedDate { get; set; }
        public long TotalOriginalSize { get; set; }
        public long TotalCompressedSize { get; set; }
    }
    /// <summary>
    //  لنتحكم بالإيقاف المؤقت للعمليات
    /// </summary>
    public class PauseController
    {
        private readonly ManualResetEventSlim pauseEvent = new ManualResetEventSlim(true);
        private volatile bool isPaused = false;

        public bool IsPaused => isPaused;

        public void Pause()
        {
            isPaused = true;
            pauseEvent.Reset();
        }

        public void Resume()
        {
            isPaused = false;
            pauseEvent.Set();
        }

        public void WaitIfPaused()
        {
            pauseEvent.Wait();
        }

        public void Dispose()
        {
            pauseEvent?.Dispose();
        }
    }
    public class HuffmanCompressor
    {
        public PauseController pauseController = new PauseController();
        /// <summary>
        //  لضغط الملفات وتخزينا بأرشيف واحد
        /// </summary>
        public double CompressFiles(string[] inputPaths, string outputPath, BackgroundWorker worker = null, string password = null)
        {
            var archive = new ArchiveInfo
            {
                Files = new List<CompressedFileInfo>(),
                CreatedDate = DateTime.Now,
                TotalOriginalSize = 0,
                TotalCompressedSize = 0
            };

            var allFiles = new List<(string fullPath, string relativePath)>();
            string baseFolder = Path.GetDirectoryName(inputPaths[0]);

            foreach (string path in inputPaths)
            {
                if (File.Exists(path))
                {
                    string relative = Path.GetFileName(path);
                    allFiles.Add((path, relative));
                }
                else if (Directory.Exists(path))
                {
                    string folderBase = Path.GetDirectoryName(path);
                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        string relative = Path.GetRelativePath(folderBase, file);
                        allFiles.Add((file, Path.Combine(Path.GetFileName(path), relative)));
                    }
                }
            }

            var compressedFiles = new CompressedFileInfo[allFiles.Count]; // نحافظ على الترتيب
            long totalOriginal = 0, totalCompressed = 0;
            object lockObj = new object();
            /// <summary>
            //  استخدام الملتي ثريد لنضغط الملفات
            /// </summary>
            Parallel.ForEach(Enumerable.Range(0, allFiles.Count), new ParallelOptions { MaxDegreeOfParallelism = 4 }, (i, loopState) =>
            
            {
                if (worker?.CancellationPending == true)
                {
                    loopState.Stop(); // توقف آمن لكل الثريدات
                    return;
                }

                //  pauseController.WaitIfPaused();

                var (fullPath, relativePath) = allFiles[i];

                var compressedFile = CompressSingleFile(fullPath, relativePath, worker);

                lock (lockObj)
                {
                    compressedFiles[i] = compressedFile;
                    totalOriginal += compressedFile.OriginalSize;
                    totalCompressed += compressedFile.CompressedSize;

                    int progress = (int)(((double)(archive.Files.Count + 1) / allFiles.Count) * 100);
                    worker?.ReportProgress(progress, $"Compressing: {relativePath}");
                }
            }
            );
            if (worker?.CancellationPending == true)
    throw new OperationCanceledException("Compression was cancelled.");

            archive.Files.AddRange(compressedFiles.Where(f => f != null));
            archive.TotalOriginalSize = totalOriginal;
            archive.TotalCompressedSize = totalCompressed;

            SaveArchive(archive, outputPath, password);
            /// <summary>
            //  لحساب نسبة الضغط
            /// </summary>
            double compressionRatio = archive.TotalOriginalSize > 0
                ? ((double)(archive.TotalOriginalSize - archive.TotalCompressedSize) / archive.TotalOriginalSize) * 100
                : 0;

            worker?.ReportProgress(100, "Compression completed successfully");
            return compressionRatio;
        }
        /// <summary>
        //  ضغط ملف واحد
        /// </summary>
        private CompressedFileInfo CompressSingleFile(string fullPath, string relativePath, BackgroundWorker worker = null)
        {
            byte[] fileData = File.ReadAllBytes(fullPath);

            if (fileData.Length == 0)
            {
                return new CompressedFileInfo
                {
                    FileName = relativePath,
                    OriginalSize = 0,
                    CompressedSize = 0,
                    CompressedData = new byte[0],
                    EncodingTable = new Dictionary<byte, string>(),
                    HuffmanTree = null
                };
            }

            var frequencyTable = BuildFrequencyTable(fileData);
            var huffmanTree = BuildHuffmanTree(frequencyTable);
            var encodingTable = new Dictionary<byte, string>();
            GenerateEncodingTable(huffmanTree, "", encodingTable);
            var encodedData = EncodeData(fileData, encodingTable, worker);

            return new CompressedFileInfo
            {
                FileName = relativePath,
                OriginalSize = fileData.Length,
                CompressedSize = encodedData.Length,
                CompressedData = encodedData,
                EncodingTable = encodingTable,
                HuffmanTree = huffmanTree
            };
        }

        /// <summary>
        //  جدول التكرارات لكل بايت بالبيانات 
        /// </summary>
        private Dictionary<byte, int> BuildFrequencyTable(byte[] data)
        {
            var frequencyTable = new Dictionary<byte, int>();

            foreach (byte b in data)
            {
                if (frequencyTable.ContainsKey(b))
                    frequencyTable[b]++;
                else
                    frequencyTable[b] = 1;
            }

            return frequencyTable;
        }
        /// <summary>
        // بناء شجرة هافمان بناءاً على جدول التكرار
        /// </summary>
        private HuffmanNode BuildHuffmanTree(Dictionary<byte, int> frequencyTable)
        {
            var priorityQueue = new SortedSet<HuffmanNode>(new HuffmanNodeComparer());

            // Create leaf nodes
            foreach (var kvp in frequencyTable)
            {
                priorityQueue.Add(new HuffmanNode
                {
                    Value = kvp.Key,
                    Frequency = kvp.Value
                });
            }

            // Handle single character case
            if (priorityQueue.Count == 1)
            {
                var singleNode = priorityQueue.First();
                var root = new HuffmanNode
                {
                    Frequency = singleNode.Frequency,
                    Left = singleNode
                };
                return root;
            }

            // Build tree
            while (priorityQueue.Count > 1)
            {
                var left = priorityQueue.Min;
                priorityQueue.Remove(left);

                var right = priorityQueue.Min;
                priorityQueue.Remove(right);

                var parent = new HuffmanNode
                {
                    Frequency = left.Frequency + right.Frequency,
                    Left = left,
                    Right = right
                };

                priorityQueue.Add(parent);
            }

            return priorityQueue.First();
        }

        private void GenerateEncodingTable(HuffmanNode node, string code, Dictionary<byte, string> encodingTable)
        {
            if (node == null) return;

            if (node.IsLeaf && node.Value.HasValue)
            {
                encodingTable[node.Value.Value] = string.IsNullOrEmpty(code) ? "0" : code;
                return;
            }

            GenerateEncodingTable(node.Left, code + "0", encodingTable);
            GenerateEncodingTable(node.Right, code + "1", encodingTable);
        }
        /// <summary>
        //  ضغطنا البيانات لسلسلة ثنائية مضغوطة
        /// </summary>
        private byte[] EncodeData(byte[] data, Dictionary<byte, string> encodingTable, BackgroundWorker worker)
        {
            var bitString = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                if (worker?.CancellationPending == true)
                    throw new OperationCanceledException("Operation canceled by user.");
                

                bitString.Append(encodingTable[data[i]]);
                //  if (i % 100 == 0) Thread.Sleep(100);
            }

            int padding = 8 - (bitString.Length % 8);
            if (padding != 8)
                bitString.Append('0', padding);

            var result = new byte[bitString.Length / 8 + 1];
            result[0] = (byte)padding;

            for (int i = 0; i < bitString.Length; i += 8)
            {   
                if (i % 8000 == 0) // كل 1000 بايت
                {
                    pauseController.WaitIfPaused();
                }

                result[i / 8 + 1] = Convert.ToByte(bitString.ToString(i, 8), 2);
            }

            return result;
        }
        /// <summary>
        //  الحفظ بالأرشيف + هون حفظنا خيار التشفير
        /// </summary>
        private void SaveArchive(ArchiveInfo archive, string outputPath, string password = null)
        {
            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);

            var writer = new BinaryWriter(fileStream, Encoding.UTF8, leaveOpen: true);
            writer.Write("HUF1");

            bool isEncrypted = !string.IsNullOrEmpty(password);
            writer.Write(isEncrypted);

            byte[] iv = null;
            if (isEncrypted)
            {
                using var aes = Aes.Create();
                aes.GenerateIV();
                iv = aes.IV;

                writer.Write(iv.Length);
                writer.Write(iv);

                writer.Flush();
                //fileStream.Flush();
            }

            Stream targetStream = isEncrypted
                ? new CryptoStream(fileStream, Aes.Create().CreateEncryptor(DeriveKey(password, 32), iv), CryptoStreamMode.Write)
                : fileStream;

            using var dataWriter = new BinaryWriter(targetStream, Encoding.UTF8, leaveOpen: false);

            dataWriter.Write(archive.CreatedDate.ToBinary());
            dataWriter.Write(archive.TotalOriginalSize);
            dataWriter.Write(archive.TotalCompressedSize);
            dataWriter.Write(archive.Files.Count);

            foreach (var file in archive.Files)
            {
                WriteCompressedFile(dataWriter, file);
            }

            dataWriter.Flush();
            if (targetStream is CryptoStream cryptoStream)
                cryptoStream.FlushFinalBlock();
        }

        private void WriteCompressedFile(BinaryWriter writer, CompressedFileInfo file)
        {
      
            writer.Write(file.FileName);
            writer.Write(file.OriginalSize);
            writer.Write(file.CompressedSize);

            writer.Write(file.EncodingTable.Count);
            foreach (var kvp in file.EncodingTable)
            {
                writer.Write(kvp.Key);
                writer.Write(kvp.Value);
            }

            writer.Write(file.CompressedData.Length);
            writer.Write(file.CompressedData);
        }
        /// <summary>
        //  فك ضغط أرشيف كامل
        /// </summary>
        public void DecompressArchive(string archivePath, string outputPath, BackgroundWorker worker = null, string password = null)
        {
            var archive = LoadArchive(archivePath, password);

            for (int i = 0; i < archive.Files.Count; i++)
            {
                if (worker?.CancellationPending == true)
                    throw new OperationCanceledException();
                pauseController.WaitIfPaused();

                var file = archive.Files[i];
                worker?.ReportProgress((i * 100) / archive.Files.Count,
                    $"Extracting: {file.FileName}");

                string outputFilePath = Path.Combine(outputPath, file.FileName);
                DecompressSingleFile(file, outputFilePath, worker);
            }

            worker?.ReportProgress(100, "Decompression completed successfully");
        }
        /// <summary>
        //  استخراج ملف واحد من الارشيف
        /// </summary>
        public void ExtractSingleFile(string archivePath, string fileName, string outputPath, BackgroundWorker worker = null, string password = null)
        {
            worker?.ReportProgress(0, $"Loading archive...");

            var archive = LoadArchive(archivePath, password);
            var file = archive.Files.FirstOrDefault(f => f.FileName == fileName);

            if (file == null)
                throw new FileNotFoundException($"File '{fileName}' not found in archive");

            //if (worker?.CancellationPending == true)
            //    throw new OperationCanceledException();

            worker?.ReportProgress(50, $"Extracting: {fileName}");
            DecompressSingleFile(file, outputPath, worker);

            worker?.ReportProgress(100, "Extraction completed successfully");
        }
        /// <summary>
        //  فك ضغط ملف واحد من الارشيف
        /// </summary>
        private void DecompressSingleFile(CompressedFileInfo file, string outputPath, BackgroundWorker worker)
        {
            if (file.CompressedData.Length == 0)
            {
                File.WriteAllBytes(outputPath, new byte[0]);
                return;
            }
            var huffmanTree = RebuildHuffmanTree(file.EncodingTable);

            var decodedData = DecodeData(file.CompressedData, huffmanTree, file.OriginalSize, worker);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            File.WriteAllBytes(outputPath, decodedData);
        }

        private HuffmanNode RebuildHuffmanTree(Dictionary<byte, string> encodingTable)
        {
            var root = new HuffmanNode();

            foreach (var kvp in encodingTable)
            {
                var current = root;
                string code = kvp.Value;

                for (int i = 0; i < code.Length; i++)
                {
                    if (code[i] == '0')
                    {
                        if (current.Left == null)
                            current.Left = new HuffmanNode();
                        current = current.Left;
                    }
                    else
                    {
                        if (current.Right == null)
                            current.Right = new HuffmanNode();
                        current = current.Right;
                    }
                }

                current.Value = kvp.Key;
            }

            return root;
        }
        /// <summary>
        //  فك البيانات من بيانات مضغوطة ل بيانات عادية اصلية
        /// </summary>
        private byte[] DecodeData(byte[] encodedData, HuffmanNode huffmanTree, long originalSize, BackgroundWorker worker)
        {
            if (originalSize == 0) return new byte[0];

            int padding = encodedData[0];

            var bitString = new StringBuilder();
            for (int i = 1; i < encodedData.Length; i++)
            {
                if (i % 1000 == 0)
                {
                    if (worker?.CancellationPending == true)
                        throw new OperationCanceledException();

                    pauseController.WaitIfPaused();
                }

                bitString.Append(Convert.ToString(encodedData[i], 2).PadLeft(8, '0'));
            }

            if (padding > 0 && padding < 8)
            {
                bitString.Length -= padding;
            }

            var decodedData = new List<byte>();
            var current = huffmanTree;
            int bitCount = 0;
            foreach (char bit in bitString.ToString())
            {
                // التحقق من الإيقاف المؤقت كل 10000 بت
                if (bitCount % 10000 == 0)
                {
                    pauseController.WaitIfPaused();
                }
                bitCount++;

                if (bit == '0')
                    current = current.Left;
                else
                    current = current.Right;

                if (current != null && current.IsLeaf && current.Value.HasValue)
                {
                    decodedData.Add(current.Value.Value);
                    current = huffmanTree;

                    if (decodedData.Count >= originalSize)
                        break;
                }
            }

            return decodedData.Take((int)originalSize).ToArray();
        }
        /// <summary>
        //  قراءة الارشيف
        /// </summary>
        private ArchiveInfo LoadArchive(string archivePath, string password = null)
        {
            using var fileStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(fileStream, Encoding.UTF8, leaveOpen: true);

            string signature = reader.ReadString();
            if (signature != "HUF1")
                throw new InvalidDataException($"Invalid archive format. Signature = '{signature}'");

            bool isEncrypted = reader.ReadBoolean();
            byte[] iv = null;
            if (isEncrypted)
            {
                int ivLength = reader.ReadInt32();
                iv = reader.ReadBytes(ivLength);
            }

            // 2. التحقق من الباسورد
            if (isEncrypted && string.IsNullOrWhiteSpace(password))
                throw new UnauthorizedAccessException("🔒 هذا الملف مشفر ويتطلب كلمة سر.");
            if (!isEncrypted && !string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("هذا الملف غير مشفر، لا حاجة لكلمة سر.");

            Stream sourceStream = fileStream;
            if (isEncrypted)
            {
                try
                {
                    var aes = Aes.Create();
                    var key = DeriveKey(password, aes.KeySize / 8);
                    sourceStream = new CryptoStream(fileStream, aes.CreateDecryptor(key, iv), CryptoStreamMode.Read);

                    // تحقق مبدئي من صحة كلمة السر
                    using var checkReader = new BinaryReader(sourceStream, Encoding.UTF8, leaveOpen: true);
                    DateTime.FromBinary(checkReader.ReadInt64()); 
                    long totalOriginalSize = checkReader.ReadInt64();
                    long totalCompressedSize = checkReader.ReadInt64();
                    int fileCount = checkReader.ReadInt32();

                    // تحقق إضافي من القيم
                    if (totalOriginalSize < 0 || totalCompressedSize < 0 || fileCount < 0)
                        throw new InvalidDataException("❌ كلمة السر غير صحيحة أو الملف تالف.");

                    fileStream.Position = 0;
                    reader.ReadString(); // HUF1
                    reader.ReadBoolean(); // isEncrypted
                    reader.ReadInt32(); // ivLength
                    reader.ReadBytes(iv.Length); // iv
                    sourceStream = new CryptoStream(fileStream, aes.CreateDecryptor(key, iv), CryptoStreamMode.Read);
                }
                catch (CryptographicException)
                {
                    throw new InvalidDataException("❌ كلمة السر غير صحيحة.");
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException("❌ فشل في فك التشفير. كلمة السر غير صحيحة أو الملف تالف.", ex);
                }
            }

            // 4. قراءة أرشيف
            try
            {
                using var dataReader = new BinaryReader(sourceStream, Encoding.UTF8);
                var archive = new ArchiveInfo
                {
                    CreatedDate = DateTime.FromBinary(dataReader.ReadInt64()),
                    TotalOriginalSize = dataReader.ReadInt64(),
                    TotalCompressedSize = dataReader.ReadInt64(),
                    Files = new List<CompressedFileInfo>()
                };

                int fileCount = dataReader.ReadInt32();
                for (int i = 0; i < fileCount; i++)
                {
                    archive.Files.Add(ReadCompressedFile(dataReader));
                }

                return archive;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("❌ فشل في قراءة الأرشيف. قد تكون كلمة السر غير صحيحة أو الملف تالف.", ex);
            }
        }
        /// <summary>
        //  بنعمل مفتاح للتشفير من كلمة السر
        /// </summary>
        public static byte[] DeriveKey(string password, int keySize)
        {
            using var sha256 = SHA256.Create();
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] hash = sha256.ComputeHash(passwordBytes);
            return hash.Take(keySize).ToArray();
        }
        /// <summary>
        //  قراءة ملف مضغوط واحد من الارشيف
        /// </summary>
        private CompressedFileInfo ReadCompressedFile(BinaryReader reader)
        {
            var file = new CompressedFileInfo
            {
                FileName = reader.ReadString(),
                OriginalSize = reader.ReadInt64(),
                CompressedSize = reader.ReadInt64(),
                EncodingTable = new Dictionary<byte, string>()
            };

            int encodingTableCount = reader.ReadInt32();
            for (int i = 0; i < encodingTableCount; i++)
            {
                byte key = reader.ReadByte();
                string value = reader.ReadString();
                file.EncodingTable[key] = value;
            }

            int dataLength = reader.ReadInt32();
            file.CompressedData = reader.ReadBytes(dataLength);

            return file;
        }

        public List<string> GetFileList(string archivePath, string password = null)
        {
            var archive = LoadArchive(archivePath, password);
            return archive.Files.Select(f => f.FileName).ToList();
        }

        public class HuffmanNodeComparer : IComparer<HuffmanNode>
        {
            private static int counter = 0;

            public int Compare(HuffmanNode x, HuffmanNode y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                int freqComparison = x.Frequency.CompareTo(y.Frequency);
                if (freqComparison != 0)
                    return freqComparison;

                return x.GetHashCode().CompareTo(y.GetHashCode());
            }
        }
        public void Dispose()
        {
            pauseController?.Dispose();
        }
    }
}