using Ryujinx.Common.Logging;
using System;
using System.IO;

namespace Ryujinx.HLE.HOS.Services.Vi.RootService.ApplicationDisplayService
{
    class ISystemDisplayService : IpcService
    {
        private readonly IApplicationDisplayService _applicationDisplayService;

        public ISystemDisplayService(IApplicationDisplayService applicationDisplayService)
        {
            _applicationDisplayService = applicationDisplayService;
        }

        [CommandCmif(2205)]
        // SetLayerZ(u64, u64)
        public ResultCode SetLayerZ(ServiceCtx context)
        {
            long z = context.RequestData.ReadInt64();
            long layerId = context.RequestData.ReadInt64();
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] ISystem SetLayerZ layerId={layerId} z={z}\n"); } catch {}
            Logger.Stub?.PrintStub(LogClass.ServiceVi);
            return ResultCode.Success;
        }

        [CommandCmif(2207)]
        // SetLayerVisibility(b8, u64)
        public ResultCode SetLayerVisibility(ServiceCtx context)
        {
            bool visible = context.RequestData.ReadBoolean();
            long layerId = context.RequestData.ReadInt64();
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] ISystem SetLayerVisibility layerId={layerId} visible={visible}\n"); } catch {}
            Logger.Stub?.PrintStub(LogClass.ServiceVi);
            return ResultCode.Success;
        }

        [CommandCmif(2312)] // 1.0.0-6.2.0
        // CreateStrayLayer(u32, u64) -> (u64, u64, buffer<bytes, 6>)
        public ResultCode CreateStrayLayer(ServiceCtx context)
        {
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] ISystem CreateStrayLayer ENTER -> forward\n"); } catch {}
            Logger.Stub?.PrintStub(LogClass.ServiceVi);
            return _applicationDisplayService.CreateStrayLayer(context);
        }

        [CommandCmif(3200)]
        // GetDisplayMode(u64) -> nn::vi::DisplayModeInfo
        public ResultCode GetDisplayMode(ServiceCtx context)
        {
            ulong displayId = context.RequestData.ReadUInt64();
            (ulong width, ulong height) = AndroidSurfaceComposerClient.GetDisplayInfo(context, displayId);
            context.ResponseData.Write((uint)width);
            context.ResponseData.Write((uint)height);
            context.ResponseData.Write(60.0f);
            context.ResponseData.Write(0);
            Logger.Stub?.PrintStub(LogClass.ServiceVi);
            return ResultCode.Success;
        }
    }
}
