using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Loader;
using System.Reflection;

namespace Ryujinx.HLE.Loaders.Processes.Extensions
{
    static class MetaLoaderExtensions
    {
        // FIX 2009-0004 - Zelda 5984MB sem main.npdm
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
                t.GetField("_is32Bit", flags)?.SetValue(metaLoader, false);
            }
            catch { }
        }

        public static void LoadFromFile(this MetaLoader metaLoader, IFileSystem fileSystem)
        {
            try
            {
                using UniqueRef<IFile> npdmFile = new();
                fileSystem.OpenFile(ref npdmFile.Ref, "/main.npdm".ToU8Span(), OpenMode.Read).ThrowIfFailure();

                npdmFile.Get.GetSize(out long size);
                byte[] data = new byte[size];
                npdmFile.Get.Read(out _, 0, data, ReadOption.None);

                // Agora Load(byte[]) - não mais Load(IStorage)
                metaLoader.Load(data);
            }
            catch
            {
                metaLoader.LoadDefault();
            }
        }
    }
}
