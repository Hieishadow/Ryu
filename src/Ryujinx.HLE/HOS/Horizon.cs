//... mantém todos os usings iguais que você mandou...

namespace Ryujinx.HLE.HOS
{
    using TimeServiceManager = Services.Time.TimeManager;

    public class Horizon : IDisposable
    {
        //... mantém todos os fields iguais...

        private static void SLog(string s){
            try{
                Console.WriteLine($"[HOS] {s}");
                File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now}: [HOS] {s}\n");
            }catch{}
        }

        public Horizon(Switch device)
        {
            //... mantém seu construtor igual até o final...
            TickSource = new TickSource(KernelConstants.CounterFrequency);
            KernelContext = new KernelContext(TickSource, device, device.Memory, device.Configuration.MemoryConfiguration.KernelMemorySize, device.Configuration.MemoryConfiguration.KernelMemoryArrange);
            Device = device;
            State = new SystemStateMgr();
            PerformanceState = new PerformanceState();
            NfpDevices = [];
            NfcDevices = [];
            KMemoryRegionManager region = KernelContext.MemoryManager.MemoryRegions[(int)MemoryRegion.NvServices];
            ulong hidPa = region.Address;
            ulong fontPa = region.Address + HidSize;
            ulong iirsPa = region.Address + HidSize + FontSize;
            ulong timePa = region.Address + HidSize + FontSize + IirsSize;
            ulong appletCaptureBufferPa = region.Address + HidSize + FontSize + IirsSize + TimeSize;
            KPageList hidPageList = new(); KPageList fontPageList = new(); KPageList iirsPageList = new(); KPageList timePageList = new(); KPageList appletCaptureBufferPageList = new();
            hidPageList.AddRange(hidPa, HidSize / KPageTableBase.PageSize);
            fontPageList.AddRange(fontPa, FontSize / KPageTableBase.PageSize);
            iirsPageList.AddRange(iirsPa, IirsSize / KPageTableBase.PageSize);
            timePageList.AddRange(timePa, TimeSize / KPageTableBase.PageSize);
            appletCaptureBufferPageList.AddRange(appletCaptureBufferPa, AppletCaptureBufferSize / KPageTableBase.PageSize);
            SharedMemoryStorage hidStorage = new(KernelContext, hidPageList);
            SharedMemoryStorage fontStorage = new(KernelContext, fontPageList);
            SharedMemoryStorage iirsStorage = new(KernelContext, iirsPageList);
            SharedMemoryStorage timeStorage = new(KernelContext, timePageList);
            SharedMemoryStorage appletCaptureBufferStorage = new(KernelContext, appletCaptureBufferPageList);
            HidStorage = hidStorage;
            HidSharedMem = new KSharedMemory(KernelContext, hidStorage, 0, 0, KMemoryPermission.Read);
            FontSharedMem = new KSharedMemory(KernelContext, fontStorage, 0, 0, KMemoryPermission.Read);
            IirsSharedMem = new KSharedMemory(KernelContext, iirsStorage, 0, 0, KMemoryPermission.Read);
            KSharedMemory timeSharedMemory = new(KernelContext, timeStorage, 0, 0, KMemoryPermission.Read);
            TimeServiceManager.Instance.Initialize(device, this, timeSharedMemory, timeStorage, TimeSize);
            AppletCaptureBufferTransfer = new KTransferMemory(KernelContext, appletCaptureBufferStorage);
            AppletState = new AppletStateMgr(this);
            AppletState.SetFocus(true);
            VsyncEvent = new KEvent(KernelContext);
            DisplayResolutionChangeEvent = new KEvent(KernelContext);
            SharedFontManager = new SharedFontManager(device, fontStorage);
            AccountManager = device.Configuration.AccountManager;
            ContentManager = device.Configuration.ContentManager;
            CaptureManager = new CaptureManager(device);
            LibHacHorizonManager = device.Configuration.LibHacHorizonManager;
            UInt128 clockSourceId = new(0x36a0328702ce8bc1, 0x1608eaba02333284);
            IRtcManager.GetExternalRtcValue(out ulong rtcValue);
            TimeSpanType systemTime = TimeSpanType.FromSeconds((long)rtcValue);
            TimeSpanType internalOffset = TimeSpanType.FromSeconds(device.Configuration.SystemTimeOffset);
            TimeSpanType systemTimeOffset = new(systemTime.NanoSeconds + internalOffset.NanoSeconds);
            if (systemTime.IsDaylightSavingTime() &&!systemTimeOffset.IsDaylightSavingTime()) internalOffset = internalOffset.AddSeconds(3600L);
            else if (!systemTime.IsDaylightSavingTime() && systemTimeOffset.IsDaylightSavingTime()) internalOffset = internalOffset.AddSeconds(-3600L);
            systemTime = new TimeSpanType(systemTime.NanoSeconds + internalOffset.NanoSeconds);
            TimeServiceManager.Instance.SetupStandardSteadyClock(TickSource, clockSourceId, TimeSpanType.Zero, TimeSpanType.Zero, TimeSpanType.Zero, false);
            TimeServiceManager.Instance.SetupStandardLocalSystemClock(TickSource, new SystemClockContext(), systemTime.ToSeconds());
            TimeServiceManager.Instance.StandardLocalSystemClock.GetClockContext(TickSource, out SystemClockContext localSytemClockContext);
            if (NxSettings.Settings.TryGetValue("time!standard_network_clock_sufficient_accuracy_minutes", out object standardNetworkClockSufficientAccuracyMinutes))
            {
                TimeSpanType standardNetworkClockSufficientAccuracy = new((int)standardNetworkClockSufficientAccuracyMinutes * 60000000000);
                TimeServiceManager.Instance.SetupStandardNetworkSystemClock(localSytemClockContext, standardNetworkClockSufficientAccuracy);
            }
            TimeServiceManager.Instance.SetupStandardUserSystemClock(TickSource, true, localSytemClockContext.SteadyTimePoint);
            TimeServiceManager.Instance.SetupEphemeralNetworkSystemClock();
            DatabaseImpl.Instance.InitializeDatabase(TickSource, LibHacHorizonManager.SdbClient);
            HostSyncpoint = new NvHostSyncpt(device);
            SurfaceFlinger = new SurfaceFlinger(device);
        }

