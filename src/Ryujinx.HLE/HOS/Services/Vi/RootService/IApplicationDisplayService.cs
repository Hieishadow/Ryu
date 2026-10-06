using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using Ryujinx.HLE.HOS.Applets;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Services.SurfaceFlinger;
using Ryujinx.HLE.HOS.Services.Vi.RootService.ApplicationDisplayService;
using Ryujinx.HLE.HOS.Services.Vi.RootService.ApplicationDisplayService.Types;
using Ryujinx.HLE.HOS.Services.Vi.Types;
using Ryujinx.HLE.UI;
using Ryujinx.Horizon.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Ryujinx.HLE.HOS.Services.Vi.RootService
{
    class IApplicationDisplayService : IpcService
    {
        private readonly ViServiceType _serviceType;

        private class DisplayState
        {
            public int RetrievedEventsCount;
        }

        private readonly List<DisplayInfo> _displayInfo;
        private readonly Dictionary<ulong, DisplayState> _openDisplays;
        private int _vsyncEventHandle;

        public IApplicationDisplayService(ViServiceType serviceType)
        {
            _serviceType = serviceType;
            _displayInfo = [];
            _openDisplays = new Dictionary<ulong, DisplayState>();

            void AddDisplayInfo(string name, bool layerLimitEnabled, ulong layerLimitMax, ulong width, ulong height)
            {
                DisplayInfo displayInfo = new()
                {
                    Name = new Array64<byte>(),
                    LayerLimitEnabled = layerLimitEnabled,
                    Padding = new Array7<byte>(),
                    LayerLimitMax = layerLimitMax,
                    Width = width,
                    Height = height,
                };
                Encoding.ASCII.GetBytes(name).AsSpan().CopyTo(displayInfo.Name.AsSpan());
                _displayInfo.Add(displayInfo);
            }

            AddDisplayInfo("Default", true, 1, 1920, 1080);
            AddDisplayInfo("External", true, 1, 1920, 1080);
            AddDisplayInfo("Edid", true, 1, 0, 0);
            AddDisplayInfo("Internal", true, 1, 1920, 1080);
            AddDisplayInfo("Null", false, 0, 1920, 1080);
        }

        [CommandCmif(100)]
        public ResultCode GetRelayService(ServiceCtx context)
        {
            if (_serviceType > ViServiceType.System) return ResultCode.PermissionDenied;
            MakeObject(context, new HOSBinderDriverServer());
            return ResultCode.Success;
        }

        [CommandCmif(101)]
        public ResultCode GetSystemDisplayService(ServiceCtx context)
        {
            if (_serviceType > ViServiceType.System) return ResultCode.PermissionDenied;
            MakeObject(context, new ISystemDisplayService(this));
            return ResultCode.Success;
        }

        [CommandCmif(102)]
        public ResultCode GetManagerDisplayService(ServiceCtx context)
        {
            if (_serviceType > ViServiceType.System) return ResultCode.PermissionDenied;
            MakeObject(context, new IManagerDisplayService(this));
            return ResultCode.Success;
        }

        [CommandCmif(103)]
        public ResultCode GetIndirectDisplayTransactionService(ServiceCtx context)
        {
            if (_serviceType > ViServiceType.System) return ResultCode.PermissionDenied;
            MakeObject(context, new HOSBinderDriverServer());
            return ResultCode.Success;
        }

        [CommandCmif(1000)]
        public ResultCode ListDisplays(ServiceCtx context)
        {
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] IAppDisp ListDisplays\n"); } catch {}
            ulong displayInfoBuffer = context.Request.ReceiveBuff[0].Position;
            ulong displayCount = 1;
            for (int i = 0; i < (int)displayCount; i++)
            {
                context.Memory.Write(displayInfoBuffer + (ulong)(i * Unsafe.SizeOf<DisplayInfo>()), _displayInfo[i]);
            }
            context.ResponseData.Write(displayCount);
            return ResultCode.Success;
        }

        [CommandCmif(1010)]
        public ResultCode OpenDisplay(ServiceCtx context)
        {
            StringBuilder nameBuilder = new();
            for (int index = 0; index < 8 && context.RequestData.BaseStream.Position < context.RequestData.BaseStream.Length; index++)
            {
                byte chr = context.RequestData.ReadByte();
                if (chr is >= 0x20 and < 0x7f) nameBuilder.Append((char)chr);
            }
            string name = nameBuilder.ToString();
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] IAppDisp OpenDisplay name={name}\n"); } catch {}
            return OpenDisplayImpl(context, name);
        }

        [CommandCmif(1011)]
        public ResultCode OpenDefaultDisplay(ServiceCtx context)
        {
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] IAppDisp OpenDefaultDisplay\n"); } catch {}
            return OpenDisplayImpl(context, "Default");
        }

        private ResultCode OpenDisplayImpl(ServiceCtx context, string name)
        {
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] IAppDisp OpenDisplayImpl name={name}\n"); } catch {}
            if (name == string.Empty) return ResultCode.InvalidValue;
            int displayId = _displayInfo.FindIndex(display => Encoding.ASCII.GetString(display.Name.AsSpan()).Trim('\0') == name);
            if (displayId == -1)
            {
                try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"[VI] OpenDisplayImpl FAIL not found name={name}\n"); } catch {}
                return ResultCode.InvalidValue;
            }
            if (!_openDisplays.TryAdd((ulong)displayId, new DisplayState()))
            {
                try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"[VI] OpenDisplayImpl AlreadyOpened id={displayId}\n"); } catch {}
                return ResultCode.AlreadyOpened;
            }
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] IAppDisp OpenDisplayImpl OK id={displayId}\n"); } catch {}
            context.ResponseData.Write((ulong)displayId);
            return ResultCode.Success;
        }

        [CommandCmif(1020)]
        public ResultCode CloseDisplay(ServiceCtx context)
        {
            ulong displayId = context.RequestData.ReadUInt64();
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] IAppDisp CloseDisplay id={displayId}\n"); } catch {}
            if (!_openDisplays.Remove(displayId)) return ResultCode.InvalidValue;
            return ResultCode.Success;
        }

        [CommandCmif(1101)]
        public ResultCode SetDisplayEnabled(ServiceCtx context) => ResultCode.Success;

        [CommandCmif(1102)]
        public ResultCode GetDisplayResolution(ServiceCtx context)
        {
            context.ResponseData.Write(1280UL);
            context.ResponseData.Write(720UL);
            return ResultCode.Success;
        }

        [CommandCmif(2020)]
        public ResultCode OpenLayer(ServiceCtx context)
        {
            byte[] displayName = context.RequestData.ReadBytes(0x40);
            long layerId = context.RequestData.ReadInt64();
            long userId = context.RequestData.ReadInt64();
            ulong parcelPtr = context.Request.ReceiveBuff[0].Position;
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] IAppDisp OpenLayer layerId={layerId} userId={userId} pid={context.Request.HandleDesc.PId}\n"); } catch {}

            ResultCode result = context.Device.System.SurfaceFlinger.OpenLayer(context.Request.HandleDesc.PId, layerId, out IBinder producer);
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] IAppDisp OpenLayer result={result} producer={(producer!=null?"OK":"NULL")}\n"); } catch {}

            if (result!= ResultCode.Success) return result;

            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] IAppDisp OpenLayer -> SetRenderLayer id={layerId}\n"); } catch {}
            context.Device.System.SurfaceFlinger.SetRenderLayer(layerId);

            using Parcel parcel = new(0x28, 0x4);
            parcel.WriteObject(producer, "dispdrv\0");
            ReadOnlySpan<byte> parcelData = parcel.Finish();
            context.Memory.Write(parcelPtr, parcelData);
            context.ResponseData.Write((long)parcelData.Length);
            return ResultCode.Success;
        }

        [CommandCmif(2021)]
        public ResultCode CloseLayer(ServiceCtx context)
        {
            long layerId = context.RequestData.ReadInt64();
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] IAppDisp CloseLayer id={layerId}\n"); } catch {}
            return context.Device.System.SurfaceFlinger.CloseLayer(layerId);
        }

        [CommandCmif(2030)]
        public ResultCode CreateStrayLayer(ServiceCtx context)
        {
            long layerFlags = context.RequestData.ReadInt64();
            long displayId = context.RequestData.ReadInt64();
            ulong parcelPtr = context.Request.ReceiveBuff[0].Position;
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] IAppDisp CreateStrayLayer flags={layerFlags:X} displayId={displayId}\n"); } catch {}

            IBinder producer = context.Device.System.SurfaceFlinger.CreateLayer(out long layerId, 0, LayerState.Stray);
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] IAppDisp CreateStrayLayer -> id={layerId} producer={(producer!=null?"OK":"NULL")}\n"); } catch {}

            context.Device.System.SurfaceFlinger.SetRenderLayer(layerId);
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] IAppDisp CreateStrayLayer -> SetRenderLayer id={layerId}\n"); } catch {}

            using Parcel parcel = new(0x28, 0x4);
            parcel.WriteObject(producer, "dispdrv\0");
            ReadOnlySpan<byte> parcelData = parcel.Finish();
            context.Memory.Write(parcelPtr, parcelData);
            context.ResponseData.Write(layerId);
            context.ResponseData.Write((long)parcelData.Length);
            return ResultCode.Success;
        }

        [CommandCmif(2031)]
        public ResultCode DestroyStrayLayer(ServiceCtx context)
        {
            long layerId = context.RequestData.ReadInt64();
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] IAppDisp DestroyStrayLayer id={layerId}\n"); } catch {}
            return context.Device.System.SurfaceFlinger.DestroyStrayLayer(layerId);
        }

        [CommandCmif(2101)]
        public ResultCode SetLayerScalingMode(ServiceCtx context) => ResultCode.Success;

        [CommandCmif(2102)]
        public ResultCode ConvertScalingMode(ServiceCtx context)
        {
            SourceScalingMode scalingMode = (SourceScalingMode)context.RequestData.ReadInt32();
            DestinationScalingMode? convertedScalingMode = scalingMode switch
            {
                SourceScalingMode.None => DestinationScalingMode.None,
                SourceScalingMode.Freeze => DestinationScalingMode.Freeze,
                SourceScalingMode.ScaleAndCrop => DestinationScalingMode.ScaleAndCrop,
                SourceScalingMode.ScaleToWindow => DestinationScalingMode.ScaleToWindow,
                SourceScalingMode.PreserveAspectRatio => DestinationScalingMode.PreserveAspectRatio,
                _ => null,
            };
            if (!convertedScalingMode.HasValue) return ResultCode.InvalidArguments;
            if (scalingMode is not SourceScalingMode.ScaleToWindow and not SourceScalingMode.PreserveAspectRatio) return ResultCode.InvalidScalingMode;
            context.ResponseData.Write((ulong)convertedScalingMode);
            return ResultCode.Success;
        }

        private ulong GetA8B8G8R8LayerSize(int width, int height, out int pitch, out int alignment)
        {
            const int DefaultAlignment = 0x1000;
            const ulong DefaultSize = 0x20000;
            alignment = DefaultAlignment;
            pitch = BitUtils.AlignUp(BitUtils.DivRoundUp(width * 32, 8), 64);
            int memorySize = pitch * BitUtils.AlignUp(height, 64);
            ulong requiredMemorySize = (ulong)BitUtils.AlignUp(memorySize, alignment);
            return (requiredMemorySize + DefaultSize - 1) / DefaultSize * DefaultSize;
        }

        [CommandCmif(2450)]
        public ResultCode GetIndirectLayerImageMap(ServiceCtx context)
        {
            long layerWidth = context.RequestData.ReadInt64();
            long layerHeight = context.RequestData.ReadInt64();
            long layerHandle = context.RequestData.ReadInt64();
            ulong layerBuffPosition = context.Request.ReceiveBuff[0].Position;
            ulong layerBuffSize = context.Request.ReceiveBuff[0].Size;
            ulong size = GetA8B8G8R8LayerSize((int)layerWidth, (int)layerHeight, out int pitch, out _);
            Debug.Assert(layerBuffSize == size);
            RenderingSurfaceInfo surfaceInfo = new(ColorFormat.A8B8G8R8, (uint)layerWidth, (uint)layerHeight, (uint)pitch, (uint)layerBuffSize);
            object appletObject = context.Device.System.AppletState.IndirectLayerHandles.GetData((int)layerHandle);
            if (appletObject == null)
            {
                Logger.Error?.Print(LogClass.ServiceVi, $"Indirect layer handle {layerHandle} does not match any applet");
                return ResultCode.Success;
            }
            Debug.Assert(appletObject is IApplet);
            IApplet applet = appletObject as IApplet;
            if (!applet.DrawTo(surfaceInfo, context.Memory, layerBuffPosition))
            {
                Logger.Warning?.Print(LogClass.ServiceVi, $"Applet did not draw on indirect layer handle {layerHandle}");
                return ResultCode.Success;
            }
            context.ResponseData.Write(layerWidth);
            context.ResponseData.Write(layerHeight);
            return ResultCode.Success;
        }

        [CommandCmif(2460)]
        public ResultCode GetIndirectLayerImageRequiredMemoryInfo(ServiceCtx context)
        {
            int width = (int)context.RequestData.ReadUInt64();
            int height = (int)context.RequestData.ReadUInt64();
            if (height < 0 || width < 0) return ResultCode.InvalidLayerSize;
            else
            {
                ulong size = GetA8B8G8R8LayerSize(width, height, out _, out int alignment);
                context.ResponseData.Write(size);
                context.ResponseData.Write(alignment);
            }
            return ResultCode.Success;
        }

        [CommandCmif(5202)]
        public ResultCode GetDisplayVsyncEvent(ServiceCtx context)
        {
            ulong displayId = context.RequestData.ReadUInt64();
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] IAppDisp GetDisplayVsyncEvent displayId={displayId}\n"); } catch {}
            if (!_openDisplays.TryGetValue(displayId, out DisplayState displayState))
            {
                try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"[VI] GetVsync FAIL InvalidValue id={displayId}\n"); } catch {}
                return ResultCode.InvalidValue;
            }
            if (displayState.RetrievedEventsCount > 0) return ResultCode.PermissionDenied;
            if (_vsyncEventHandle == 0)
            {
                if (context.Process.HandleTable.GenerateHandle(context.Device.System.VsyncEvent.ReadableEvent, out _vsyncEventHandle)!= Result.Success)
                    throw new InvalidOperationException("Out of handles!");
            }
            displayState.RetrievedEventsCount++;
            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_vsyncEventHandle);
            return ResultCode.Success;
        }
    }
}
