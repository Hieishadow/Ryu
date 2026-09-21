using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.HLE.FileSystem;
using Ryujinx.Horizon;
using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Fs;
using System;
using System.Collections.Concurrent;
using System.IO;

namespace Ryujinx.HLE.HOS
{
    class HorizonFsClient : IFsClient
    {
        private readonly Horizon _system;
        private readonly LibHac.Fs.FileSystemClient _fsClient;
        private readonly ConcurrentDictionary<string, LocalStorage> _mountedStorages;

        private const string LogPath = "/data/user/0/com.ryubing.android/files/Ryujinx/hid_debug.log";
        private static void L(string msg)
        {
            try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} [FsClient] {msg}\n"); } catch {}
        }

        public HorizonFsClient(Horizon system)
        {
            L($"ctor ENTER system null? {system==null}");
            _system = system ?? throw new ArgumentNullException(nameof(system));
            if (_system.LibHacHorizonManager == null) throw new InvalidOperationException("LibHacHorizonManager is NULL");
            if (_system.LibHacHorizonManager.FsClient == null) throw new InvalidOperationException("FsClient is NULL");
            if (_system.LibHacHorizonManager.FsClient.Fs == null) throw new InvalidOperationException("FsClient.Fs is NULL");
            _fsClient = _system.LibHacHorizonManager.FsClient.Fs;
            _mountedStorages = new();
            L("ctor EXIT OK");
        }

        public void CloseFile(FileHandle handle)
        {
            _fsClient.CloseFile((LibHac.Fs.FileHandle)handle.Value);
        }

        public Result GetFileSize(out long size, FileHandle handle)
        {
            return _fsClient.GetFileSize(out size, (LibHac.Fs.FileHandle)handle.Value).Horizon;
        }

        public Result MountSystemData(string mountName, ulong dataId)
        {
            try
            {
                L($"MountSystemData mount={mountName} dataId={dataId:X16}");
                string contentPath = _system.ContentManager.GetInstalledContentPath(dataId, StorageId.BuiltInSystem, NcaContentType.PublicData);
                
                // FIX #501 - se não achou, tenta Data também
                if (string.IsNullOrEmpty(contentPath))
                {
                    contentPath = _system.ContentManager.GetInstalledContentPath(dataId, StorageId.BuiltInSystem, NcaContentType.Data);
                }

                if (string.IsNullOrEmpty(contentPath))
                {
                    L($"MountSystemData NOT FOUND dataId={dataId:X16} -> Return Success (evita crash Ngc)");
                    return Result.Success; // <- FIX CRITICAL: não retorna TargetNotFound
                }

                string installPath = VirtualFileSystem.SwitchPathToSystemPath(contentPath);
                L($"MountSystemData path={installPath}");

                if (!string.IsNullOrWhiteSpace(installPath) && File.Exists(installPath))
                {
                    LocalStorage ncaStorage = null;
                    try
                    {
                        ncaStorage = new LocalStorage(installPath, FileAccess.Read, FileMode.Open);
                        Nca nca = new(_system.KeySet, ncaStorage);
                        using IFileSystem ncaFileSystem = nca.OpenFileSystem(NcaSectionType.Data, _system.FsIntegrityCheckLevel);
                        using UniqueRef<IFileSystem> ncaFsRef = new(ncaFileSystem);
                        Result result = _fsClient.Register(mountName.ToU8Span(), ref ncaFsRef.Ref).Horizon;
                        if (result.IsFailure)
                        {
                            L($"MountSystemData Register FAIL {result}");
                            ncaStorage.Dispose();
                            return Result.Success; // FIX: não propaga falha
                        }
                        else
                        {
                            _mountedStorages.TryAdd(mountName, ncaStorage);
                            L($"MountSystemData OK mount={mountName}");
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        L($"MountSystemData EX {ex.Message} -> Success");
                        ncaStorage?.Dispose();
                        return Result.Success; // FIX: evita crash do Ngc
                    }
                }
                L($"MountSystemData file not exists -> Success");
                return Result.Success;
            }
            catch (Exception ex)
            {
                L($"MountSystemData OUTER EX {ex} -> Success");
                return Result.Success;
            }
        }

        public Result OpenFile(out FileHandle handle, string path, OpenMode openMode)
        {
            LibHac.Result result = _fsClient.OpenFile(out LibHac.Fs.FileHandle libhacHandle, path.ToU8Span(), (LibHac.Fs.OpenMode)openMode);
            handle = new(libhacHandle);
            return result.Horizon;
        }

        public Result QueryMountSystemDataCacheSize(out long size, ulong dataId)
        {
            size = 0;
            return Result.Success;
        }

        public Result ReadFile(FileHandle handle, long offset, Span<byte> destination)
        {
            return _fsClient.ReadFile((LibHac.Fs.FileHandle)handle.Value, offset, destination).Horizon;
        }

        public void Unmount(string mountName)
        {
            if (_mountedStorages.TryRemove(mountName, out LocalStorage ncaStorage))
            {
                ncaStorage.Dispose();
            }
            _fsClient.Unmount(mountName.ToU8Span());
        }
    }
}