        public void InitializeServices()
        {
            SLog("InitializeServices START");
            try{
                SmRegistry = new SmRegistry();
                SmServer = new ServerBase(KernelContext, "Sm", () => new IUserInterface(KernelContext, SmRegistry));
                SmServer.InitDone.WaitOne();
                SLog("SmServer OK");
                BsdServer = new ServerBase(KernelContext, "Bsd");
                FsServer = new ServerBase(KernelContext, "Fs");
                HidServer = new ServerBase(KernelContext, "Hid");
                NvDrvServer = new ServerBase(KernelContext, "Nv");
                TimeServer = new ServerBase(KernelContext, "Time");
                ViServer = new ServerBase(KernelContext, "Vi:u");
                ViServerM = new ServerBase(KernelContext, "Vi:m");
                ViServerS = new ServerBase(KernelContext, "Vi:s");
                LdnServer = new ServerBase(KernelContext, "Ldn");
                SLog("All Servers OK");
                StartNewServices();
                SLog("InitializeServices END OK");
            }catch(Exception ex){
                SLog("InitializeServices CRASH: " + ex.ToString());
                throw;
            }
        }

        private void StartNewServices()
        {
            SLog("StartNewServices START");
            HorizonFsClient fsClient = new(this);
            ServiceTable = new ServiceTable();
            IEnumerable<ServiceEntry> services = ServiceTable.GetServices(new HorizonOptions
                (Device.Configuration.IgnoreMissingServices, LibHacHorizonManager.BcatClient, fsClient, AccountManager, Device.AudioDeviceDriver, TickSource));
            int idx=0;
            foreach (ServiceEntry service in services)
            {
                SLog($"Service[{idx}] {service.Name} STARTING");
                const ProcessCreationFlags Flags = ProcessCreationFlags.EnableAslr | ProcessCreationFlags.AddressSpace64Bit | ProcessCreationFlags.Is64Bit | ProcessCreationFlags.PoolPartitionSystem;
                ProcessCreationInfo creationInfo = new(service.Name, 1, 0, 0x8000000, 1, Flags, 0, 0);
                uint[] defaultCapabilities = [(((uint)KScheduler.CpuCoresCount - 1) << 24) + (((uint)KScheduler.CpuCoresCount - 1) << 16) + 0x63F7u, 0x1FFFFFCF, 0x207FFFEF, 0x47E0060F, 0x0048BFFF, 0x01007FFF];
                try{
                    KernelStatic.StartInitialProcess(KernelContext, creationInfo, defaultCapabilities, 44, () =>
                    {
                        try{ service.Start(KernelContext.Syscall, KernelStatic.GetCurrentProcess().CpuMemory, KernelStatic.GetCurrentThread().ThreadContext); }
                        catch(Exception ex){ SLog($"Service {service.Name} Start CRASH: {ex}"); throw; }
                    });
                    SLog($"Service[{idx}] {service.Name} OK");
                }catch(Exception ex){
                    SLog($"Service[{idx}] {service.Name} CRASH: {ex}");
                    throw;
                }
                idx++;
            }
            SLog("StartNewServices END OK");
        }

