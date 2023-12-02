using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ookii.Dialogs.Wpf;

namespace EasyPicOrganizer
{
    class EasyFile : INotifyPropertyChanged
    {

        private string _isCoped { get; set; } = string.Empty;

        public FileInfo? Info { get; set; }
        public string IsCoped
        {

            get => _isCoped;
            set
            {
                if (_isCoped != value)
                {
                    _isCoped = value;
                    OnPropertyChanged(nameof(IsCoped));
                }
            }
        }

        public string Name
        {
            get
            {
                return Info?.Name == null ? string.Empty : Info.Name;
            }
        }

        public string FullName
        {
            get
            {
                return Info?.FullName == null ? string.Empty : Info.FullName;
            }
        }
        public DateTime? CreationTime
        {
            get
            {
                return Info?.CreationTime;
            }
        }
        public DateTime? LastWriteTime
        {
            get
            {
                return Info?.LastWriteTime;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }

    public partial class MainWindow : Window
    {
        private string sourceFolderPath = string.Empty;
        private string targetFolderPath = string.Empty;
        private List<EasyFile> fileList = new List<EasyFile>();
        private List<EasyFile> filteredList = new List<EasyFile>();
        private const int ItemsPerPage = 20; // 每页显示数量
        private int currentPage = 1; // 当前页码
        private List<EasyFile> currentPagedItems = new List<EasyFile>(); // 当前页的项目列表

        public MainWindow()
        {
            InitializeComponent();
        }

        private void SelectSourceFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();
            if (dialog.ShowDialog() == true)
            {
                sourceFolderPath = dialog.SelectedPath;
                SourceFolderPath.Text = sourceFolderPath;
                SourceFolderPath.MouseDown += (s, args) => OpenFolder(sourceFolderPath);
            }
        }

        private void SelectTargetFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();
            if (dialog.ShowDialog() == true)
            {
                targetFolderPath = dialog.SelectedPath;
                TargetFolderPath.Text = targetFolderPath;
                TargetFolderPath.MouseDown += (s, args) => OpenFolder(targetFolderPath);
            }
        }

        private async void ScanFiles_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(sourceFolderPath))
            {
                MessageBox.Show("请先选择源文件夹。");
                return;
            }
            ShowOverlay("正在扫描...", false);

            await Task.Run(() =>
            {
                fileList.Clear();
                var files = Directory.EnumerateFiles(sourceFolderPath, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    fileList.Add(new EasyFile() { Info = fileInfo });
                }

            });

            ScannedFilesCount.Text = $"扫描图片总数：{fileList.Count}";
            HideOverlay();

            filteredList = new List<EasyFile>();
            FilesListView.ItemsSource = filteredList;
            DisplayPage(1);
        }

        private void FilterFiles_Click(object sender, RoutedEventArgs e)
        {
            if (fileList.Count() == 0)
            {
                MessageBox.Show("未扫描或扫描出图片数量为0.");
                return;
            }
            filteredList = fileList.Where(fileInfo =>
                (string.IsNullOrWhiteSpace(FileNamePattern.Text) || fileInfo.Name.Contains(FileNamePattern.Text)) &&
                (!CreatedAfterPicker.SelectedDate.HasValue || fileInfo.CreationTime > CreatedAfterPicker.SelectedDate.Value) &&
                (!ModifiedAfterPicker.SelectedDate.HasValue || fileInfo.LastWriteTime > ModifiedAfterPicker.SelectedDate.Value))
                .ToList();
            DisplayPage(1);
        }

        private void DisplayPage(int pageNumber)
        {
            int totalItems = filteredList.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)ItemsPerPage);

            currentPage = pageNumber;

            if (currentPage < 1) currentPage = 1;
            else if (currentPage > totalPages) currentPage = totalPages;

            int startIndex = (currentPage - 1) * ItemsPerPage;
            int endIndex = Math.Min(startIndex + ItemsPerPage - 1, totalItems - 1);

            currentPagedItems = filteredList.Skip(startIndex).Take(ItemsPerPage).ToList();
            FilesListView.ItemsSource = currentPagedItems;
            PageInfo.Text = $"页码: {currentPage} / {totalPages}";
        }

        // 上一页和下一页按钮的点击事件处理
        private void PreviousPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage > 1)
            {
                DisplayPage(currentPage - 1);
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = (int)Math.Ceiling(fileList.Count / (double)ItemsPerPage);
            if (currentPage < totalPages)
            {
                DisplayPage(currentPage + 1);
            }
        }

        private void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            DisplayPage(1);
        }
        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = (int)Math.Ceiling(fileList.Count / (double)ItemsPerPage);
            DisplayPage(totalPages);
        }


        private async void CopyFiles_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(targetFolderPath))
            {
                MessageBox.Show("请先选择目标文件夹。");
                return;
            }
            if (targetFolderPath == sourceFolderPath)
            {
                MessageBox.Show("源文件夹和目标文件夹不应该相同。");
                return;
            }
            ShowOverlay("正在复制...");

            int totalFiles = filteredList.Count;
            int copiedFiles = 0;
            await Task.Run(() =>
            {
                foreach (var file in filteredList)
                {
                    var destFile = Path.Combine(targetFolderPath, file.Name);
                    try
                    {
                        File.Copy(file.FullName, destFile, true);
                        file.IsCoped = "是";
                    }
                    catch (Exception ex)
                    {
                        file.IsCoped = $"否：{ex.Message}";
                    }

                    copiedFiles++;
                    UpdateProgress(copiedFiles, totalFiles);
                }
            });
            HideOverlay();
        }

        private void ShowOverlay(string message, bool progress = true)
        {
            LoadingText.Text = message;
            Progress.Visibility = progress ? Visibility.Visible : Visibility.Hidden;
            Overlay.Visibility = Visibility.Visible;
        }

        private void HideOverlay()
        {
            Overlay.Visibility = Visibility.Collapsed;
        }

        private void UpdateProgress(int value, int max)
        {
            Dispatcher.Invoke(() =>
            {
                Progress.Value = (double)value / max * 100;
            });
        }

        private void FilesListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (FilesListView.SelectedItem is EasyFile selectedFile)
            {
                // 尝试打开图片
                try
                {
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = selectedFile.FullName,
                        UseShellExecute = true,
                        Verb = "open"
                    };
                    System.Diagnostics.Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法打开文件,{ex.Message}");
                }
            }
        }

        private void OpenFolder(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                System.Diagnostics.Process.Start("explorer.exe", path);
            }
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is EasyFile fileItem)
            {
                filteredList.Remove(fileItem);
                DisplayPage(currentPage);
            }
        }

        private void RemoveCoped_Click(object sender, RoutedEventArgs e)
        {
            if (filteredList == null || filteredList.Count() < 1)
            {
                return;
            }

            for (int i = filteredList.Count - 1; i >= 0; i--)
            {
                var item = filteredList[i];
                if (item.IsCoped == "是")
                {
                    filteredList.Remove(item);
                }
            }
            DisplayPage(1);
        }

    }
}
