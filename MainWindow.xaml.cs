using Ookii.Dialogs.Wpf;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace FileCopy
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

    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _authorInfo = "© 2023 RQ - 版本 1.0.3";

        public string AuthorInfo
        {
            get => _authorInfo;
            set
            {
                _authorInfo = value;
                OnPropertyChanged(nameof(AuthorInfo));
            }
        }
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
            DataContext = this;
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

            ScannedFilesCount.Text = $"扫描文件总数：{fileList.Count}";
            HideOverlay();

            filteredList = new List<EasyFile>();
            FilesListView.ItemsSource = filteredList;
            DisplayPage(1);
        }

        private void FilterFiles_Click(object sender, RoutedEventArgs e)
        {
            if (fileList.Count() == 0)
            {
                MessageBox.Show("未扫描或扫描出文件数量为0.");
                return;
            }
            filteredList = fileList.Where(fileInfo => (string.IsNullOrWhiteSpace(FileNamePattern.Text) || fileInfo.Name.Contains(FileNamePattern.Text)) &&
            (!CreatedAfterPicker.SelectedDate.HasValue || fileInfo.CreationTime?.Date == CreatedAfterPicker.SelectedDate.Value.Date) &&
            (!ModifiedAfterPicker.SelectedDate.HasValue || fileInfo.LastWriteTime?.Date == ModifiedAfterPicker.SelectedDate.Value.Date))
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
            int totalFiles = filteredList.Count;
            if (totalFiles == 0)
            {
                return;
            }

            ShowOverlay("正在复制...");

            int copiedFiles = 0;
            int successCount = 0;
            int failCount = 0;
            await Task.Run(() =>
            {
                foreach (var file in filteredList)
                {
                    var destFile = Path.Combine(targetFolderPath, file.Name);
                    try
                    {
                        File.Copy(file.FullName, destFile, true);
                        file.IsCoped = "是";
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        file.IsCoped = $"否：{ex.Message}";
                        failCount++;
                    }

                    copiedFiles++;
                    UpdateProgress(copiedFiles, totalFiles);
                }
            });
            HideOverlay();
            MessageBox.Show($"复制完成，成功：{successCount} 失败：{failCount}");
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
                // 尝试打开文件
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
