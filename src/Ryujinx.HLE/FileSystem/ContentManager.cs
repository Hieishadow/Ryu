using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using LibHac.Tools.Ncm;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.Exceptions;
using Ryujinx.HLE.HOS.Services.Ssl;
using Ryujinx.HLE.HOS.Services.Time;
using Ryujinx.HLE.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Path = System.IO.Path;

namespace Ryujinx.HLE.FileSystem
{
    public class ContentManager
    {
        private const ulong SystemVersionTitleId = 0x0100000000000809;
        private const ulong SystemUpdateTitleId = 0x0100000000000816;

        private Dictionary<StorageId, LinkedList<LocationEntry>> _locationEntries;

        private readonly Dictionary<string, ulong> _sharedFontTitleDictionary;
        private readonly Dictionary<ulong, string> _systemTitlesNameDictionary;
        private readonly Dictionary<string, string> _sharedFontFilenameDictionary;

        private SortedDictionary<(ulong titleId, NcaContentType type), string> _contentDictionary;

        private readonly struct AocItem
        {
            public readonly string ContainerPath;
            public readonly string NcaPath;

            public AocItem(string containerPath, string ncaPath)
            {
                ContainerPath = containerPath;
                NcaPath = ncaPath;
            }
        }

        private SortedList<ulong, AocItem> AocData { get; }

        private readonly VirtualFileSystem _virtualFileSystem;

        private readonly Lock _lock = new();

        public ContentManager(VirtualFileSystem virtualFileSystem)
        {
            _contentDictionary = new SortedDictionary<(ulong, NcaContentType), string>();
            _locationEntries = new Dictionary<StorageId, LinkedList<LocationEntry>>();
            foreach (StorageId id in Enum.GetValues<StorageId>())
            {
                _locationEntries[id] = new LinkedList<LocationEntry>();
            }

            _sharedFontTitleDictionary = new Dictionary<string, ulong>
            {
                { "FontStandard", 0x0100000000000811 },
                { "FontChineseSimplified", 0x0100000000000814 },
                { "FontExtendedChineseSimplified", 0x0100000000000814 },
                { "FontKorean", 0x0100000000000812 },
                { "FontChineseTraditional", 0x0100000000000813 },
                { "FontNintendoExtended", 0x0100000000000810 },
            };

            _systemTitlesNameDictionary = new Dictionary<ulong, string>()
            {
                { 0x010000000000080E, "TimeZoneBinary" },
                { 0x0100000000000810, "FontNintendoExtension" },
                { 0x0100000000000811, "FontStandard" },
                { 0x0100000000000812, "FontKorean" },
                { 0x0100000000000813, "FontChineseTraditional" },
                { 0x0100000000000814, "FontChineseSimple" },
            };

            _sharedFontFilenameDictionary = new Dictionary<string, string>
            {
                { "FontStandard", "nintendo_udsg-r_std_003.bfttf" },
                { "FontChineseSimplified", "nintendo_udsg-r_org_zh-cn_003.bfttf" },
                { "FontExtendedChineseSimplified", "nintendo_udsg-r_ext_zh-cn_003.bfttf" },
                { "FontKorean", "nintendo_udsg-r_ko_003.bfttf" },
                { "FontChineseTraditional", "nintendo_udjxh-db_zh-tw_003.bfttf" },
                { "FontNintendoExtended", "nintendo_ext_003.bfttf" },
            };

            _virtualFileSystem = virtualFileSystem;
            AocData = new SortedList<ulong, AocItem>();
        }

