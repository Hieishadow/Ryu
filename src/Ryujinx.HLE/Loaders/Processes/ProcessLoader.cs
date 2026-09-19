using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ns;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Gpu;
using Ryujinx.HLE.HOS.SystemState;
using Ryujinx.HLE.Loaders.Executables;
using Ryujinx.HLE.Loaders.Processes.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;

namespace Ryujinx.HLE.Loaders.Processes
{
    public class ProcessLoader
    {
        private readonly Switch _device;
        private readonly ConcurrentDictionary<ulong, ProcessResult> _processesByPid;
        private ulong _latestPid;
        private readonly object _pidLock = new();

#nullable enable
        public ProcessResult? ActiveApplication
        {
            get
            {
                lock (_pidLock)
                {
                    if (_latestPid == 0) return null;
                    if (!_device.System.KernelContext.Processes.TryGetValue(_latestPid, out HOS.Kernel.Process.KProcess? kernelProcess))
                    {
                        Logger.Warning?.Print(LogClass.Loader, $"ActiveApplication PID {_latestPid} no longer exists in kernel, clearing stale state");
                        _processesByPid.TryRemove(_latestPid, out _);
                        _latestPid = 0;
                        TitleIDs.CurrentApplication.Value = null;
                        return null;
                    }
                    if (_processesByPid.TryGetValue(_latestPid, out ProcessResult? processResult))
                    {
                        if (kernelProcess.State == HOS.Kernel.Process.ProcessState.Exited ||
                            kernelProcess.State == HOS.Kernel.Process.ProcessState.Exiting)
                        {
                            Logger.Warning?.Print(LogClass.Loader, $"ActiveApplication PID {_latestPid} is in state {kernelProcess.State}, clearing");
                            _processesByPid.TryRemove(_latestPid, out _);
                            _latestPid = 0;
                            TitleIDs.CurrentApplication.Value = null;
                            return null;
                        }
                        return processResult;
                    }
                    Logger.Warning?.Print(LogClass.Loader, $"ActiveApplication PID {_latestPid} not in ProcessLoader dictionary, clearing");
                    _latestPid = 0;
                    return null;
                }
            }
        }
#nullable disable

        public ProcessLoader(Switch device)
        {
            _device = device;
            _processesByPid = new ConcurrentDictionary<ulong, ProcessResult>();
        }

        public bool TryGetProcess(ulong pid, out ProcessResult process) => _processesByPid.TryGetValue(pid, out process);

        public ProcessResult GetProcess(ulong pid)
        {
            if (_processesByPid.TryGetValue(pid, out ProcessResult process)) return process;
            Logger.Warning?.Print(LogClass.Loader, $"Process metadata for pid {pid} was not found. Falling back to active application metadata.");
            return ActiveApplication;
        }

        public bool LoadXci(string path, ulong applicationId)
        {
            FileStream stream = new(path, FileMode.Open, FileAccess.Read);
            Xci xci = new(_device.Configuration.VirtualFileSystem.KeySet, stream.AsStorage());
            if (!xci.HasPartition(XciPartitionType.Secure))
            {
                Logger.Error?.Print(LogClass.Loader, "Unable to load XCI: Could not find XCI Secure partition");
                return false;
            }
            (bool success, ProcessResult processResult) = xci.OpenPartition(XciPartitionType.Secure).TryLoad(_device, path, applicationId, out string errorMessage);
            if (!success)
            {
                Logger.Error?.Print(LogClass.Loader, errorMessage, nameof(PartitionFileSystemExtensions.TryLoad));
                return false;
            }
            if (processResult.ProcessId!= 0 && _processesByPid.TryAdd(processResult.ProcessId, processResult))
            {
                if (processResult.Start(_device))
                {
                    _latestPid = processResult.ProcessId;
                    TitleIDs.CurrentApplication.Value = processResult.ProgramIdText;
                    return true;
                }
            }
            return false;
        }

