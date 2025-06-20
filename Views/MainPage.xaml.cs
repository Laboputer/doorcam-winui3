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
using System.Linq;
using System.Text;

namespace doorcamPoC.Views
{
    /// <summary>
    /// A simple page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public partial class MainPage : Page
    {
        private StorageFile? selectedFile;
        private bool isAnalyzing = false;
        private readonly AdvancedVideoAnalysisService _advancedAnalysisService;

        public MainPage()
        {
            this.InitializeComponent();
            _advancedAnalysisService = new AdvancedVideoAnalysisService();
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
                await StartAdvancedVideoAnalysis();
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

        private async Task StartAdvancedVideoAnalysis()
        {
            if (selectedFile == null || isAnalyzing) return;

            isAnalyzing = true;
            
            // Show progress section
            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressSection.Visibility = Visibility.Visible;
                ResultsSection.Visibility = Visibility.Collapsed;
                ErrorSection.Visibility = Visibility.Collapsed;
            });

            try
            {
                // Update progress - Windows AI 초기화
                await UpdateProgressAsync("Initializing Windows AI services...", "Step 1 of 6", 16);

                // Update progress - 비디오 속성 로드
                await UpdateProgressAsync("Loading video properties...", "Step 2 of 6", 33);

                // Update progress - 프레임 추출
                await UpdateProgressAsync("Extracting video frames...", "Step 3 of 6", 50);

                // Update progress - AI 분석
                await UpdateProgressAsync("Analyzing frames with Windows AI...", "Step 4 of 6", 66);

                // Update progress - 특이점 감지
                await UpdateProgressAsync("Detecting anomalies and patterns...", "Step 5 of 6", 83);

                // 고급 분석 수행
                var result = await _advancedAnalysisService.AnalyzeVideoAsync(selectedFile);
                
                // Update progress - 요약 생성
                await UpdateProgressAsync("Generating detailed summary...", "Step 6 of 6", 100);

                // 결과 표시
                await ShowAdvancedAnalysisResultsAsync(result);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Analysis failed: {ex.Message}");
            }
            finally
            {
                isAnalyzing = false;
            }
        }

        private async Task UpdateProgressAsync(string text, string details, double value)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressText.Text = text;
                ProgressDetails.Text = details;
                ProgressBar.Value = value;
            });
            
            // 실제 분석 시간을 시뮬레이션하기 위한 지연
            await Task.Delay(500);
        }

        private async Task ShowAdvancedAnalysisResultsAsync(AdvancedVideoAnalysisResult result)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // Hide progress section
                ProgressSection.Visibility = Visibility.Collapsed;
                
                // Show results section
                ResultsSection.Visibility = Visibility.Visible;
                
                // Update result text
                var resultText = GenerateResultText(result);
                SummaryText.Text = resultText;
                
                // Update file info
                FileNameText.Text = result.FileName;
                FileSizeText.Text = $"{result.FileSize / (1024 * 1024):F1} MB";
                
                // Show copy button
                CopyButton.Visibility = Visibility.Visible;
            });
        }

        private async Task ShowErrorAsync(string message)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // Hide progress and results sections
                ProgressSection.Visibility = Visibility.Collapsed;
                ResultsSection.Visibility = Visibility.Collapsed;
                
                // Show error section
                ErrorSection.Visibility = Visibility.Visible;
                ErrorText.Text = message;
                
                // Show retry button
                RetryButton.Visibility = Visibility.Visible;
            });
        }

        private string GenerateResultText(AdvancedVideoAnalysisResult result)
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine($"📹 비디오 분석 완료: {result.FileName}");
            sb.AppendLine($"⏱️ 분석 시간: {result.AnalysisDuration.TotalSeconds:F1}초");
            sb.AppendLine($"📊 분석된 프레임: {result.TotalFramesAnalyzed}개");
            sb.AppendLine();
            
            sb.AppendLine("🔍 주요 이벤트:");
            foreach (var priorityEvent in result.PriorityEvents.Take(5))
            {
                var severityIcon = priorityEvent.Severity switch
                {
                    EventSeverity.High => "🔴",
                    EventSeverity.Medium => "🟡",
                    EventSeverity.Low => "🟢"
                };
                
                sb.AppendLine($"{severityIcon} {priorityEvent.Timestamp:hh\\:mm} - {priorityEvent.Description}");
            }
            
            sb.AppendLine();
            sb.AppendLine("📝 상세 요약:");
            sb.AppendLine(result.DetailedSummary);
            
            return sb.ToString();
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
