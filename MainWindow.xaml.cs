using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms; // Ensure correct reference
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox; // To avoid conflict

namespace MediaSorter
{
    public partial class MainWindow : Window
    {
        private string sourceFolder = string.Empty;
        private string destinationFolder = string.Empty;
        public ObservableCollection<FileItem> Files { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            Files = new ObservableCollection<FileItem>();
            dataGridFiles.ItemsSource = Files;
            btnProcessFiles.IsEnabled = false; // Disable the button initially
        }

        private void btnSelectSource_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    sourceFolder = dialog.SelectedPath;
                    LoadFiles();
                    CheckIfReadyToProcess();
                }
            }
        }

        private void btnSelectDestination_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    destinationFolder = dialog.SelectedPath;
                    UpdateTargetFolders();
                    CheckIfReadyToProcess();
                }
            }
        }

        private void LoadFiles()
        {
            Files.Clear();

            if (!string.IsNullOrEmpty(sourceFolder))
            {
                var files = Directory.GetFiles(sourceFolder);

                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string targetFolder = GetTargetFolder(fileName);
                    string extension = Path.GetExtension(file).ToLower();

                    Files.Add(new FileItem
                    {
                        FileName = fileName,
                        TargetFolder = targetFolder,
                        Process = extension == ".mkv" || extension == ".mp4"
                    });
                }
            }
        }

        private void UpdateTargetFolders()
        {
            foreach (var fileItem in Files)
            {
                fileItem.TargetFolder = GetTargetFolder(fileItem.FileName);
            }

            dataGridFiles.ItemsSource = null;
            dataGridFiles.ItemsSource = Files; // Refresh the DataGrid binding
        }

        private string GetTargetFolder(string fileName)
        {
            // Extract the series name from the file name using regex
            var match = Regex.Match(fileName, @"^(?<seriesName>.+?)\s+s\d+e\d+", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string seriesName = match.Groups["seriesName"].Value;
                return Path.Combine(destinationFolder, seriesName);
            }

            // If the file name does not match the expected pattern, return the destination folder directly
            return destinationFolder;
        }

        private async void btnProcessFiles_Click(object sender, RoutedEventArgs e)
        {
            var filesToProcess = Files.Where(f => f.Process).ToList();
            int totalFiles = filesToProcess.Count;

            Dispatcher.Invoke(() =>
            {
                progressBar.Visibility = Visibility.Visible;
                progressBar.Minimum = 0;
                progressBar.Maximum = totalFiles;
                progressBar.Value = 0;
            });

            await Task.Run(() =>
            {
                for (int i = 0; i < totalFiles; i++)
                {
                    var fileItem = filesToProcess[i];
                    string sourceFilePath = Path.Combine(sourceFolder, fileItem.FileName);
                    string targetFolder = fileItem.TargetFolder;

                    try
                    {
                        if (!Directory.Exists(targetFolder))
                        {
                            Directory.CreateDirectory(targetFolder);
                        }

                        string destinationFilePath = Path.Combine(targetFolder, fileItem.FileName);

                        if (chkCopyFiles.IsChecked == true)
                        {
                            File.Copy(sourceFilePath, destinationFilePath, true);
                        }
                        else
                        {
                            File.Move(sourceFilePath, destinationFilePath);
                        }

                        // Update progress on the UI thread
                        Dispatcher.Invoke(() => progressBar.Value = i + 1);
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => MessageBox.Show($"Error processing file {fileItem.FileName}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                }
            });

            Dispatcher.Invoke(() =>
            {
                progressBar.Visibility = Visibility.Collapsed;
                MessageBox.Show("Files processed successfully!");
                LoadFiles(); // Refresh the list after processing.
            });
        }

        private void CheckIfReadyToProcess()
        {
            btnProcessFiles.IsEnabled = !string.IsNullOrEmpty(sourceFolder) && !string.IsNullOrEmpty(destinationFolder);
        }
    }

    public class FileItem
    {
        public string FileName { get; set; } = string.Empty;
        public string TargetFolder { get; set; } = string.Empty;
        public bool Process { get; set; }
    }
}
