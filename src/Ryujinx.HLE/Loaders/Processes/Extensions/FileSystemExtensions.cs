using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Loader;
using LibHac.Ns;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Gpu;
using Ryujinx.HLE.Loaders.Executables;
using Ryujinx.Memory;
using System;
using System.Linq;
using static Ryujinx.HLE.HOS.ModLoader;

namespace Ryujinx.HLE.Loaders.Processes.Extensions
{
    static class FileSystemExtensions
    {
        public static MetaLoader GetNpdm(this IFileSystem fileSystem)
        {
            MetaLoader metaLoader = new();

            try
            {
                if (fileSystem == null ||!fileSystem.FileExists(ProcessConst.MainNpdmPath))
                {
                    Logger.Warning?.Print(LogClass.Loader, "NPDM file not found, using default values! (Android fallback)");
                    metaLoader.LoadDefault();
                }
                else
                {
                    metaLoader.LoadFromFile(fileSystem);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning?.Print(LogClass.Loader, $"GetNpdm fallback: {ex.Message}, using LoadDefault");
                try { metaLoader.LoadDefault(); } catch { }
            }

            return metaLoader;
        }

        public static ProcessResult Load(this IFileSystem exeFs, Switch device, BlitStruct<ApplicationControlProperty> nacpData, MetaLoader metaLoader, byte programIndex, bool isHomebrew = false)
        {
            ulong programId = metaLoader.ProgramId;
            if (device.Configuration.VirtualFileSystem.ModLoader.ReplaceExefsPartition(programId, ref exeFs))
            {
                metaLoader = null;
            }
            metaLoader??= exeFs.GetNpdm();
            NsoExecutable[] nsoExecutables = new NsoExecutable[ProcessConst.ExeFsPrefixes.Length];
            for (int i = 0; i < nsoExecutables.Length; i++)
            {
                string name = ProcessConst.ExeFsPrefixes[i];
                if (!exeFs.FileExists($"/{name}".ToU8Span())) continue;
                Logger.Info?.Print(LogClass.Loader, $"Loading {name}...");
                using UniqueRef<IFile> nsoFile = new();
                exeFs.OpenFile(ref nsoFile.Ref, $"/{name}".ToU8Span(), OpenMode.Read).ThrowIfFailure();
                nsoExecutables[i] = new NsoExecutable(nsoFile.Release().AsStorage(), name);
            }
            ModLoadResult modLoadResult = device.Configuration.VirtualFileSystem.ModLoader.ApplyExefsMods(programId, nsoExecutables);
            if (modLoadResult.Npdm!= null) metaLoader = modLoadResult.Npdm;
            nsoExecutables = nsoExecutables.Where(x => x!= null).ToArray();
            device.Configuration.VirtualFileSystem.ModLoader.ApplyNsoPatches(programId, nsoExecutables);
            string programName = string.Empty;
            if (!isHomebrew && programId > 0x010000000000FFFF)
            {
                programName = nacpData.Value.Title[(int)device.System.State.DesiredTitleLanguage].NameString.ToString();
                if (string.IsNullOrWhiteSpace(programName))
                {
                    foreach (ApplicationControlProperty.ApplicationTitle appTitle in nacpData.Value.Title)
                    {
                        if (appTitle.Name[0]!= 0) continue;
                        programName = appTitle.NameString.ToString();
                    }
                }
            }
            GraphicsConfig.TitleId = programId.ToString("X16");
            device.Gpu.HostInitalized.Set();
            if (!MemoryBlock.SupportsFlags(MemoryAllocationFlags.ViewCompatible))
            {
                device.Configuration.MemoryManagerMode = MemoryManagerMode.SoftwarePageTable;
            }
            ProcessResult processResult = ProcessLoaderHelper.LoadNsos(
                device, device.System.KernelContext, metaLoader, nacpData,
                device.System.EnablePtc, modLoadResult.Hash, true,
                programName, programId, programIndex, null, nsoExecutables);
            device.System.LibHacHorizonManager.ArpIReader.ApplicationId = new LibHac.ApplicationId(programId);
            return processResult;
        }
    }
}
