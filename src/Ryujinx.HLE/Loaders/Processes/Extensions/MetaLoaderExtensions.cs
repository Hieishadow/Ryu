using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Loader;
using LibHac.Tools.FsSystem;
using System;
using System.Reflection;

namespace Ryujinx.HLE.Loaders.Processes.Extensions
{
    static class MetaLoaderExtensions
    {
        public static void LoadDefault(this MetaLoader metaLoader)
        {
            try
            {
                BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var type = typeof(MetaLoader);

                // Marca como carregado pra não crashar o 2009-0004
                type.GetField("_isLoaded", flags)?.SetValue(metaLoader, true);
                type.GetField("_programId", flags)?.SetValue(metaLoader, (ulong)0x0100000000000000);
                type.GetField("_is64Bit", flags)?.SetValue(metaLoader, true);

                // Cria campos minimos se existirem
                try { type.GetField("_acidPublicKey", flags)?.SetValue(metaLoader, new byte[0x100]); } catch {}
                try { type.GetField("_mainThreadStackSize", flags)?.SetValue(metaLoader, (ulong)0x100000); } catch {}
            }
            catch { }
        }

        public static void LoadFromFile(this MetaLoader metaLoader, IFileSystem fileSystem)
        {
            try
            {
                if (fileSystem.FileExists("/main.npdm".ToU8Span()))
                {
                    using UniqueRef<IFile> npdmFile = new();
                    fileSystem.OpenFile(ref npdmFile.Ref, "/main.npdm".ToU8Span(), OpenMode.Read).ThrowIfFailure();
                    IStorage storage = npdmFile.Get.AsStorage();
                    metaLoader.Load(storage);
                }
                else
                {
                    metaLoader.LoadDefault();
                }
            }
            catch
            {
                metaLoader.LoadDefault();
            }
        }
    }
}
