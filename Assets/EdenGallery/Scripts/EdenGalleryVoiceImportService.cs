using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using UnityEngine;

namespace EdenGallery
{
    public sealed class EdenGalleryVoiceImportService
    {
        private const string MetadataFileName = ".eden_voice_pack.info";
        private const int CopyBufferSize = 1024 * 1024;
        private const int MaximumEntryCount = 200000;
        private const long MaximumUncompressedBytes = 64L * 1024L * 1024L * 1024L;

        private static readonly string[] SupportedAudioExtensions =
        {
            ".wav", ".ogg", ".mp3", ".m4a", ".aac"
        };

        private readonly object stateLock = new object();
        private readonly string voiceParentDirectory;
        private readonly string installDirectory;
        private bool isBusy;
        private float progress;
        private string statusMessage = string.Empty;
        private int installedFileCount;
        private long installedBytes;
        private Dictionary<string, string> installedAudioIndex;

        public EdenGalleryVoiceImportService()
            : this(Application.persistentDataPath)
        {
        }

        public EdenGalleryVoiceImportService(string persistentDataRoot)
        {
            voiceParentDirectory = Path.Combine(
                persistentDataRoot,
                "EdenGallery");
            installDirectory = Path.Combine(voiceParentDirectory, "Voice");
            RefreshInstalledInfo();
        }

        public string InstallDirectory { get { return installDirectory; } }

        public bool IsBusy
        {
            get { lock (stateLock) return isBusy; }
        }

        public float Progress
        {
            get { lock (stateLock) return progress; }
        }

        public string StatusMessage
        {
            get { lock (stateLock) return statusMessage; }
        }

        public int InstalledFileCount
        {
            get { lock (stateLock) return installedFileCount; }
        }

        public long InstalledBytes
        {
            get { lock (stateLock) return installedBytes; }
        }

        public bool HasInstalledVoicePack
        {
            get { lock (stateLock) return installedFileCount > 0; }
        }

        public bool BeginImport(string archivePath, bool deleteArchiveAfterImport)
        {
            if (string.IsNullOrEmpty(archivePath) || !File.Exists(archivePath))
            {
                SetIdleStatus("没有找到选中的语音压缩包。");
                return false;
            }

            lock (stateLock)
            {
                if (isBusy)
                    return false;
                isBusy = true;
                progress = 0f;
                statusMessage = "正在检查语音压缩包…";
                installedAudioIndex = null;
            }

            Thread worker = new Thread(delegate()
            {
                ImportWorker(archivePath, deleteArchiveAfterImport);
            });
            worker.Name = "EdenGalleryVoiceImport";
            worker.IsBackground = true;
            worker.Start();
            return true;
        }

