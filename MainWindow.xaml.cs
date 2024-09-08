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
using System.Runtime.InteropServices;
using System.Text;

namespace FileSorter
{
    public partial class MainWindow : Window
    {
        private string sourceFolder = string.Empty;
        private string destinationFolder = string.Empty;
        private string iniFilePath;
        public ObservableCollection<FileItem> Files { get; set; }
        
        public MainWindow()
        {
            InitializeComponent();
            iniFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileSorter.ini");
            Files = new ObservableCollection<FileItem>();
            dataGridFiles.ItemsSource = Files;
            btnProcessFiles.IsEnabled = false; // Disable the button initially
            LoadSettings();
        }
        
        private void LoadSettings()
        {
            if (File.Exists(iniFilePath))
            {
                sourceFolder = ReadIniFile("Folders", "SourceFolder", string.Empty);
                destinationFolder = ReadIniFile("Folders", "DestinationFolder", string.Empty);
                LoadFiles();
                CheckIfReadyToProcess();
            }
        }
        
        private void SaveSettings()
        {
            WriteIniFile("Folders", "SourceFolder", sourceFolder);
            WriteIniFile("Folders", "DestinationFolder", destinationFolder);
        }
        
        private string ReadIniFile(string section, string key, string defaultValue)
        {
            StringBuilder temp = new StringBuilder(255);
            int i = GetPrivateProfileString(section, key, defaultValue, temp, 255, iniFilePath);
            return temp.ToString();
        }
        
        private void WriteIniFile(string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, iniFilePath);
        }
        
        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);
        
        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

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
            SaveSettings();
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
            SaveSettings();
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

            this.Dispatcher.Invoke(() =>
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

                        bool isCopyFilesChecked = false;
                        this.Dispatcher.Invoke(() =>
                        {
                            isCopyFilesChecked = chkCopyFiles.IsChecked == true;
                        });
                        
                        if (isCopyFilesChecked)
                        {
                            File.Copy(sourceFilePath, destinationFilePath, true);
                        }
                        else
                        {
                            File.Move(sourceFilePath, destinationFilePath);
                        }

                        // Update progress on the UI thread
                        this.Dispatcher.Invoke(() => progressBar.Value = i + 1);
                    }
                    catch (Exception ex)
                    {
                        this.Dispatcher.Invoke(() => MessageBox.Show($"Error processing file {fileItem.FileName}: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                }
            });

            this.Dispatcher.Invoke(() =>
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
