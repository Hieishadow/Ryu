using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Gommon;
using LibHac.Common.Keys;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Systems.AppLibrary;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Ava.Utilities;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.Loaders.Processes.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace Ryujinx.Ava.UI.ViewModels
{
    public class Fat32MergeViewModel : BaseModel
    {
        public List<string> SplitPaths = new List<string>();

        public string Dir;

        public string FileName = "None Selected";

        public string keydir = AppDataManager.KeysDirPath;

        private KeySet _KeySet = KeySet.CreateDefaultKeySet(); // Check VirtualFileSystem.ReloadKeySet() perhaps
        
        List<ApplicationData> applications;
        
        private readonly MainWindowViewModel _mainWindowViewModel;
        
        public string MergedName
        {
            get
            {
                return $"{LocaleManager.Instance[LocaleKeys.Fat32Merge_FileNamePrefix]}{FileName}";
            }
        }
        
        public Fat32MergeViewModel(MainWindowViewModel mainWindowViewModel)
        {
            _mainWindowViewModel = mainWindowViewModel;
        }

        public async void OpenFolderPicker()
        {
            try
            {
                Optional<IStorageFolder> folder = await RyujinxApp.MainWindow.ViewModel.StorageProvider.OpenSingleFolderPickerAsync();
                Dir = folder.Value.Path.LocalPath; 
                SplitPaths = Directory.EnumerateFiles(Dir, "*").ToList();
                SplitPaths.Sort();
                foreach (string path in SplitPaths)
                {
                    if (Path.GetExtension(path).ToLower().Contains(".xci") || Path.GetExtension(path).ToLower().Contains(".nsp"))
                    {
                        SplitPaths.Remove(path);
                    }
                }

                
                bool IsXci = true;
                // Check ApplicationLibrary.TryGetApplicationsFromFile() for pointers.
                using FileStream file = new(SplitPaths[0], FileMode.Open, FileAccess.Read);
                // NOTE: Either the 00 or the merged dump will be first, so this is probably fine! (I sure hope so me)
                

                if (IsXci)
                {
                    // For XCI games
                    Xci xci = new(_mainWindowViewModel.VirtualFileSystem.KeySet, file.AsStorage());
                    IFileSystem pfs = xci.OpenPartition(XciPartitionType.Secure);
                    Dictionary<ulong, ContentMetaData> metadata = pfs.GetContentData(ContentMetaType.Application, _mainWindowViewModel.VirtualFileSystem , IntegrityCheckLevel.None); // So I should have checked if ContentMetaData included the title...
                    
                }
                else
                {
                    // For NSP games
                    PartitionFileSystem pfs = new();
                    pfs.Initialize(file.AsStorage());
                }

                
                
                

                
                //ApplicationData result = GetApplicationFromNsp(pfs, applicationPath);
                // In this struct there exists the title name from the NSP. Go figure it out future me!
                
                
                // Replace this with libhac or something idk
                FileName = Path.GetDirectoryName(Dir);
                FileName = FileName.Remove(0, FileName.LastIndexOf(Path.DirectorySeparatorChar) + 1);
            }
            catch (Exception exception)
            {
                Logger.Error?.Print(LogClass.Application, $"{LocaleManager.Instance[LocaleKeys.Fat32Merge_MergeEndFailed]}");
                Logger.Error?.Print(LogClass.Application, exception.ToString());
            }
        }

        public void MergeDump()
        {
            
            try
            {
                if (File.Exists($"{Dir}{FileName}"))
                {
                    File.Delete($"{Dir}{FileName}");
                    Logger.Notice.Print(LogClass.Application, $"{LocaleManager.Instance[LocaleKeys.Fat32Merge_RemoveExistingFile]}");
                }
                Logger.Notice.Print(LogClass.Application, $"{LocaleManager.Instance[LocaleKeys.Fat32Merge_MergeBegin]}");
                using (FileStream output = File.Create($"{Dir}{FileName}"))
                {
                    foreach (string file in SplitPaths)
                    {
                        using (FileStream input = File.OpenRead(file))
                        {
                            input.CopyTo(output);
                        }
                    }
                }
                Logger.Notice.Print(LogClass.Application, $"{LocaleManager.Instance[LocaleKeys.Fat32Merge_MergeEnd]}");
            }
            catch (Exception e)
            {
                Logger.Error?.Print(LogClass.Application, $"{LocaleManager.Instance[LocaleKeys.Fat32Merge_MergeEndFailed]}");
                Logger.Error?.Print(LogClass.Application, e.ToString());
            }
        }
    }
}
