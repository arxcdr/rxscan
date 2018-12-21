using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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

        // Default temporary folder where scanned files will be stored for conversion
        Windows.Storage.StorageFolder tempFolder;

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

        // Create temp folder to store scanned file
        private async Task<StorageFolder> CreateTempFolder()
        {
            // Create temporary folder in app install location
            tempFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("TempFiles", Windows.Storage.CreationCollisionOption.OpenIfExists);
            return tempFolder;
        }

        // Convert scanned file to jpeg
        private async Task<StorageFile> ConvertTempFile()
        {
            IReadOnlyList<StorageFile> files = await tempFolder.GetFilesAsync();
            StorageFile image = files[0];

            // Create new jpeg file in local folder
            StorageFile jpegOutput = await localFolder.CreateFileAsync("NumérisationSansTitre.jpeg",
                Windows.Storage.CreationCollisionOption.GenerateUniqueName);

            // Convert the image
            convert_to_jpeg conversion = new convert_to_jpeg();
            await conversion.ConvertImageToJpegAsync(image, jpegOutput);

            return image;
        }

        // Delete temp folder with scanned file
        private async Task<StorageFolder> DeleteTempFolder ()
        {
            await tempFolder.DeleteAsync(StorageDeleteOption.PermanentDelete);
            await CreateTempFolder();
            return tempFolder;
        }

        // Scan documents into specified folder
        private async Task<StorageFolder> ScanToFolder(string deviceId, StorageFolder folder, ColorMode clr)
        {
            ImageScannerResolution scanRes = new ImageScannerResolution
            {
                DpiX = 300,
                DpiY = 300,
            };

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

                myScanner.FlatbedConfiguration.DesiredResolution = scanRes;

                // Scan API call to start scanning  
                var result = await myScanner.ScanFilesToFolderAsync(ImageScannerScanSource.Default, folder);
                
            }
            catch (OperationCanceledException)
            {
                //Utils.DisplayScanCancelationMessage();
            }
            return folder;
        }

        private enum ColorMode
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

        // Initiate document scanning in greyscale
        private async void BWScanButton_Click(object sender, RoutedEventArgs e)
        {
            await CreateTempFolder();
            StatusBlock.Text = "Numérisation en cours, veuillez patienter...";
            PRing.IsActive = true;
            DisableUI();
            await ScanToFolder(scanContext.CurrentScannerDeviceId, tempFolder, ColorMode.Greyscale);
            StatusBlock.Text = "Conversion en cours, veuillez patienter...";
            await ConvertTempFile();
            await DeleteTempFolder();
            StatusBlock.Text = "Prêt pour la numérisation.";
            PRing.IsActive = false;
            EnableUI();
        }

        // Initiate document scanning in color
        private async void CLRScanButton_Click(object sender, RoutedEventArgs e)
        {
            await CreateTempFolder();
            StatusBlock.Text = "Numérisation en cours, veuillez patienter...";
            PRing.IsActive = true;
            DisableUI();
            await ScanToFolder(scanContext.CurrentScannerDeviceId, tempFolder, ColorMode.Color);
            StatusBlock.Text = "Conversion en cours, veuillez patienter...";
            await ConvertTempFile();
            await DeleteTempFolder();
            StatusBlock.Text = "Prêt pour la numérisation.";
            PRing.IsActive = false;
            EnableUI();
        }

        private async void ShowFilesButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Opening local folder...");

            // Open local folder
            await Launcher.LaunchFolderAsync(localFolder);
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

        // Disable buttons while a document is being scanned to prevent errors
        private void DisableUI()
        {
            BWScanButton.IsEnabled = false;
            CLRScanButton.IsEnabled = false;
            FolderButton.IsEnabled = false;
            ShowFilesButton.IsEnabled = false;
        }

        // Enable the UI when the app is ready
        private void EnableUI()
        {
            BWScanButton.IsEnabled = true;
            CLRScanButton.IsEnabled = true;
            FolderButton.IsEnabled = true;
            ShowFilesButton.IsEnabled = true;
        }
    }

}