        //... mantém o resto do arquivo igual (LoadKip, ChangeDockedMode, ScanAmiibo, Dispose, etc)...
        public bool LoadKip(string kipPath){ using SharedRef<IStorage> kipFile = new(new LocalStorage(kipPath, FileAccess.Read)); return ProcessLoaderHelper.LoadKip(KernelContext, new KipExecutable(in kipFile)); }
        public void ChangeDockedModeState(bool newState){ if (newState!= State.DockedMode){ State.DockedMode = newState; PerformanceState.PerformanceMode = State.DockedMode? PerformanceMode.Boost : PerformanceMode.Default; AppletState.Messages.Enqueue(AppletMessage.OperationModeChanged); AppletState.Messages.Enqueue(AppletMessage.PerformanceModeChanged); AppletState.MessageEvent.ReadableEvent.Signal(); SignalDisplayResolutionChange(); Device.Configuration.RefreshInputConfig?.Invoke(); } }
        public void ReturnFocus(){ AppletState.SetFocus(true); }
        public void SimulateWakeUpMessage(){ AppletState.Messages.Enqueue(AppletMessage.Resume); AppletState.MessageEvent.ReadableEvent.Signal(); }
        public void ScanAmiibo(int nfpDeviceId, string amiiboId, bool useRandomUuid){ if (VirtualAmiibo.ApplicationBytes.Length > 0){ VirtualAmiibo.ApplicationBytes = []; VirtualAmiibo.InputBin = string.Empty; } if (NfpDevices[nfpDeviceId].State == NfpDeviceState.SearchingForTag){ NfpDevices[nfpDeviceId].State = NfpDeviceState.TagFound; NfpDevices[nfpDeviceId].AmiiboId = amiiboId; NfpDevices[nfpDeviceId].UseRandomUuid = useRandomUuid; } }
        public void ScanAmiiboFromBin(string path){ VirtualAmiibo.InputBin = path; if (VirtualAmiibo.ApplicationBytes.Length > 0){ VirtualAmiibo.ApplicationBytes = []; } byte[] encryptedData = File.ReadAllBytes(path); VirtualAmiiboFile newFile = AmiiboBinReader.ReadBinFile(encryptedData); if (SearchingForAmiibo(out int nfpDeviceId)){ NfpDevices[nfpDeviceId].State = NfpDeviceState.TagFound; NfpDevices[nfpDeviceId].AmiiboId = newFile.AmiiboId; NfpDevices[nfpDeviceId].UseRandomUuid = false; } }
        public void ScanSkylander(int nfcDeviceId, byte[] data){ if (NfcDevices[nfcDeviceId].State == NfcDeviceState.SearchingForTag){ NfcDevices[nfcDeviceId].State = NfcDeviceState.TagFound; NfcDevices[nfcDeviceId].Data = data; } }
        public bool SearchingForAmiibo(out int nfpDeviceId){ nfpDeviceId = default; for (int i = 0; i < NfpDevices.Count; i++){ if (NfpDevices[i].State == NfpDeviceState.SearchingForTag){ nfpDeviceId = i; return true; } } return false; }
        public bool SearchingForSkylander(out int nfcDeviceId){ nfcDeviceId = default; for (int i = 0; i < NfcDevices.Count; i++){ if (NfcDevices[i].State == NfcDeviceState.SearchingForTag){ nfcDeviceId = i; return true; } } return false; }
        public bool HasSkylander(out int nfcDeviceId){ nfcDeviceId = default; for (int i = 0; i < NfcDevices.Count; i++){ if (NfcDevices[i].State == NfcDeviceState.TagFound){ nfcDeviceId = i; return true; } } return false; }
        public void RemoveSkylander(){ for (int i = 0; i < NfcDevices.Count; i++){ if (NfcDevices[i].State == NfcDeviceState.TagFound){ NfcDevices[i].State = NfcDeviceState.Initialized; NfcDevices[i].SignalDeactivate(); Thread.Sleep(100); } } }
        public void SignalDisplayResolutionChange(){ DisplayResolutionChangeEvent.ReadableEvent.Signal(); }
        public void SignalVsync(){ VsyncEvent.ReadableEvent.Signal(); }
        public void Dispose(){ GC.SuppressFinalize(this); Dispose(true); }
        protected virtual void Dispose(bool disposing){ if (!_isDisposed && disposing){ _isDisposed = true; if (IsPaused){ TogglePauseEmulation(false); } KProcess terminationProcess = new(KernelContext); KThread terminationThread = new(KernelContext); terminationThread.Initialize(0, 0, 0, 3, 0, terminationProcess, ThreadType.Kernel, () => { lock (KernelContext.Processes){ foreach (KProcess process in KernelContext.Processes.Values.Where(x => x.IsApplication)){ process.Terminate(); process.DecrementReferenceCount(); } SurfaceFlinger.Dispose(); foreach (KProcess process in KernelContext.Processes.Values.Where(x =>!x.IsApplication)){ process.Terminate(); process.DecrementReferenceCount(); } KernelContext.Processes.Clear(); } KernelStatic.GetCurrentThread().Exit(); }); terminationThread.Start(); while (terminationThread.HostThread.ThreadState == ThreadState.Unstarted){ Thread.Sleep(10); } terminationThread.HostThread.Join(); INvDrvServices.Destroy(); if (LibHacHorizonManager.ApplicationClient!= null){ LibHacHorizonManager.PmClient.Fs.UnregisterProgram(LibHacHorizonManager.ApplicationClient.Os.GetCurrentProcessId().Value).ThrowIfFailure(); } KernelContext.Dispose(); } }
        public void TogglePauseEmulation(bool pause){ lock (KernelContext.Processes){ foreach (KProcess process in KernelContext.Processes.Values){ if (process.IsApplication){ process.SetActivity(pause); } } if (pause &&!IsPaused){ Device.AudioDeviceDriver.GetPauseEvent().Reset(); TickSource.Suspend(); } else if (!pause && IsPaused){ Device.AudioDeviceDriver.GetPauseEvent().Set(); TickSource.Resume(); } } IsPaused = pause; }
        internal IDebuggableProcess DebugGetApplicationProcessDebugInterface(){ lock (KernelContext.Processes){ return KernelContext.Processes.Values.FirstOrDefault(x => x.IsApplication)?.DebugInterface; } }
        internal KProcess DebugGetApplicationProcess(){ lock (KernelContext.Processes){ return KernelContext.Processes.Values.FirstOrDefault(x => x.IsApplication); } }
    }
}
