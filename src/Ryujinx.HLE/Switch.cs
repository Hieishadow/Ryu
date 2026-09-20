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
using System.IO;

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

        private static void DebugLog(string message)
        {
            Console.WriteLine($"[SWITCH] {message}");
            try{ File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [SWITCH] {message}\n"); }catch{}
        }

        public Switch(HleConfiguration configuration)
        {
            DebugLog("ctor START");
            try{
                ArgumentNullException.ThrowIfNull(configuration.GpuRenderer);
                ArgumentNullException.ThrowIfNull(configuration.AudioDeviceDriver);
                ArgumentNullException.ThrowIfNull(configuration.UserChannelPersistence);
                
                Configuration = configuration;
                FileSystem = Configuration.VirtualFileSystem;
                UIHandler = Configuration.HostUIHandler;
                
                MemoryAllocationFlags memoryAllocationFlags = MemoryAllocationFlags.Reserve;
                DebugLog("MemoryAllocationFlags = Reserve ONLY (forced for Android)");

                DirtyHacks = new DirtyHacks(Configuration.Hacks);
                AudioDeviceDriver = new CompatLayerHardwareDeviceDriver(Configuration.AudioDeviceDriver);
                DebugLog("new MemoryBlock " + Configuration.MemoryConfiguration.DramSize);
                Memory = new MemoryBlock(Configuration.MemoryConfiguration.DramSize, memoryAllocationFlags);
                DebugLog("Memory OK Size=" + Memory.Size);
                Gpu = new GpuContext(Configuration.GpuRenderer, DirtyHacks);
                Debugger = Configuration.EnableGdbStub ? new Debugger.Debugger(this, Configuration.GdbStubPort) : null;
                System = new HOS.Horizon(this);
                Statistics = new PerformanceStatistics(this);
                Hid = new Hid(this, System.HidStorage);
                Processes = new ProcessLoader(this);
                TamperMachine = new TamperMachine();
                DebugLog("ANTES InitializeServices");
                System.InitializeServices();
                DebugLog("DEPOIS InitializeServices OK");
                
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
                DebugLog("ctor END OK");
            }catch(Exception ex){
                DebugLog("CTOR CRASH: " + ex.ToString());
                throw;
            }
        }

        public void ProcessFrame()
        {
            DebugLog("PF-> ProcessShaderCacheQueue START");
            Gpu.ProcessShaderCacheQueue();
            DebugLog("PF-> ProcessShaderCacheQueue END");

            DebugLog("PF-> PreFrame START");
            Gpu.Renderer.PreFrame();
            DebugLog("PF-> PreFrame END");

            DebugLog("PF-> DispatchCalls START");
            Gpu.GPFifo.DispatchCalls();
            DebugLog("PF-> DispatchCalls END");
        }

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
