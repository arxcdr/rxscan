using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.System;
using System.Threading.Tasks;


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace RXscan
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        // Save default local folder path
        Windows.Storage.ApplicationDataContainer localSettings =
                Windows.Storage.ApplicationData.Current.LocalSettings;
        Windows.Storage.StorageFolder localFolder =
            Windows.Storage.ApplicationData.Current.LocalFolder;

        // Default folder path to display in the UI
        public String dPath { get; set; }

        // Device watcher used to detect any connected scanner
        public DeviceWatcher scannerWatcher;

        // Scanner context
        public ScannerContext scanContext;

        public static MainPage Current;

        public MainPage()
        {
            this.InitializeComponent();           
            Debug.WriteLine(localSettings.Values);
            CheckDefaultFolderPath();
            DataContext = this;

            // This is a static public property that will allow downstream pages to get  
            // a handle to the MainPage instance in order to call methods that are in this class. 
            Current = this;

            // Create scanner context object to access scanning methods
            scanContext = new ScannerContext();

            // Automatically start enumerating scanners
            scanContext.StartScannerWatcher();

            StatusBlock.Text = "Prêt pour la numérisation.";
        }

        // SCANNER CODE
        public async void ScanToFolder(string deviceId, StorageFolder folder, ColorMode clr)
        {
            try
            {
                // Get the scanner object for this device id 
                ImageScanner myScanner = await ImageScanner.FromIdAsync(deviceId);

                // Set color mode depending on user input
                if(clr == ColorMode.Greyscale)
                {
                    myScanner.FlatbedConfiguration.ColorMode = ImageScannerColorMode.Grayscale;
                }
                else
                {
                    myScanner.FlatbedConfiguration.ColorMode = ImageScannerColorMode.Color;
                }

                // Scan API call to start scanning  
                var result = await myScanner.ScanFilesToFolderAsync(ImageScannerScanSource.Default, folder);

            }
            catch (OperationCanceledException)
            {
                //Utils.DisplayScanCancelationMessage();
            }

        }

        public enum ColorMode
        {
            Greyscale,
            Color,
        }

        // FILE SYSTEM CODE
        private void CheckDefaultFolderPath()
        {            
            // Load default folder path and set it if not already defined
            if (localSettings.Values.ContainsKey("DefaultToken"))
            {
                var tempFolder = RetrieveDefaultFolder().Result;
                if (tempFolder != null && tempFolder is StorageFolder)
                {
                    localFolder = tempFolder;
                    dPath = localFolder.Path;
                    Debug.WriteLine("Existing Default Path found: " + dPath);
                }
                
            }
            else
            {
                localSettings.Values["DefaultPath"] = localFolder.Path;
                dPath = localSettings.Values["DefaultPath"].ToString();
                Debug.WriteLine("Created Default Path: " + dPath);
            }            
            OnPropertyChanged("dPath");
        }

        // Set the last default folder saved by user as current localfolder
        private async Task<StorageFolder> RetrieveDefaultFolder()
        {
            string folderToken = localSettings.Values["DefaultToken"].ToString();
            if (StorageApplicationPermissions.FutureAccessList.ContainsItem(folderToken))
            {
                return await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(folderToken);
            }
            else
            {
                return null;
            }            
        }

        private void BWScanButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Testing black & white scan button");
            ScanToFolder(scanContext.CurrentScannerDeviceId, localFolder, ColorMode.Greyscale);
            StatusBlock.Text = "Numérisation en cours, veuillez patienter.";
        }

        private void CLRScanButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Testing color scan button");            
            ScanToFolder(scanContext.CurrentScannerDeviceId, localFolder, ColorMode.Color);
            StatusBlock.Text = "Numérisation en cours, veuillez patienter.";
        }

        private async void ShowFilesButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Opening local folder...");

            // Open local folder
            await Launcher.LaunchFolderAsync(localFolder);

            // TODO:
            // 1. Show default folder path on UI + button to change it - DONE
            // 2. Save/Retrieve default folder path from app settings - DONE
            // 3. Handle errors & exceptions to some extent
        }

        // Browse to choose default folder
        private async void FolderBrowse()
        {           
            FolderPicker picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            var pfolder = await picker.PickSingleFolderAsync();            

            // If the user selected a new folder...
            if(pfolder != null){
                Debug.WriteLine("PFolder: " + pfolder.Path);

                // Save user folder in permission list for future access
                StorageApplicationPermissions.FutureAccessList.AddOrReplace("DefaultFolderToken", pfolder);
                localSettings.Values["DefaultToken"] = "DefaultFolderToken";
                Debug.WriteLine("Folder access saved for path: " + pfolder.Path);

                // Update Default Path
                dPath = pfolder.Path;
                OnPropertyChanged("dPath");

                // Update Local Folder
                localFolder = pfolder;
            }

            else
            {
                Debug.WriteLine("Folder browsing cancelled by User.");
            }
            
        }

        private void FolderButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Browsing folders...");
            FolderBrowse();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler propertyChangedEvent = PropertyChanged;
            if (propertyChangedEvent != null)
            {
                propertyChangedEvent(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

}