        private void ImportWorker(string archivePath, bool deleteArchiveAfterImport)
        {
            string stagingDirectory = installDirectory + ".importing";
            string backupDirectory = installDirectory + ".backup";
            try
            {
                Directory.CreateDirectory(voiceParentDirectory);
                DeleteDirectoryIfPresent(stagingDirectory);
                Directory.CreateDirectory(stagingDirectory);

                long totalUncompressedBytes = 0L;
                int entryCount = 0;
                using (FileStream archiveStream = new FileStream(
                    archivePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                using (ZipArchive archive = new ZipArchive(
                    archiveStream,
                    ZipArchiveMode.Read,
                    false,
                    Encoding.UTF8))
                {
                    entryCount = archive.Entries.Count;
                    if (entryCount == 0)
                        throw new InvalidDataException("压缩包是空的。");
                    if (entryCount > MaximumEntryCount)
                        throw new InvalidDataException("压缩包中的文件数量过多。");

                    for (int i = 0; i < archive.Entries.Count; i++)
                    {
                        ZipArchiveEntry entry = archive.Entries[i];
                        GetSafeDestinationPath(stagingDirectory, entry.FullName);
                        if (entry.Length < 0L ||
                            totalUncompressedBytes > MaximumUncompressedBytes - entry.Length)
                        {
                            throw new InvalidDataException("压缩包解压后的体积过大。");
                        }
                        totalUncompressedBytes += entry.Length;
                    }

                    long extractedBytes = 0L;
                    byte[] buffer = new byte[CopyBufferSize];
                    for (int i = 0; i < archive.Entries.Count; i++)
                    {
                        ZipArchiveEntry entry = archive.Entries[i];
                        string destinationPath = GetSafeDestinationPath(
                            stagingDirectory,
                            entry.FullName);
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(destinationPath);
                            continue;
                        }

                        string destinationParent = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(destinationParent))
                            Directory.CreateDirectory(destinationParent);
                        using (Stream input = entry.Open())
                        using (FileStream output = new FileStream(
                            destinationPath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None))
                        {
                            int read;
                            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                output.Write(buffer, 0, read);
                                extractedBytes += read;
                                SetImportProgress(
                                    totalUncompressedBytes <= 0L
                                        ? 0.9f
                                        : Mathf.Clamp01(
                                            (float)extractedBytes /
                                            totalUncompressedBytes) * 0.9f,
                                    "正在解压语音文件 " + (i + 1) + "/" + entryCount);
                            }
                        }
                    }
                }

                SetImportProgress(0.94f, "正在建立语音文件索引…");
                int audioFileCount;
                long audioBytes;
                ScanAudioFiles(stagingDirectory, out audioFileCount, out audioBytes);
                if (audioFileCount == 0)
                    throw new InvalidDataException("压缩包中没有 WAV、OGG、MP3、M4A 或 AAC 语音文件。");
                WriteMetadata(stagingDirectory, audioFileCount, audioBytes);

                SetImportProgress(0.97f, "正在安装语音资源…");
                DeleteDirectoryIfPresent(backupDirectory);
                bool movedExistingPack = false;
                if (Directory.Exists(installDirectory))
                {
                    Directory.Move(installDirectory, backupDirectory);
                    movedExistingPack = true;
                }
                try
                {
                    Directory.Move(stagingDirectory, installDirectory);
                    DeleteDirectoryIfPresent(backupDirectory);
                }
                catch
                {
                    DeleteDirectoryIfPresent(installDirectory);
                    if (movedExistingPack && Directory.Exists(backupDirectory))
                        Directory.Move(backupDirectory, installDirectory);
                    throw;
                }

                lock (stateLock)
                {
                    installedFileCount = audioFileCount;
                    installedBytes = audioBytes;
                    installedAudioIndex = null;
                    progress = 1f;
                    statusMessage = "语音资源导入完成。";
                    isBusy = false;
                }
            }
            catch (Exception exception)
            {
                DeleteDirectoryIfPresent(stagingDirectory);
                RefreshInstalledInfo();
                lock (stateLock)
                {
                    progress = 0f;
                    statusMessage = "语音资源导入失败：" + exception.Message;
                    isBusy = false;
                }
            }
            finally
            {
                if (deleteArchiveAfterImport)
                {
                    try
                    {
                        File.Delete(archivePath);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void RefreshInstalledInfo()
        {
            int fileCount = 0;
            long byteCount = 0L;
            string metadataPath = Path.Combine(installDirectory, MetadataFileName);
            try
            {
                if (File.Exists(metadataPath))
                {
                    string[] lines = File.ReadAllLines(metadataPath);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        int separator = line.IndexOf('=');
                        if (separator <= 0)
                            continue;
                        string key = line.Substring(0, separator);
                        string value = line.Substring(separator + 1);
                        if (key == "fileCount")
                            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out fileCount);
                        else if (key == "totalBytes")
                            long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out byteCount);
                    }
                }
            }
            catch
            {
                fileCount = 0;
                byteCount = 0L;
            }

            lock (stateLock)
            {
                installedFileCount = Mathf.Max(0, fileCount);
                installedBytes = Math.Max(0L, byteCount);
                installedAudioIndex = null;
                if (!isBusy)
                {
                    statusMessage = installedFileCount > 0
                        ? "语音资源已安装。"
                        : "尚未导入语音资源。";
                }
            }
        }

