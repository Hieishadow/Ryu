using LibHac.Common;
using LibHac.Ns;
using Ryujinx.Audio.Backends.CompatLayer;
using Ryujinx.Audio.Integration;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Cpu;
using Ryujinx.Graphics.Gpu;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Apm;
using Ryujinx.HLE.HOS.Services.Hid;
using Ryujinx.HLE.Loaders.Processes;
using Ryujinx.HLE.UI;
using Ryujinx.Memory;
using System;

namespace Ryujinx.HLE
{
    public class Switch : IDisposable
    {
        public static Switch Shared { get; private set; }
        public HleConfiguration Configuration { get; }
        public IHardwareDeviceDriver AudioDeviceDriver { get; }
        public MemoryBlock Memory { get; }
        public GpuContext Gpu { get; }
        public VirtualFileSystem FileSystem { get; }
        public HOS.Horizon System { get; }
        public bool TurboMode = false;
        public long TickScalar { get => System?.TickSource?.TickScalar ?? ITickSource.RealityTickScalar; set => System.TickSource.TickScalar = value; }
        public ProcessLoader Processes { get; }
        public PerformanceStatistics Statistics { get; }
        public Hid Hid { get; }
        public TamperMachine TamperMachine { get; }
        public IHostUIHandler UIHandler { get; }
        public Debugger.Debugger Debugger { get; }
        public int CpuCoresCount = 4;
        public VSyncMode VSyncMode { get; set; }
        public bool CustomVSyncIntervalEnabled { get; set; }
        public int CustomVSyncInterval { get; set; }
        public long TargetVSyncInterval { get; set; } = 60;
        public bool IsFrameAvailable => Gpu.Window.IsFrameAvailable;
        public DirtyHacks DirtyHacks { get; }

