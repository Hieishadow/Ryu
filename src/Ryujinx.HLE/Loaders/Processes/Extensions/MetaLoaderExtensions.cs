using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Loader;
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
                t.GetField("_mainThreadPriority", flags)?.SetValue(metaLoader, (uint)44);
            }
            catch { }
        }

        public static void LoadFromFile(this MetaLoader metaLoader, IFileSystem fileSystem)
        {
            try
            {
                if (fileSystem.FileExists(ProcessConst.MainNpdmPath))
                {
                    using UniqueRef<IFile> npdmFile = new();
                    fileSystem.OpenFile(ref npdmFile.Ref, ProcessConst.MainNpdmPath, OpenMode.Read).ThrowIfFailure();
                    var storage = npdmFile.Get.AsStorage();
                    metaLoader.Load(storage);
                    return;
                }
            }
            catch { }
            
            metaLoader.LoadDefault();
        }
    }
}
