using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Text.Json;

namespace PhotoImporter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly OpenFolderDialog _openFolderDialog = new OpenFolderDialog();
        private PhotoMover? _photoMover = null;
        private readonly SynchronizationContext _uiThread;
        private readonly static string s_settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhotoImporter", "PhotoImportSettings.json");

        public MainWindow()
        {
            InitializeComponent();
            btnCancel.IsEnabled = false;
            if (SynchronizationContext.Current == null)
            {
                throw new Exception("Null sync thread");
            }
            _uiThread = SynchronizationContext.Current;

            try
            {
                FileInfo settingsInfo = new FileInfo(s_settingsFile);
                if (settingsInfo.Directory?.Exists != true && !string.IsNullOrWhiteSpace(settingsInfo.DirectoryName))
                {
                    Directory.CreateDirectory(settingsInfo.DirectoryName);
                }
            }
            catch { }

            if (File.Exists(s_settingsFile))
            {
                try
                {
                    string settingsFileValue = File.ReadAllText(s_settingsFile);
                    ImportSettings? fileSettings = JsonSerializer.Deserialize<ImportSettings>(settingsFileValue);
                    if (fileSettings != null)
                    {
                        txtImportFolder.Text = fileSettings.FromFolder;
                        txtExportFolder.Text = fileSettings.ToFolder;
                        chkCreateSubFolders.IsChecked = fileSettings.CreateSubFolders;
                        chkDeleteOriginals.IsChecked = fileSettings.DeleteFilesAfterImport;
                        chkSkipDuplicates.IsChecked = fileSettings.SkipDuplicates;
                    }
                }
                catch
                {
                }
            }
        }

        private void btnImportFolderSelect_Click(object sender, RoutedEventArgs e)
        {
            _openFolderDialog.Title = "Please select which folder to import from";
            if (_openFolderDialog.ShowDialog() == true)
            {
                txtImportFolder.Text = _openFolderDialog.FolderName;
            }
        }

        private void btnExportFolderSelect_Click(object sender, RoutedEventArgs e)
        {
            _openFolderDialog.Title = "Please select which folder to import to";
            if (_openFolderDialog.ShowDialog() == true)
            {
                txtExportFolder.Text = _openFolderDialog.FolderName;
            }
        }

        private void btnImport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtImportFolder.Text))
            {
                MessageBox.Show("You must select a folder to import from", "Import folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtImportFolder.Focus();
                return;
            }
            if (!Directory.Exists(txtImportFolder.Text))
            {
                MessageBox.Show("The selected import folder does not exist", "Import folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtImportFolder.Focus();
                return;
            }          
            if (string.IsNullOrWhiteSpace(txtExportFolder.Text))
            {
                MessageBox.Show("You must select a folder to import to", "Export folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtExportFolder.Focus();
                return;
            }
            if (!Directory.Exists(txtExportFolder.Text))
            {
                MessageBox.Show("The selected export folder does not exist", "Export folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtExportFolder.Focus();
                return;
            }

            if (MessageBox.Show("Are you sure you want to import with these settings?", "Are you sure?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                txtOutput.Text = "";
                ImportSettings importSettings = new ImportSettings()
                {
                    FromFolder = txtImportFolder.Text,
                    ToFolder = txtExportFolder.Text,
                    CreateSubFolders = chkCreateSubFolders.IsChecked == true,
                    DeleteFilesAfterImport = chkDeleteOriginals.IsChecked == true,
                    SkipDuplicates = chkSkipDuplicates.IsChecked == true
                };

                try
                {
                    string jsonSettings = JsonSerializer.Serialize(importSettings);
                    File.WriteAllText(s_settingsFile, jsonSettings);
                }
                catch
                {
                }

                _photoMover = new PhotoMover(importSettings);
                btnImport.IsEnabled = false;
                btnCancel.IsEnabled = true;
                _photoMover.MessageLogged += _photoMover_MessageLogged;
                Thread importThread = new Thread(() =>
                {
                    _photoMover.MovePhotos();
                    while (_photoMover.Running)
                    {
                        Thread.Sleep(100);
                    }
                    _uiThread.Send(d =>
                    {
                        btnCancel.IsEnabled = false;
                        btnImport.IsEnabled = true;
                    }, null);
                    _photoMover.MessageLogged -= _photoMover_MessageLogged;
                    _photoMover = null;
                });
                importThread.Start();
            }
        }

        private void _photoMover_MessageLogged(object? sender, LogMessage e)
        {
            _uiThread.Send(d =>
            {
                txtOutput.Text += $"{e.MessageTime} - {e.Message}{Environment.NewLine}";
                txtOutput.ScrollToEnd();
            }, null);
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_photoMover?.Running == true)
            {
                _photoMover.Cancel();
            }
        }
    }
}