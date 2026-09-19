using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Loader;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using System.Reflection;

namespace Ryujinx.HLE.Loaders.Processes.Extensions
{
    static class MetaLoaderExtensions
    {
        public static void LoadDefault(this MetaLoader metaLoader)
        {
            try
            {
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var t = typeof(MetaLoader);
                t.GetField("_isLoaded", flags)?.SetValue(metaLoader, true);
                t.GetField("_programId", flags)?.SetValue(metaLoader, (ulong)0x0100000000000000);
                t.GetField("_is64Bit", flags)?.SetValue(metaLoader, true);
                t.GetField("_mainThreadStackSize", flags)?.SetValue(metaLoader, (ulong)0x100000);
            }
            catch { }
        }

        public static void LoadFromFile(this MetaLoader metaLoader, IFileSystem fileSystem)
        {
            try
            {
                using UniqueRef<IFile> npdmFile = new();
                fileSystem.OpenFile(ref npdmFile.Ref, ProcessConst.MainNpdmPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();
                metaLoader.Load(npdmFile.Get.AsStorage());
            }
            catch
            {
                metaLoader.LoadDefault();
            }
        }
    }
}
