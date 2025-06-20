using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.ApplicationModel.DataTransfer;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using doorcamPoC.Services;

namespace doorcamPoC.Views
{
    /// <summary>
    /// A simple page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public partial class MainPage : Page
    {
        private StorageFile? selectedFile;
        private bool isAnalyzing = false;
        private readonly VideoAnalysisService _analysisService;

        public MainPage()
        {
            this.InitializeComponent();
            _analysisService = new VideoAnalysisService();
            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            // Button click handlers
            UploadButton.Click += UploadButton_Click;
            CopyButton.Click += CopyButton_Click;
            RetryButton.Click += RetryButton_Click;

            // Drag and drop handlers
            DropZone.DragOver += DropZone_DragOver;
            DropZone.Drop += DropZone_Drop;
            DropZone.Tapped += DropZone_Tapped;

            // Enable drag and drop
            DropZone.AllowDrop = true;
        }

        private async void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            await PickVideoFile();
        }

        private async void DropZone_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await PickVideoFile();
        }

        private async Task PickVideoFile()
        {
            try
            {
                var filePicker = new FileOpenPicker();
                filePicker.FileTypeFilter.Add(".mp4");
                filePicker.FileTypeFilter.Add(".avi");
                filePicker.FileTypeFilter.Add(".mov");
                filePicker.FileTypeFilter.Add(".mkv");

                // Get the current window's HWND
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

                var file = await filePicker.PickSingleFileAsync();
                if (file != null)
                {
                    await ProcessSelectedFile(file);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error picking file: {ex.Message}");
            }
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Drop video file here";
            }
        }

        private async void DropZone_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    if (items.Count > 0 && items[0] is StorageFile file)
                    {
                        var extension = Path.GetExtension(file.Name).ToLower();
                        if (extension == ".mp4" || extension == ".avi" || extension == ".mov" || extension == ".mkv")
                        {
                            await ProcessSelectedFile(file);
                        }
                        else
                        {
                            ShowError("Please select a valid video file (.mp4, .avi, .mov, .mkv)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error processing dropped file: {ex.Message}");
            }
        }

        private async Task ProcessSelectedFile(StorageFile file)
        {
            try
            {
                selectedFile = file;
                
                // Update UI to show file info
                FileNameText.Text = file.Name;
                FileSizeText.Text = await GetFileSizeString(file);
                FileInfoPanel.Visibility = Visibility.Visible;
                
                // Hide error section if it was showing
                ErrorSection.Visibility = Visibility.Collapsed;
                
                // Start analysis
                await StartVideoAnalysis();
            }
            catch (Exception ex)
            {
                ShowError($"Error processing file: {ex.Message}");
            }
        }

        private async Task<string> GetFileSizeString(StorageFile file)
        {
            var properties = await file.GetBasicPropertiesAsync();
            var sizeInBytes = properties.Size;
            
            if (sizeInBytes < 1024)
                return $"{sizeInBytes} B";
            else if (sizeInBytes < 1024 * 1024)
                return $"{sizeInBytes / 1024:F1} KB";
            else if (sizeInBytes < 1024 * 1024 * 1024)
                return $"{sizeInBytes / (1024 * 1024):F1} MB";
            else
                return $"{sizeInBytes / (1024 * 1024 * 1024):F1} GB";
        }

        private async Task StartVideoAnalysis()
        {
            if (selectedFile == null || isAnalyzing) return;

            isAnalyzing = true;
            
            // Show progress section
            ProgressSection.Visibility = Visibility.Visible;
            ResultsSection.Visibility = Visibility.Collapsed;
            ErrorSection.Visibility = Visibility.Collapsed;

            try
            {
                // Update progress
                ProgressText.Text = "Loading video file...";
                ProgressDetails.Text = "Step 1 of 4";
                ProgressBar.Value = 25;

                // Analyze video using the service
                var result = await _analysisService.AnalyzeVideoAsync(selectedFile);
                
                // Update progress
                ProgressText.Text = "Generating summary...";
                ProgressDetails.Text = "Step 4 of 4";
                ProgressBar.Value = 100;
                
                // Show results
                ShowAnalysisResults(result);
            }
            catch (Exception ex)
            {
                ShowError($"Analysis failed: {ex.Message}");
            }
            finally
            {
                isAnalyzing = false;
                ProgressSection.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowAnalysisResults(VideoAnalysisResult result)
        {
            SummaryText.Text = result.Summary;
            ResultsSection.Visibility = Visibility.Visible;
        }

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(SummaryText.Text);
                Clipboard.SetContent(dataPackage);
                
                // Show temporary success message
                var originalContent = CopyButton.Content;
                CopyButton.Content = "Copied!";
                await Task.Delay(2000);
                CopyButton.Content = originalContent;
            }
            catch (Exception ex)
            {
                ShowError($"Failed to copy to clipboard: {ex.Message}");
            }
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            // Reset UI state
            ErrorSection.Visibility = Visibility.Collapsed;
            FileInfoPanel.Visibility = Visibility.Collapsed;
            ProgressSection.Visibility = Visibility.Collapsed;
            ResultsSection.Visibility = Visibility.Collapsed;
            
            selectedFile = null;
            isAnalyzing = false;
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorSection.Visibility = Visibility.Visible;
            ProgressSection.Visibility = Visibility.Collapsed;
            ResultsSection.Visibility = Visibility.Collapsed;
        }
    }
}