        public Switch(HleConfiguration configuration)
        {
            void SWLOG(string s){ try{ Console.WriteLine(s); System.IO.File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", DateTime.Now+": "+s+Environment.NewLine); }catch{} }

            SWLOG("[SWITCH] ctor START");
            SWLOG("[SWITCH] GpuRenderer="+(configuration.GpuRenderer==null?"NULL":configuration.GpuRenderer.GetType().FullName));
            SWLOG("[SWITCH] AudioDeviceDriver="+(configuration.AudioDeviceDriver==null?"NULL":configuration.AudioDeviceDriver.GetType().FullName));
            SWLOG("[SWITCH] UserChannelPersistence="+(configuration.UserChannelPersistence==null?"NULL":"OK"));

            ArgumentNullException.ThrowIfNull(configuration.GpuRenderer);
            ArgumentNullException.ThrowIfNull(configuration.AudioDeviceDriver);
            ArgumentNullException.ThrowIfNull(configuration.UserChannelPersistence);

            Configuration = configuration;
            FileSystem = Configuration.VirtualFileSystem;
            UIHandler = Configuration.HostUIHandler;

            MemoryAllocationFlags memoryAllocationFlags = configuration.MemoryManagerMode == MemoryManagerMode.SoftwarePageTable ? MemoryAllocationFlags.Reserve : MemoryAllocationFlags.Reserve | MemoryAllocationFlags.Mirrorable;

            SWLOG("[SWITCH] ANTES DirtyHacks");
            DirtyHacks = new DirtyHacks(Configuration.Hacks);
            SWLOG("[SWITCH] DEPOIS DirtyHacks");

            SWLOG("[SWITCH] ANTES AudioDeviceDriver");
            AudioDeviceDriver = new CompatLayerHardwareDeviceDriver(Configuration.AudioDeviceDriver);
            SWLOG("[SWITCH] DEPOIS AudioDeviceDriver");

            SWLOG("[SWITCH] ANTES MemoryBlock DramSize="+Configuration.MemoryConfiguration.DramSize);
            Memory = new MemoryBlock(Configuration.MemoryConfiguration.DramSize, memoryAllocationFlags);
            SWLOG("[SWITCH] DEPOIS MemoryBlock");

            SWLOG("[SWITCH] ANTES GpuContext");
            Gpu = new GpuContext(Configuration.GpuRenderer, DirtyHacks);
            SWLOG("[SWITCH] DEPOIS GpuContext");

            SWLOG("[SWITCH] ANTES Debugger");
            Debugger = Configuration.EnableGdbStub ? new Debugger.Debugger(this, Configuration.GdbStubPort) : null;
            SWLOG("[SWITCH] DEPOIS Debugger");

            SWLOG("[SWITCH] ANTES Horizon");
            System = new HOS.Horizon(this);
            SWLOG("[SWITCH] DEPOIS Horizon");

            Statistics = new PerformanceStatistics(this);
            Hid = new Hid(this, System.HidStorage);
            Processes = new ProcessLoader(this);
            TamperMachine = new TamperMachine();

            SWLOG("[SWITCH] ANTES InitializeServices");
            System.InitializeServices();
            SWLOG("[SWITCH] DEPOIS InitializeServices");

            System.State.SetLanguage(Configuration.SystemLanguage);
            System.State.SetRegion(Configuration.Region);
            VSyncMode = Configuration.VSyncMode;
            CustomVSyncInterval = Configuration.CustomVSyncInterval;
            TickScalar = TurboMode ? Configuration.TickScalar : ITickSource.RealityTickScalar;
            System.State.DockedMode = Configuration.EnableDockedMode;
            System.PerformanceState.PerformanceMode = System.State.DockedMode ? PerformanceMode.Boost : PerformanceMode.Default;
            System.EnablePtc = Configuration.EnablePtc;
            System.FsIntegrityCheckLevel = Configuration.FsIntegrityCheckLevel;
            System.GlobalAccessLogMode = Configuration.FsGlobalAccessLogMode;
            UpdateVSyncInterval();
            Shared = this;
            SWLOG("[SWITCH] ctor END OK");
        }

        public void ProcessFrame(){ Gpu.ProcessShaderCacheQueue(); Gpu.Renderer.PreFrame(); Gpu.GPFifo.DispatchCalls(); }
        public int IncrementCustomVSyncInterval(){ CustomVSyncInterval+=1; UpdateVSyncInterval(); return CustomVSyncInterval; }
        public int DecrementCustomVSyncInterval(){ CustomVSyncInterval-=1; UpdateVSyncInterval(); return CustomVSyncInterval; }
        public void UpdateVSyncInterval(){ switch(VSyncMode){ case VSyncMode.Custom: TargetVSyncInterval=CustomVSyncInterval; break; case VSyncMode.Switch: TargetVSyncInterval=60; break; case VSyncMode.Unbounded: TargetVSyncInterval=1; break; } }
        public void ToggleTurbo(){ TurboMode=!TurboMode; TickScalar=TurboMode?Configuration.TickScalar:ITickSource.RealityTickScalar; }
        public bool LoadCart(string exeFsDir, string romFsFile = null) => Processes.LoadUnpackedNca(exeFsDir, romFsFile);
        public bool LoadXci(string xciFile, ulong applicationId = 0) => Processes.LoadXci(xciFile, applicationId);
        public bool LoadNca(string ncaFile, BlitStruct<ApplicationControlProperty>? customNacpData = null) => Processes.LoadNca(ncaFile, customNacpData);
        public bool LoadNsp(string nspFile, ulong applicationId = 0) => Processes.LoadNsp(nspFile, applicationId);
        public bool LoadProgram(string fileName) => Processes.LoadNxo(fileName);
        public void SetVolume(float volume) => AudioDeviceDriver.Volume = Math.Clamp(volume, 0f, 1f);
        public float GetVolume() => AudioDeviceDriver.Volume;
        public bool IsAudioMuted() => AudioDeviceDriver.Volume == 0;
        public void EnableCheats() => ModLoader.EnableCheats(Processes.ActiveApplication.ProgramId, TamperMachine);
        public bool WaitFifo() => Gpu.GPFifo.WaitForCommands();
        public bool ConsumeFrameAvailable() => Gpu.Window.ConsumeFrameAvailable();
        public void PresentFrame(Action swapBuffersCallback) => Gpu.Window.Present(swapBuffersCallback);
        public void DisposeGpu() => Gpu.Dispose();
        public void Dispose(){ GC.SuppressFinalize(this); Dispose(true); }
        protected virtual void Dispose(bool disposing){ if(disposing){ Processes.ClearAllProcesses(); System.Dispose(); AudioDeviceDriver.Dispose(); FileSystem.Dispose(); Memory.Dispose(); Debugger?.Dispose(); TitleIDs.CurrentApplication.Value=null; Shared=null; } }
    }
}