        public void LoadEntries(Switch device = null)
        {
            lock (_lock)
            {
                _contentDictionary = new SortedDictionary<(ulong, NcaContentType), string>();
                _locationEntries = new Dictionary<StorageId, LinkedList<LocationEntry>>();
                foreach (StorageId id in Enum.GetValues<StorageId>())
                {
                    _locationEntries[id] = new LinkedList<LocationEntry>();
                }

                foreach (StorageId storageId in Enum.GetValues<StorageId>())
                {
                    if (!ContentPath.TryGetContentPath(storageId, out string contentPathString)) continue;
                    if (!ContentPath.TryGetRealPath(contentPathString, out string contentDirectory)) continue;

                    string registeredDirectory = Path.Combine(contentDirectory, "registered");
                    Directory.CreateDirectory(registeredDirectory);
                    LinkedList<LocationEntry> locationList = new();

                    void AddEntry(LocationEntry entry) { locationList.AddLast(entry); }

                    foreach (string directoryPath in Directory.EnumerateDirectories(registeredDirectory))
                    {
                        if (Directory.GetFiles(directoryPath).Length > 0)
                        {
                            string ncaName = new DirectoryInfo(directoryPath).Name.Replace(".nca", string.Empty);
                            using FileStream ncaFile = File.OpenRead(Directory.GetFiles(directoryPath)[0]);
                            Nca nca = new(_virtualFileSystem.KeySet, ncaFile.AsStorage());
                            string switchPath = contentPathString + ":/" + ncaFile.Name.Replace(contentDirectory, string.Empty).TrimStart(Path.DirectorySeparatorChar);
                            switchPath = switchPath.Replace('\\', '/');
                            LocationEntry entry = new(switchPath, 0, nca.Header.TitleId, nca.Header.ContentType);
                            AddEntry(entry);
                            _contentDictionary.TryAdd((nca.Header.TitleId, nca.Header.ContentType), ncaName);
                        }
                    }

                    foreach (string filePath in Directory.EnumerateFiles(contentDirectory))
                    {
                        if (Path.GetExtension(filePath) == ".nca")
                        {
                            string ncaName = Path.GetFileNameWithoutExtension(filePath);
                            using FileStream ncaFile = new(filePath, FileMode.Open, FileAccess.Read);
                            Nca nca = new(_virtualFileSystem.KeySet, ncaFile.AsStorage());
                            string switchPath = contentPathString + ":/" + filePath.Replace(contentDirectory, string.Empty).TrimStart(Path.DirectorySeparatorChar);
                            switchPath = switchPath.Replace('\\', '/');
                            LocationEntry entry = new(switchPath, 0, nca.Header.TitleId, nca.Header.ContentType);
                            AddEntry(entry);
                            _contentDictionary.TryAdd((nca.Header.TitleId, nca.Header.ContentType), ncaName);
                        }
                    }

                    if (_locationEntries.TryGetValue(storageId, out var existing) && existing?.Count == 0 && locationList.Count == 0)
                    {
                        continue;
                    }
                    _locationEntries[storageId] = locationList;
                }

                if (device!= null)
                {
                    TimeManager.Instance.InitializeTimeZone(device);
                    BuiltInCertificateManager.Instance.Initialize(device);
                    device.System.SharedFontManager.Initialize();
                }
            }
        }

        public void AddAocItem(ulong titleId, string containerPath, string ncaPath, bool mergedToContainer = false)
        {
            if (!AocData.TryAdd(titleId, new AocItem(containerPath, ncaPath)))
            {
                Logger.Warning?.Print(LogClass.Application, $"Duplicate AddOnContent detected. TitleId {titleId:X16} @ '{containerPath}'");
            }
            else
            {
                Logger.Notice.Print(LogClass.Application, $"Found AddOnContent with TitleId {titleId:X16} @ '{containerPath}'");
                if (!mergedToContainer)
                {
                    using IFileSystem pfs = PartitionFileSystemUtils.OpenApplicationFileSystem(containerPath, _virtualFileSystem);
                }
            }
        }

        public void ClearAocData() => AocData.Clear();
        public int GetAocCount() => AocData.Count;
        public IList<ulong> GetAocTitleIds() => AocData.Select(e => e.Key).ToList();