        public bool TryResolveAudioFile(string audioFileOrVoicePath, out string audioPath)
        {
            audioPath = null;
            if (string.IsNullOrEmpty(audioFileOrVoicePath) || IsBusy)
                return false;

            Dictionary<string, string> index;
            lock (stateLock)
                index = installedAudioIndex;
            if (index == null)
            {
                index = BuildInstalledAudioIndex();
                lock (stateLock)
                {
                    if (!isBusy)
                        installedAudioIndex = index;
                }
            }

            string fileName = Path.GetFileName(audioFileOrVoicePath);
            if (!string.IsNullOrEmpty(fileName) && index.TryGetValue(fileName, out audioPath))
                return true;
            string stem = Path.GetFileNameWithoutExtension(fileName);
            return !string.IsNullOrEmpty(stem) && index.TryGetValue(stem, out audioPath);
        }

        private Dictionary<string, string> BuildInstalledAudioIndex()
        {
            Dictionary<string, string> index =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(installDirectory))
                return index;

            try
            {
                string[] files = Directory.GetFiles(
                    installDirectory,
                    "*",
                    SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string file = files[i];
                    if (!IsSupportedAudioFile(file))
                        continue;
                    string fileName = Path.GetFileName(file);
                    string stem = Path.GetFileNameWithoutExtension(file);
                    if (!string.IsNullOrEmpty(fileName) && !index.ContainsKey(fileName))
                        index.Add(fileName, file);
                    if (!string.IsNullOrEmpty(stem) && !index.ContainsKey(stem))
                        index.Add(stem, file);
                }
            }
            catch
            {
            }
            return index;
        }

        private static string GetSafeDestinationPath(string rootDirectory, string entryName)
        {
            if (string.IsNullOrEmpty(entryName))
                throw new InvalidDataException("压缩包中包含无效文件名。");
            string normalizedName = entryName.Replace('\\', '/');
            while (normalizedName.StartsWith("/", StringComparison.Ordinal))
                normalizedName = normalizedName.Substring(1);
            string rootPath = Path.GetFullPath(rootDirectory + Path.DirectorySeparatorChar);
            string destinationPath = Path.GetFullPath(Path.Combine(rootDirectory, normalizedName));
            if (!destinationPath.StartsWith(rootPath, StringComparison.Ordinal))
                throw new InvalidDataException("压缩包中包含不安全的文件路径。");
            return destinationPath;
        }

        private static void ScanAudioFiles(
            string directory,
            out int audioFileCount,
            out long audioBytes)
        {
            audioFileCount = 0;
            audioBytes = 0L;
            string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                if (!IsSupportedAudioFile(files[i]))
                    continue;
                audioFileCount++;
                audioBytes += new FileInfo(files[i]).Length;
            }
        }

        private static bool IsSupportedAudioFile(string path)
        {
            string extension = Path.GetExtension(path);
            for (int i = 0; i < SupportedAudioExtensions.Length; i++)
            {
                if (string.Equals(
                    extension,
                    SupportedAudioExtensions[i],
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static void WriteMetadata(
            string directory,
            int audioFileCount,
            long audioBytes)
        {
            string[] lines =
            {
                "version=1",
                "fileCount=" + audioFileCount.ToString(CultureInfo.InvariantCulture),
                "totalBytes=" + audioBytes.ToString(CultureInfo.InvariantCulture),
                "importedUtcTicks=" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)
            };
            File.WriteAllLines(Path.Combine(directory, MetadataFileName), lines);
        }

        private void SetImportProgress(float value, string message)
        {
            lock (stateLock)
            {
                progress = Mathf.Clamp01(value);
                statusMessage = message;
            }
        }

        private void SetIdleStatus(string message)
        {
            lock (stateLock)
            {
                statusMessage = message;
            }
        }

        private static void DeleteDirectoryIfPresent(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
            catch
            {
            }
        }
    }
}
