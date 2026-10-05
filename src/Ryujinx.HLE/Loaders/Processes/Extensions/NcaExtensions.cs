using LibHac;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Loader;
using LibHac.Ncm;
using LibHac.Ns;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using LibHac.Tools.Ncm;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.Utilities;
using System.IO;
using System.Linq;
using ApplicationId = LibHac.Ncm.ApplicationId;
using ContentType = LibHac.Ncm.ContentType;
using Path = System.IO.Path;

namespace Ryujinx.HLE.Loaders.Processes.Extensions
{
    public static class NcaExtensions
    {
        private static readonly TitleUpdateMetadataJsonSerializerContext _applicationSerializerContext = new(JsonHelper.GetDefaultSerializerOptions());

        public static ProcessResult Load(this Nca nca, Switch device, Nca patchNca, Nca controlNca, BlitStruct<ApplicationControlProperty>? customNacpData = null)
        {
            IStorage romFs = nca.GetRomFs(device, patchNca);
            IFileSystem exeFs = nca.GetExeFs(device, patchNca);

            if (exeFs == null)
            {
                Logger.Error?.Print(LogClass.Loader, "No ExeFS found in NCA");
                return ProcessResult.Failed;
            }

            MetaLoader metaLoader = exeFs.GetNpdm();

            device.Configuration.VirtualFileSystem.ModLoader.CollectMods(
                device.Configuration.ContentManager.GetAocTitleIds().Prepend(metaLoader.ProgramId),
                ModLoader.GetModsBasePath(),
                ModLoader.GetSdModsBasePath());

            BlitStruct<ApplicationControlProperty> nacpData = new(1);

            if (controlNca != null)
            {
                nacpData = controlNca.GetNacp(device);
            }
            else if (customNacpData != null)
            {
                nacpData = (BlitStruct<ApplicationControlProperty>)customNacpData;
            }

            ProcessResult processResult = exeFs.Load(device, nacpData, metaLoader, (byte)nca.ProgramIndex);

            if (romFs == null)
            {
                Logger.Warning?.Print(LogClass.Loader, "No RomFS found in NCA");
            }
            else
            {
                romFs = device.Configuration.VirtualFileSystem.ModLoader.ApplyRomFsMods(processResult.ProgramId, romFs);
                device.Configuration.VirtualFileSystem.SetRomFs(processResult.ProcessId, romFs.AsStream(FileAccess.Read));
            }

            if (processResult.ProgramId != 0 && (processResult.ProgramId < SystemProgramId.Start.Value || processResult.ProgramId > SystemAppletId.End.Value))
            {
                ProcessLoaderHelper.EnsureSaveData(device, new ApplicationId(processResult.ProgramId & ~0xFul), nacpData);
            }

            return processResult;
        }

        public static (Nca, Nca) GetUpdateData(this Nca mainNca, VirtualFileSystem fileSystem, IntegrityCheckLevel checkLevel, int programIndex, out string updatePath)
        {
            updatePath = null;
            Nca updatePatchNca = null;
            Nca updateControlNca = null;

            ulong titleIdBase = mainNca.ProgramIdBase;

            string titleUpdateMetadataPath = Path.Combine(AppDataManager.GamesDirPath, titleIdBase.ToString("x16"), "updates.json");
            if (File.Exists(titleUpdateMetadataPath))
            {
                updatePath = JsonHelper.DeserializeFromFile(titleUpdateMetadataPath, _applicationSerializerContext.TitleUpdateMetadata).Selected;
                if (File.Exists(updatePath))
                {
                    IFileSystem updatePartitionFileSystem = PartitionFileSystemUtils.OpenApplicationFileSystem(updatePath, fileSystem);

                    foreach ((ulong applicationTitleId, ContentMetaData content) in updatePartitionFileSystem.GetContentData(ContentMetaType.Patch, fileSystem, checkLevel))
                    {
                        if ((applicationTitleId & ~0x1FFFUL) != titleIdBase)
                        {
                            continue;
                        }

                        updatePatchNca = content.GetNcaByType(fileSystem.KeySet, ContentType.Program, programIndex);
                        updateControlNca = content.GetNcaByType(fileSystem.KeySet, ContentType.Control, programIndex);
                        break;
                    }
                }
            }

            return (updatePatchNca, updateControlNca);
        }

        public static IFileSystem GetExeFs(this Nca nca, Switch device, Nca patchNca = null)
        {
            IFileSystem exeFs = null;
            if (patchNca == null)
            {
                if (nca.CanOpenSection(NcaSectionType.Code))
                {
                    exeFs = nca.OpenFileSystem(NcaSectionType.Code, device.System.FsIntegrityCheckLevel);
                }
            }
            else
            {
                if (patchNca.CanOpenSection(NcaSectionType.Code))
                {
                    exeFs = nca.OpenFileSystemWithPatch(patchNca, NcaSectionType.Code, device.System.FsIntegrityCheckLevel);
                }
            }
            return exeFs;
        }

        public static IStorage GetRomFs(this Nca nca, Switch device, Nca patchNca = null)
        {
            IStorage romFs = null;
            if (patchNca == null)
            {
                if (nca.CanOpenSection(NcaSectionType.Data))
                {
                    romFs = nca.OpenStorage(NcaSectionType.Data, device.System.FsIntegrityCheckLevel);
                }
            }
            else
            {
                if (patchNca.CanOpenSection(NcaSectionType.Data))
                {
                    romFs = nca.OpenStorageWithPatch(patchNca, NcaSectionType.Data, device.System.FsIntegrityCheckLevel);
                }
            }
            return romFs;
        }

        public static BlitStruct<ApplicationControlProperty> GetNacp(this Nca controlNca, Switch device)
        {
            BlitStruct<ApplicationControlProperty> nacpData = new(1);
            using UniqueRef<IFile> controlFile = new();
            Result result = controlNca.OpenFileSystem(NcaSectionType.Data, device.System.FsIntegrityCheckLevel)
                                      .OpenFile(ref controlFile.Ref, "/control.nacp".ToU8Span(), OpenMode.Read);
            if (result.IsSuccess())
            {
                result = controlFile.Get.Read(out long bytesRead, 0, nacpData.ByteSpan, ReadOption.None);
            }
            else
            {
                nacpData.ByteSpan.Clear();
            }
            return nacpData;
        }

        public static Cnmt GetCnmt(this Nca cnmtNca, IntegrityCheckLevel checkLevel, ContentMetaType metaType)
        {
            string path = $"/{metaType}_{cnmtNca.Header.TitleId:x16}.cnmt";
            using UniqueRef<IFile> cnmtFile = new();
            try
            {
                Result result = cnmtNca.OpenFileSystem(0, checkLevel)
                                       .OpenFile(ref cnmtFile.Ref, path.ToU8Span(), OpenMode.Read);
                if (result.IsSuccess())
                {
                    return new Cnmt(cnmtFile.Release().AsStream());
                }
            }
            catch (HorizonResultException ex)
            {
                if (!ResultFs.PathNotFound.Includes(ex.ResultValue))
                {
                    Logger.Warning?.Print(LogClass.Application, $"Failed get CNMT for '{cnmtNca.Header.TitleId:x16}' from NCA: {ex.Message}");
                }
            }
            return null;
        }
    }
}