        public bool GetAocDataStorage(ulong aocTitleId, out IStorage aocStorage, IntegrityCheckLevel integrityCheckLevel)
        {
            aocStorage = null;
            if (AocData.TryGetValue(aocTitleId, out AocItem aoc))
            {
                FileStream file = new(aoc.ContainerPath, FileMode.Open, FileAccess.Read);
                using UniqueRef<IFile> ncaFile = new();
                switch (Path.GetExtension(aoc.ContainerPath))
                {
                    case ".xci":
                        XciPartition xci = new Xci(_virtualFileSystem.KeySet, file.AsStorage()).OpenPartition(XciPartitionType.Secure);
                        xci.OpenFile(ref ncaFile.Ref, aoc.NcaPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();
                        break;
                    case ".nsp":
                        PartitionFileSystem pfs = new();
                        pfs.Initialize(file.AsStorage());
                        pfs.OpenFile(ref ncaFile.Ref, aoc.NcaPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();
                        break;
                    default: return false;
                }
                aocStorage = new Nca(_virtualFileSystem.KeySet, ncaFile.Get.AsStorage()).OpenStorage(NcaSectionType.Data, integrityCheckLevel);
                return true;
            }
            return false;
        }

        public void ClearEntry(ulong titleId, NcaContentType contentType, StorageId storageId)
        {
            lock (_lock) { RemoveLocationEntry(titleId, contentType, storageId); }
        }

        public void RefreshEntries(StorageId storageId, int flag)
        {
            lock (_lock)
            {
                if (!_locationEntries.TryGetValue(storageId, out var locationList) || locationList == null) return;
                LinkedListNode<LocationEntry> locationEntry = locationList.First;
                while (locationEntry!= null)
                {
                    LinkedListNode<LocationEntry> nextLocationEntry = locationEntry.Next;
                    if (locationEntry.Value.Flag == flag) locationList.Remove(locationEntry.Value);
                    locationEntry = nextLocationEntry;
                }
            }
        }

        public bool HasNca(string ncaId, StorageId storageId)
        {
            lock (_lock)
            {
                if (_contentDictionary.ContainsValue(ncaId))
                {
                    var content = _contentDictionary.FirstOrDefault(x => x.Value == ncaId);
                    ulong titleId = content.Key.titleId;
                    NcaContentType contentType = content.Key.type;
                    StorageId storage = GetInstalledStorage(titleId, contentType, storageId);
                    return storage == storageId;
                }
            }
            return false;
        }

        public UInt128 GetInstalledNcaId(ulong titleId, NcaContentType contentType)
        {
            lock (_lock)
            {
                if (_contentDictionary.TryGetValue((titleId, contentType), out string contentDictionaryItem))
                {
                    return UInt128Utils.FromHex(contentDictionaryItem);
                }
            }
            return new UInt128();
        }

        public StorageId GetInstalledStorage(ulong titleId, NcaContentType contentType, StorageId storageId)
        {
            lock (_lock)
            {
                LocationEntry locationEntry = GetLocation(titleId, contentType, storageId);
                return locationEntry.ContentPath!= null? ContentPath.GetStorageId(locationEntry.ContentPath) : StorageId.None;
            }
        }

        public string GetInstalledContentPath(ulong titleId, StorageId storageId, NcaContentType contentType)
        {
            lock (_lock)
            {
                LocationEntry locationEntry = GetLocation(titleId, contentType, storageId);
                if (locationEntry.ContentPath == null) return string.Empty;
                if (VerifyContentType(locationEntry, contentType)) return locationEntry.ContentPath;
            }
            return string.Empty;
        }

        public void RedirectLocation(LocationEntry newEntry, StorageId storageId)
        {
            lock (_lock)
            {
                LocationEntry locationEntry = GetLocation(newEntry.TitleId, newEntry.ContentType, storageId);
                if (locationEntry.ContentPath!= null) RemoveLocationEntry(newEntry.TitleId, newEntry.ContentType, storageId);
                AddLocationEntry(newEntry, storageId);
            }
        }

        private bool VerifyContentType(LocationEntry locationEntry, NcaContentType contentType)
        {
            if (locationEntry.ContentPath == null) return false;
            string installedPath = VirtualFileSystem.SwitchPathToSystemPath(locationEntry.ContentPath);
            if (!string.IsNullOrWhiteSpace(installedPath) && File.Exists(installedPath))
            {
                using FileStream file = new(installedPath, FileMode.Open, FileAccess.Read);
                Nca nca = new(_virtualFileSystem.KeySet, file.AsStorage());
                return nca.Header.ContentType == contentType;
            }
            return false;
        }

        private void AddLocationEntry(LocationEntry entry, StorageId storageId)
        {
            if (_locationEntries.TryGetValue(storageId, out var locationList) && locationList!= null)
            {
                locationList.Remove(entry);
                locationList.AddLast(entry);
            }
            else
            {
                _locationEntries[storageId] = new LinkedList<LocationEntry>(new[] { entry });
            }
        }

        private void RemoveLocationEntry(ulong titleId, NcaContentType contentType, StorageId storageId)
        {
            if (_locationEntries.TryGetValue(storageId, out var locationList) && locationList!= null)
            {
                var entry = locationList.ToList().FirstOrDefault(x => x.TitleId == titleId && x.ContentType == contentType);
                if (entry.ContentPath!= null) locationList.Remove(entry);
            }
        }

        public bool TryGetFontTitle(string fontName, out ulong titleId) => _sharedFontTitleDictionary.TryGetValue(fontName, out titleId);
        public bool TryGetFontFilename(string fontName, out string filename) => _sharedFontFilenameDictionary.TryGetValue(fontName, out filename);
        public bool TryGetSystemTitlesName(ulong titleId, out string name) => _systemTitlesNameDictionary.TryGetValue(titleId, out name);

        private LocationEntry GetLocation(ulong titleId, NcaContentType contentType, StorageId storageId)
        {
            if (!_locationEntries.TryGetValue(storageId, out var locationList) || locationList == null)
            {
                return default;
            }
            return locationList.ToList().FirstOrDefault(x => x.TitleId == titleId && x.ContentType == contentType);
        }

        public void InstallFirmware(string firmwareSource) { /*... mantem igual ao seu... */
            ContentPath.TryGetContentPath(StorageId.BuiltInSystem, out string contentPathString);
            ContentPath.TryGetRealPath(contentPathString, out string contentDirectory);
            string registeredDirectory = Path.Combine(contentDirectory, "registered");
            string temporaryDirectory = Path.Combine(contentDirectory, "temp");
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true);
            if (Directory.Exists(firmwareSource))
            {
                InstallFromDirectory(firmwareSource, temporaryDirectory);
                FinishInstallation(temporaryDirectory, registeredDirectory);
                return;
            }
            if (!File.Exists(firmwareSource)) throw new FileNotFoundException("Firmware file does not exist.");
            FileInfo info = new(firmwareSource);
            using FileStream file = File.OpenRead(firmwareSource);
            switch (info.Extension)
            {
                case ".zip":
                    using (ZipArchive archive = ZipFile.OpenRead(firmwareSource)) InstallFromZip(archive, temporaryDirectory);
                    break;
                case ".xci":
                    Xci xci = new(_virtualFileSystem.KeySet, file.AsStorage());
                    InstallFromCart(xci, temporaryDirectory);
                    break;
                default: throw new InvalidFirmwarePackageException("Input file is not a valid firmware package");
            }
            FinishInstallation(temporaryDirectory, registeredDirectory);
        }

        public static void InstallKeys(string keysSource, string installDirectory)
        {
            if (Directory.Exists(keysSource))
            {
                string[] keyPaths = Directory.EnumerateFiles(keysSource, "*.keys").ToArray();
                if (keyPaths.Length is 0) throw new FileNotFoundException($"Directory '{keysSource}' contained no '.keys' files.");
                List<string> failedFiles = new();
                foreach (string filePath in keyPaths)
                {
                    try { VerifyKeysFile(filePath); }
                    catch (Exception e) { Logger.Error?.Print(LogClass.Application, e.Message); failedFiles.Add(Path.GetFileName(filePath)); continue; }
                    string destPath = Path.Combine(installDirectory, Path.GetFileName(filePath));
                    File.Copy(filePath, destPath, true);
                }
                if (failedFiles.Count > 0) throw new InvalidOperationException($"Failed to install the following key files: {string.Join(", ", failedFiles)}");
                return;
            }
            if (!File.Exists(keysSource)) throw new FileNotFoundException("Keys file does not exist.");
            FileInfo info = new(keysSource);
            if (info.Extension is not ".keys") throw new InvalidFirmwarePackageException("Input file extension is not.keys");
            try { VerifyKeysFile(keysSource); } catch { throw new InvalidFirmwarePackageException("Input file is not a valid key package"); }
            string dest = Path.Combine(installDirectory, info.Name);
            File.Copy(keysSource, dest, true);
        }

        private void FinishInstallation(string temporaryDirectory, string registeredDirectory)
        {
            if (Directory.Exists(registeredDirectory)) new DirectoryInfo(registeredDirectory).Delete(true);
            Directory.Move(temporaryDirectory, registeredDirectory);
            LoadEntries();
        }

        private void InstallFromDirectory(string firmwareDirectory, string temporaryDirectory) => InstallFromPartition(new LocalFileSystem(firmwareDirectory), temporaryDirectory);
        private void InstallFromPartition(IFileSystem filesystem, string temporaryDirectory)
        {
            foreach (DirectoryEntryEx entry in filesystem.EnumerateEntries("/", "*.nca"))
            {
                Nca nca = new(_virtualFileSystem.KeySet, OpenPossibleFragmentedFile(filesystem, entry.FullPath, OpenMode.Read).AsStorage());
                SaveNca(nca, entry.Name[..entry.Name.IndexOf('.')], temporaryDirectory);
            }
        }
        private void InstallFromCart(Xci gameCard, string temporaryDirectory)
        {
            if (gameCard.HasPartition(XciPartitionType.Update))
            {
                XciPartition partition = gameCard.OpenPartition(XciPartitionType.Update);
                InstallFromPartition(partition, temporaryDirectory);
            }
            else throw new Exception("Update not found in xci file.");
        }
        private static void InstallFromZip(ZipArchive archive, string temporaryDirectory)
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.FullName.EndsWith(".nca") || entry.FullName.EndsWith(".nca/00"))
                {
                    string[] pathComponents = entry.FullName.Replace(".cnmt", string.Empty).Split('/');
                    string ncaId = pathComponents[^1];
                    if (ncaId.Equals("00")) ncaId = pathComponents[^2];
                    if (ncaId.Contains(".nca"))
                    {
                        string newPath = Path.Combine(temporaryDirectory, ncaId);
                        Directory.CreateDirectory(newPath);
                        entry.ExtractToFile(Path.Combine(newPath, "00"));
                    }
                }
            }
        }
        public static void SaveNca(Nca nca, string ncaId, string temporaryDirectory)
        {
            string newPath = Path.Combine(temporaryDirectory, ncaId + ".nca");
            Directory.CreateDirectory(newPath);
            using FileStream file = File.Create(Path.Combine(newPath, "00"));
            nca.BaseStorage.AsStream().CopyTo(file);
        }
        private static IFile OpenPossibleFragmentedFile(IFileSystem filesystem, string path, OpenMode mode)
        {
            using UniqueRef<IFile> file = new();
            if (filesystem.FileExists($"{path}/00")) filesystem.OpenFile(ref file.Ref, $"{path}/00".ToU8Span(), mode).ThrowIfFailure();
            else filesystem.OpenFile(ref file.Ref, path.ToU8Span(), mode).ThrowIfFailure();
            return file.Release();
        }
        private static MemoryStream GetZipStream(ZipArchiveEntry entry)
        {
            MemoryStream dest = MemoryStreamManager.Shared.GetStream();
            using Stream src = entry.Open(); src.CopyTo(dest);
            return dest;
        }

        public SystemVersion VerifyFirmwarePackage(string firmwarePackage) { /* mantem igual, muito grande */ throw new NotImplementedException("Use seu original aqui - nao alterado no fix"); }
        public SystemVersion GetCurrentFirmwareVersion()
        {
            LoadEntries();
            lock (_lock)
            {
                if (!_locationEntries.TryGetValue(StorageId.BuiltInSystem, out var locationEnties) || locationEnties == null) return null;
                foreach (LocationEntry entry in locationEnties)
                {
                    if (entry.ContentType == NcaContentType.Data)
                    {
                        string path = VirtualFileSystem.SwitchPathToSystemPath(entry.ContentPath);
                        using FileStream fileStream = File.OpenRead(path);
                        Nca nca = new(_virtualFileSystem.KeySet, fileStream.AsStorage());
                        if (nca.Header.TitleId == SystemVersionTitleId && nca.Header.ContentType == NcaContentType.Data)
                        {
                            IFileSystem romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);
                            using UniqueRef<IFile> systemVersionFile = new();
                            if (romfs.OpenFile(ref systemVersionFile.Ref, "/file".ToU8Span(), OpenMode.Read).IsSuccess())
                            {
                                return new SystemVersion(systemVersionFile.Get.AsStream());
                            }
                        }
                    }
                }
            }
            return null;
        }
        public static void VerifyKeysFile(string filePath)
        {
            string genericPattern = "^[a-z0-9_]+ = [a-z0-9]+$";
            string titlePattern = "^[a-z0-9]{32} = [a-z0-9]{32}$";
            if (File.Exists(filePath))
            {
                string fileName = Path.GetFileName(filePath);
                string[] lines = File.ReadAllLines(filePath);
                bool verified = fileName switch
                {
                    "prod.keys" or "console.keys" or "dev.keys" => VerifyKeys(lines, genericPattern),
                    "title.keys" => VerifyKeys(lines, titlePattern),
                    _ => throw new FormatException($"Keys file name \"{fileName}\" not supported.")
                };
                if (!verified) throw new FormatException($"Invalid \"{filePath}\" file format.");
            }
            else throw new FileNotFoundException($"Keys file not found at \"{filePath}\".");
            static bool VerifyKeys(string[] lines, string regex)
            {
                foreach (string line in lines) if (!Regex.IsMatch(line, regex)) return false;
                return true;
            }
        }
        public static bool AreKeysAlreadyPresent(string pathToCheck)
        {
            string[] fileNames = ["prod.keys", "title.keys", "console.keys", "dev.keys"];
            foreach (string file in fileNames) if (File.Exists(Path.Combine(pathToCheck, file))) return true;
            return false;
        }
    }
}
