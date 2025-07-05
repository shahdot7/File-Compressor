using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

namespace FileCompressor
{
    [Serializable]
    public class ShannonFanoCompressor
    {
        public double CompressFiles(string[] filePaths, string outputPath, BackgroundWorker worker = null)
        {
            var archive = new ArchiveInfo
            {
                Files = new List<CompressedFileInfo>(),
                CreatedDate = DateTime.Now,
                TotalOriginalSize = 0,
                TotalCompressedSize = 0
            };

            for (int i = 0; i < filePaths.Length; i++)
            {
                if (worker?.CancellationPending == true)
                    throw new OperationCanceledException();

                string filePath = filePaths[i];
                worker?.ReportProgress((i * 100) / filePaths.Length, $"Compressing: {Path.GetFileName(filePath)}");

                var compressedFile = CompressSingleFile(filePath, worker);
                archive.Files.Add(compressedFile);
                archive.TotalOriginalSize += compressedFile.OriginalSize;
                archive.TotalCompressedSize += compressedFile.CompressedSize;
            }

            SaveArchive(archive, outputPath);

            double compressionRatio = archive.TotalOriginalSize > 0
                ? ((double)(archive.TotalOriginalSize - archive.TotalCompressedSize) / archive.TotalOriginalSize) * 100
                : 0;

            worker?.ReportProgress(100, "Compression completed successfully");
            return compressionRatio;
        }

        private CompressedFileInfo CompressSingleFile(string filePath, BackgroundWorker worker = null)
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            if (fileData.Length == 0)
            {
                return new CompressedFileInfo
                {
                    FileName = Path.GetFileName(filePath),
                    OriginalSize = 0,
                    CompressedSize = 0,
                    CompressedData = new byte[0],
                    EncodingTable = new Dictionary<byte, string>(),
                    HuffmanTree = null
                };
            }

            var frequencyTable = BuildFrequencyTable(fileData);
            var encodingTable = BuildShannonFanoTable(frequencyTable);
            var encodedData = EncodeData(fileData, encodingTable, worker);

            return new CompressedFileInfo
            {
                FileName = Path.GetFileName(filePath),
                OriginalSize = fileData.Length,
                CompressedSize = encodedData.Length,
                CompressedData = encodedData,
                EncodingTable = encodingTable
            };
        }

        private Dictionary<byte, int> BuildFrequencyTable(byte[] data)
        {
            return data.GroupBy(b => b).ToDictionary(g => g.Key, g => g.Count());
        }

        private Dictionary<byte, string> BuildShannonFanoTable(Dictionary<byte, int> freqTable)
        {
            var sorted = freqTable.OrderByDescending(kvp => kvp.Value).ToList();
            var table = new Dictionary<byte, string>();
            BuildCode(sorted, table);
            return table;
        }

        private void BuildCode(List<KeyValuePair<byte, int>> symbols, Dictionary<byte, string> table, string prefix = "")
        {
            if (symbols.Count == 1)
            {
                table[symbols[0].Key] = prefix.Length == 0 ? "0" : prefix;
                return;
            }

            int total = symbols.Sum(kv => kv.Value);
            int splitIndex = 1;
            int leftSum = symbols[0].Value;

            while (splitIndex < symbols.Count - 1 && leftSum + symbols[splitIndex].Value <= total / 2)
            {
                leftSum += symbols[splitIndex].Value;
                splitIndex++;
            }

            BuildCode(symbols.GetRange(0, splitIndex), table, prefix + "0");
            BuildCode(symbols.GetRange(splitIndex, symbols.Count - splitIndex), table, prefix + "1");
        }

        private byte[] EncodeData(byte[] data, Dictionary<byte, string> encodingTable, BackgroundWorker worker = null)
        {
            var bitString = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                if (worker?.CancellationPending == true)
                    throw new OperationCanceledException();

                bitString.Append(encodingTable[data[i]]);

                if (i % 100 == 0)
                    System.Threading.Thread.Sleep(1);
            }

            int padding = 8 - (bitString.Length % 8);
            if (padding != 8)
            {
                bitString = bitString.Append('0', padding);
            }

            var result = new byte[bitString.Length / 8 + 1];
            result[0] = (byte)padding;

            for (int i = 0; i < bitString.Length; i += 8)
            {
                result[i / 8 + 1] = Convert.ToByte(bitString.ToString(i, 8), 2);
            }