        public bool LoadNsp(string path, ulong applicationId)
        {
            try
            {
                Logger.Info?.Print(LogClass.Loader, $"Loading NSP: {path} AppId: {applicationId:X16}");
                FileStream file = new(path, FileMode.Open, FileAccess.Read);
                PartitionFileSystem partitionFileSystem = new();
                Result res = partitionFileSystem.Initialize(file.AsStorage());
                if (res.IsFailure())
                {
                    Logger.Error?.Print(LogClass.Loader, $"NSP Initialize failed {res} - check prod.keys! Path: {path}");
                    file.Dispose();
                    return false;
                }
                (bool success, ProcessResult processResult) = partitionFileSystem.TryLoad(_device, path, applicationId, out string errorMessage);
                if (processResult.ProcessId == 0)
                {
                    Logger.Warning?.Print(LogClass.Loader, "NSP is ExeFS, loading as homebrew");
                    try
                    {
                        processResult = partitionFileSystem.Load(_device, new BlitStruct<ApplicationControlProperty>(1), partitionFileSystem.GetNpdm(), 0, true);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error?.Print(LogClass.Loader, $"ExeFS load fail: {ex}");
                    }
                }
                if (processResult.ProcessId!= 0 && _processesByPid.TryAdd(processResult.ProcessId, processResult))
                {
                    if (processResult.Start(_device))
                    {
                        _latestPid = processResult.ProcessId;
                        TitleIDs.CurrentApplication.Value = processResult.ProgramIdText;
                        Logger.Info?.Print(LogClass.Loader, $"NSP loaded PID:{_latestPid} Title:{processResult.ProgramIdText}");
                        return true;
                    }
                }
                if (!success)
                {
                    Logger.Error?.Print(LogClass.Loader, errorMessage, nameof(PartitionFileSystemExtensions.TryLoad));
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error?.Print(LogClass.Loader, $"LoadNsp exception for {path}: {ex}");
                return false;
            }
        }

        public bool LoadNca(string path, BlitStruct<ApplicationControlProperty>? customNacpData = null)
        {
            FileStream file = new(path, FileMode.Open, FileAccess.Read);
            Nca nca = new(_device.Configuration.VirtualFileSystem.KeySet, file.AsStorage(false));
            ProcessResult processResult = nca.Load(_device, null, null, customNacpData);
            if (processResult.ProcessId!= 0 && _processesByPid.TryAdd(processResult.ProcessId, processResult))
            {
                if (processResult.Start(_device))
                {
                    if (processResult.ProgramId > 0x01000000000007FF)
                    {
                        _latestPid = processResult.ProcessId;
                        TitleIDs.CurrentApplication.Value = processResult.ProgramIdText;
                    }
                    return true;
                }
            }
            return false;
        }

        public bool LoadUnpackedNca(string exeFsDirPath, string romFsPath = null)
        {
            ProcessResult processResult = new LocalFileSystem(exeFsDirPath).Load(_device, romFsPath);
            if (processResult.ProcessId!= 0 && _processesByPid.TryAdd(processResult.ProcessId, processResult))
            {
                if (processResult.Start(_device))
                {
                    _latestPid = processResult.ProcessId;
                    TitleIDs.CurrentApplication.Value = processResult.ProgramIdText;
                    return true;
                }
            }
            return false;
        }

        public bool LoadNxo(string path)
        {
            BlitStruct<ApplicationControlProperty> nacpData = new(1);
            IFileSystem dummyExeFs = null;
            Stream romfsStream = null;
            string programName = string.Empty;
            ulong programId = 0000000000000000;
            IExecutable executable;
            if (Path.GetExtension(path).Equals(".nro", StringComparison.OrdinalIgnoreCase))
            {
                FileStream input = new(path, FileMode.Open);
                NroExecutable nro = new(input.AsStorage());
                executable = nro;
                IStorage romFsStorage = nro.OpenNroAssetSection(LibHac.Tools.Ro.NroAssetType.RomFs, false);
                romFsStorage.GetSize(out long romFsSize).ThrowIfFailure();
                if (romFsSize!= 0) romfsStream = romFsStorage.AsStream();
                IStorage nacpStorage = nro.OpenNroAssetSection(LibHac.Tools.Ro.NroAssetType.Nacp, false);
                nacpStorage.GetSize(out long nacpSize).ThrowIfFailure();
                if (nacpSize!= 0)
                {
                    nacpStorage.Read(0, nacpData.ByteSpan);
                    programName = nacpData.Value.Title[(int)_device.System.State.DesiredTitleLanguage].NameString.ToString();
                    if ("Switch Verification" == nacpData.Value.Title[(int)TitleLanguage.AmericanEnglish].NameString.ToString()) throw new InvalidOperationException();
                    if (string.IsNullOrWhiteSpace(programName))
                    {
                        foreach (ApplicationControlProperty.ApplicationTitle nacpTitles in nacpData.Value.Title)
                        {
                            if (nacpTitles.Name[0]!= 0) continue;
                            programName = nacpTitles.NameString.ToString();
                        }
                    }
                    if (nacpData.Value.PresenceGroupId!= 0) { programId = nacpData.Value.PresenceGroupId; TitleIDs.CurrentApplication.Value = programId.ToString("X16"); }
                    else if (nacpData.Value.SaveDataOwnerId!= 0) { programId = nacpData.Value.SaveDataOwnerId; TitleIDs.CurrentApplication.Value = programId.ToString("X16"); }
                    else if (nacpData.Value.AddOnContentBaseId!= 0) { programId = nacpData.Value.AddOnContentBaseId - 0x1000; TitleIDs.CurrentApplication.Value = programId.ToString("X16"); }
                }
            }
            else
            {
                programName = Path.GetFileNameWithoutExtension(path);
                executable = new NsoExecutable(new LocalStorage(path, FileAccess.Read), programName);
            }
            GraphicsConfig.TitleId = null;
            _device.Gpu.HostInitalized.Set();
            ProcessResult processResult = ProcessLoaderHelper.LoadNsos(_device, _device.System.KernelContext, dummyExeFs.GetNpdm(), nacpData, diskCacheEnabled: false, diskCacheSelector: null, allowCodeMemoryForJit: true, programName, programId, 0, null, executable);
            if (processResult.ProcessId!= 0)
            {
                if (romfsStream!= null) _device.Configuration.VirtualFileSystem.SetRomFs(processResult.ProcessId, romfsStream);
                if (_processesByPid.TryAdd(processResult.ProcessId, processResult))
                {
                    if (processResult.Start(_device))
                    {
                        _latestPid = processResult.ProcessId;
                        return true;
                    }
                }
            }
            return false;
        }

        public void ClearProcess(ulong pid)
        {
            lock (_pidLock)
            {
                if (_processesByPid.TryRemove(pid, out _))
                {
                    if (_latestPid == pid)
                    {
                        _latestPid = 0;
                        TitleIDs.CurrentApplication.Value = null;
                    }
                }
            }
        }

        public void ClearAllProcesses()
        {
            lock (_pidLock)
            {
                _processesByPid.Clear();
                _latestPid = 0;
                TitleIDs.CurrentApplication.Value = null;
            }
        }
    }
}
