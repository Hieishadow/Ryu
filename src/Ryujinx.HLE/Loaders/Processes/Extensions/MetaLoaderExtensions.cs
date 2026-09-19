using System;
using System.IO;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.Loaders.Processes;
using LibHac.Common;

namespace Ryujinx.HLE.Loaders.Processes.Extensions
{
    public static class MetaLoaderExtensions
    {
        public static void LoadDefault(this MetaLoader metaLoader)
        {
            byte[] npdmBuffer = null;

            try
            {
                npdmBuffer = Ryujinx.Common.EmbeddedResources.Read("Ryujinx.HLE/Homebrew.npdm");
            }
            catch (Exception ex)
            {
                Logger.Warning?.Print(LogClass.Loader, $"Embedded Homebrew.npdm fail: {ex.Message}");
            }

            if (npdmBuffer!= null && npdmBuffer.Length > 0)
            {
                metaLoader.Load(npdmBuffer).ThrowIfFailure();
                return;
            }

            // FALLBACK ANDROID: tenta carregar do disco
            string[] paths = new[]
            {
                "/storage/emulated/0/Download/Ryubing/Homebrew.npdm",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Ryujinx/Homebrew.npdm")
            };

            foreach (var p in paths)
            {
                if (File.Exists(p))
                {
                    try
                    {
                        var buf = File.ReadAllBytes(p);
                        if (buf.Length > 0)
                        {
                            Logger.Info?.Print(LogClass.Loader, $"Loading Homebrew.npdm from {p} ({buf.Length} bytes)");
                            metaLoader.Load(buf).ThrowIfFailure();
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning?.Print(LogClass.Loader, $"Fallback Homebrew.npdm fail {p}: {ex.Message}");
                    }
                }
            }

            // Se chegou aqui, nenhum Homebrew.npdm existe - dummy pra não dar 2009-0004
            Logger.Warning?.Print(LogClass.Loader, "Homebrew.npdm not found, using minimal dummy NPDM");
            var dummy = new byte[0x1000];
            metaLoader.Load(dummy).ThrowIfFailure();
        }
    }
}