            return result;
        }

        public void DecompressArchive(string archivePath, string outputPath, BackgroundWorker worker = null)
        {
            var archive = LoadArchive(archivePath);
            for (int i = 0; i < archive.Files.Count; i++)
            {
                if (worker?.CancellationPending == true)
                    throw new OperationCanceledException();

                var file = archive.Files[i];
                worker?.ReportProgress((i * 100) / archive.Files.Count, $"Extracting: {file.FileName}");

                string outputFilePath = Path.Combine(outputPath, file.FileName);
                DecompressSingleFile(file, outputFilePath, worker);
            }
            worker?.ReportProgress(100, "Decompression completed successfully");
        }

        private void DecompressSingleFile(CompressedFileInfo file, string outputPath, BackgroundWorker worker = null)
        {
            if (file.CompressedData.Length == 0)
            {
                File.WriteAllBytes(outputPath, new byte[0]);
                return;
            }

            var bitString = new StringBuilder();
            int padding = file.CompressedData[0];
            for (int i = 1; i < file.CompressedData.Length; i++)
            {
                bitString.Append(Convert.ToString(file.CompressedData[i], 2).PadLeft(8, '0'));
            }

            if (padding > 0 && padding < 8)
            {
                bitString.Length -= padding;
            }

            var decoded = new List<byte>();
            string current = "";
            Thread.Sleep(100);

            var table = file.EncodingTable.ToDictionary(kv => kv.Value, kv => kv.Key);

            foreach (char bit in bitString.ToString())
            {
                if (worker?.CancellationPending == true)
                    throw new OperationCanceledException();

                current += bit;
                if (table.TryGetValue(current, out byte symbol))
                {
                    decoded.Add(symbol);
                    current = "";

                    if (decoded.Count == file.OriginalSize)
                        break;
                }
            }

            File.WriteAllBytes(outputPath, decoded.ToArray());
        }

        private void SaveArchive(ArchiveInfo archive, string outputPath)
        {
            using var fs = new FileStream(outputPath, FileMode.Create);
            using var writer = new BinaryWriter(fs);

            writer.Write("SHF1");
            writer.Write(archive.CreatedDate.ToBinary());
            writer.Write(archive.TotalOriginalSize);
            writer.Write(archive.TotalCompressedSize);
            writer.Write(archive.Files.Count);

            foreach (var file in archive.Files)
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
        }

        private ArchiveInfo LoadArchive(string archivePath)
        {
            using var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            string signature = reader.ReadString();
            if (signature != "SHF1")
                throw new InvalidDataException("Invalid Shannon-Fano archive");

            var archive = new ArchiveInfo
            {
                CreatedDate = DateTime.FromBinary(reader.ReadInt64()),
                TotalOriginalSize = reader.ReadInt64(),
                TotalCompressedSize = reader.ReadInt64(),
                Files = new List<CompressedFileInfo>()
            };

            int fileCount = reader.ReadInt32();
            for (int i = 0; i < fileCount; i++)
            {
                var file = new CompressedFileInfo
                {
                    FileName = reader.ReadString(),
                    OriginalSize = reader.ReadInt64(),
                    CompressedSize = reader.ReadInt64(),
                    EncodingTable = new Dictionary<byte, string>()
                };

                int encodingCount = reader.ReadInt32();
                for (int j = 0; j < encodingCount; j++)
                {
                    byte key = reader.ReadByte();
                    string value = reader.ReadString();
                    file.EncodingTable[key] = value;
                }

                int dataLength = reader.ReadInt32();
                file.CompressedData = reader.ReadBytes(dataLength);

                archive.Files.Add(file);
            }

            return archive;
        }

        public List<string> GetFileList(string archivePath)
        {
            var archive = LoadArchive(archivePath);
            return archive.Files.Select(f => f.FileName).ToList();
        }

        public void ExtractSingleFile(string archivePath, string fileName, string outputPath, BackgroundWorker worker = null)
        {
            var archive = LoadArchive(archivePath);
            var file = archive.Files.FirstOrDefault(f => f.FileName == fileName);
            if (file == null)
                throw new FileNotFoundException($"File '{fileName}' not found in archive");

            DecompressSingleFile(file, outputPath, worker);
        }
    }
}
