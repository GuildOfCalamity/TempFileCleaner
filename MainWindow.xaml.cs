using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using Microsoft.Win32;

namespace TempFileCleaner
{
    /// <summary>
    /// App for testing code snipets
    /// </summary>
    public partial class MainWindow : Window
    {
        #region [Properties]
        int _totalFail = 0;
        int _totalSuccess = 0;
        int _totalExclude = 0;
        long _totalBytes = 0;
        double sfcCount = 0;
        double _pbUpperMaximum = 18000; // user could have many files, or only a few, so estimate a ceiling to start
        double _cfgWinHeight = 750;
        double _cfgWinWidth = 1100;
        int _cfgMonthAge = -12;
        bool _cfgReportOnly = false;
        bool _cfgFirstRun = true;
        bool _UAC = false;
        string[] _cfgExcludeFolders = null;
        CancellationTokenSourceEnhanced _cts;

        public event EventHandler<List<FileResult>> CleanupCompleted;
        public event EventHandler<Exception> CleanupError;
        // Add any folder names you want to ignore here.
        readonly string[] IgnoredFolders =
        {
            "WinSAT",              // Windows System Assessment Tool (benchmarks system hardware—CPU, memory, disk, and graphics—to calculate the Windows Experience Index score)
            "WER",                 // Windows Error Reporting
            "Diagnostics",         // Windows Diagnostics logs
            "AppCrash",            // Common Application crash dumps
            "$PatchCache$",        // Windows Installer cache
            "SoftwareDistribution" // Windows Update temp
        };

        // Add any folder names (with wildcards) you want to ignore here.
        readonly string[] IgnoredWildcardFolders =
        {
            "WinSAT",
            "WER",
            "Diagnostics",
            "Diag*",
            "AppCrash",
            "$PatchCache$",
            "SoftwareDistribution",
            "*Cache*"
        };

        #endregion

        public MainWindow()
        {
            InitializeComponent();
            #region [Event Hooks]
            this.Loaded += OnMainWindowLoaded;
            this.Closing += OnMainWindowClosing;
            CleanupError += OnCleanupError;
            //ObservableCollectionExample.CollectionChanged += FileLog_CollectionChanged;
            ConfigManager.OnError += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    WpfMessageBox.Show($"ConfigManager error:{Environment.NewLine}{e.Message}", false, true, owner: this);
                });
            };
            
            MethodTimer.LogHandler = (msg) => 
            { 
                Debug.WriteLine(msg);
                ConfigManager.Set("ExecutionTime", msg);
            };
            #endregion
        }


        #region [Events]
        /// <summary>
        /// For <see cref="ObservableCollection{T}"/> event if MVVM pattern is adopted.
        /// </summary>
        /// <param name="sender"><see cref="ObservableCollection{T}"/></param>
        /// <param name="e"><see cref="System.Collections.Specialized.NotifyCollectionChangedAction"/></param>
        void FileLog_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                lstFiles.ScrollIntoView(e.NewItems[e.NewItems.Count - 1]);
            }
        }

        void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            this.Title = $"{App.GetCurrentAssemblyName()} v{App.GetCurrentAssemblyVersion()}";
            spProgress.Visibility = Visibility.Collapsed;
            btnCancel.Visibility = Visibility.Hidden;
            cbReport.IsChecked = _cfgReportOnly = ConfigManager.Get("ReportOnly", defaultValue: true);
            _cfgMonthAge = ConfigManager.Get("MonthAge", defaultValue: -12);
            tbMonths.Text = $"{_cfgMonthAge}";
            // Read folder array
            _cfgExcludeFolders = ConfigManager.Get<string[]>("ExcludeFolders");
            // Or use alternative method
            //_cfgExcludeFolders = ConfigManager.GetArray("ExcludeFolders");

            //var savedCodes = ConfigManager.Get<List<int>>("WarningCodes") ?? new List<int>();

            this.Width = _cfgWinWidth = ConfigManager.Get("WindowWidth", defaultValue: 1100.0);
            this.Height = _cfgWinHeight = ConfigManager.Get("WindowHeight", defaultValue: 750.0);
            _cfgFirstRun = ConfigManager.Get("FirstRun", defaultValue: true);
            if (_cfgFirstRun)
            {
                WpfMessageBox.Show($"Welcome to {App.GetCurrentAssemblyName()} v{App.GetCurrentAssemblyVersion()} !{Environment.NewLine}" +
                    $"This app will identify and delete old temp files from your system.{Environment.NewLine}" +
                    $"You can configure the age threshold and choose to run in report-only mode if you want to review files before deletion.{Environment.NewLine}" +
                    $"Use the cancel button to stop the process at any time.{Environment.NewLine}" +
                    $"Items marked with an orange dot will NOT be removed,{Environment.NewLine}items marked with a green dot WILL be removed."
                    , false, false, fontSize: 13.0,  owner: this);
            }
        }

        void OnMainWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ConfigManager.Set("WindowWidth", this.Width >= 300 ? this.Width : 500);
            ConfigManager.Set("WindowHeight", this.Height >= 200 ? this.Height : 300);
            ConfigManager.Set("ReportOnly", (bool)cbReport.IsChecked);
            ConfigManager.Set("FirstRun", false);
            if (!int.TryParse(tbMonths.Text, out int months)) { months = -12; }
            ConfigManager.Set("MonthAge", months);
            if (_cfgExcludeFolders != null)
                ConfigManager.Set("ExcludeFolders", _cfgExcludeFolders);
            else
                ConfigManager.Set("ExcludeFolders", IgnoredFolders); // default

            // Testing array save/load
            //List<int> codes = new List<int> { 101, 202, 303, 404, 505 };
            //ConfigManager.Set("WarningCodes", codes);
        }

        void btnCancelClick(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }

        void OnCleanupError(object sender, Exception e)
        {
            Dispatcher.Invoke(() =>
            {
                var result = WpfMessageBox.Show($"Cleanup error:{Environment.NewLine}{e.Message}", true, true, owner: this);
                // Check for cancel click
                if (result.HasValue && !result.Value)
                    _cts?.Cancel();
            });
        }

        async void btnCleanupClick(object sender, RoutedEventArgs e)
        {
            double totalCount = 0;
            _totalBytes = 0;
            _cts = new CancellationTokenSourceEnhanced(TimeSpan.FromMinutes(5)); // reset our token
            lstFiles.Items.Clear();
            btnStart.Visibility = Visibility.Hidden;
            cbReport.IsEnabled = tbMonths.IsEnabled = false;
            spProgress.Visibility = btnCancel.Visibility = pbCleaning.Visibility = Visibility.Visible;
            pbCleaning.Minimum = 0;
            pbCleaning.Maximum = _pbUpperMaximum;
            if (!int.TryParse(tbMonths.Text, out int months)) { _cfgMonthAge = -12; }
            _cfgReportOnly = (bool)cbReport.IsChecked;

            // Deleted Files
            var progressSuccess = new Progress<FileResult>(fr =>
            {
                lstFiles.Items.Add(fr);
                totalCount++;
                _totalSuccess++;
                if (totalCount > pbCleaning.Maximum)
                    pbCleaning.Maximum = totalCount + (_pbUpperMaximum / 8.0);
                pbCleaning.Value = totalCount;
            });
            // Couldn't Remove
            var progressFailed = new Progress<FileResult>(fr =>
            {
                lstFiles.Items.Add(fr);
                totalCount++;
                _totalFail++;
                if (totalCount > pbCleaning.Maximum)
                    pbCleaning.Maximum = totalCount + (_pbUpperMaximum / 8.0);
                pbCleaning.Value = totalCount;
            });
            // Excluded Files
            var progressExclude = new Progress<FileResult>(fr =>
            {
                lstFiles.Items.Add(fr);
                totalCount++;
                _totalExclude++;
                if (totalCount > pbCleaning.Maximum)
                    pbCleaning.Maximum = totalCount + (_pbUpperMaximum / 8.0);
                pbCleaning.Value = totalCount;
            });
            // Delegate for SFC command
            var progressSfc = new Progress<FileResult>(fr =>
            {
                Dispatcher.Invoke(() => { lstFiles.Items.Add(fr); });
                if (sfcCount == 0)
                    sfcCount = pbCleaning.Value;
                sfcCount++;
                if (sfcCount > pbCleaning.Maximum)
                    pbCleaning.Maximum = sfcCount + (_pbUpperMaximum / 8.0);
                pbCleaning.Value = sfcCount;
            });

            try
            {
                //List<FileResult> allFailed = await CleanOldTempFilesAsync(_cfgMonthAge, _cfgReportOnly, _cts.Token, progressFailed, progressSuccess, progressExclude);
                var allTimed = MethodTimer.TimeAsync(() => CleanOldTempFilesAsync(_cfgMonthAge, _cfgReportOnly, _cts.Token, progressFailed, progressSuccess, progressExclude), "CleanOldTempFilesAsync");
                var tsk = await allTimed;
            }
            catch (OperationCanceledException)
            {
                WpfMessageBox.Show("Cleanup was cancelled by the user.", false, true, owner: this);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Cleanup: {ex.Message}", false, true, owner: this);
            }
            finally
            {
                await Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (lstFiles.Items.Count > 0)
                    {
                        var lastItem = lstFiles.Items[lstFiles.Items.Count - 1];
                        lstFiles.ScrollIntoView(lastItem);
                    }
                }));

                if (_cfgReportOnly)
                {
                    WpfMessageBox.Show($"Total files that could be cleaned: {_totalSuccess}{Environment.NewLine}" +
                        $"Total files skipped due to error: {_totalFail}{Environment.NewLine}" +
                        $"Total files excluded: {_totalExclude}{Environment.NewLine}" +
                        $"Total file size: {_totalBytes.ToFileSize()}",
                        false, false, owner: this);
                    if (_UAC)
                    {
                        var dskscn = WpfMessageBox.Show($"Would you like to run the local system's file checker?{Environment.NewLine}{Environment.NewLine}Time remaining on your current token: {_cts.TimeRemaining.ToReadableTime()}", true, false, owner: this);
                        if (dskscn.HasValue && dskscn.Value)
                        {
                            var result = await MethodTimerEx.TimeAsync(async () => await RunSystemFileChecker(_cts.Token, progressSfc));
                            WpfMessageBox.Show($"SFC process finished.{Environment.NewLine}SFC attempt took {result.ToReadableTime()}.", false, false, owner: this);
                        }
                    }
                }
                else
                {
                    WpfMessageBox.Show($"Total files cleaned: {_totalSuccess}{Environment.NewLine}" +
                        $"Total files skipped: {_totalFail}{Environment.NewLine}" +
                        $"Total files excluded: {_totalExclude}{Environment.NewLine}" +
                        $"Total file size: {_totalBytes.ToFileSize()}",
                        false, false, owner: this);
                    if (_UAC)
                    {
                        var dskcln = WpfMessageBox.Show($"Would you like to run the local system's DiskClean tool?", true, false, owner: this);
                        if (dskcln.HasValue && dskcln.Value)
                        {
                            PreConfigureDiskCleanup();
                            RunDiskCleanup(_cts.Token);
                            RunSystemFileChecker(_cts.Token, progressSfc);
                        }
                    }
                }

                _cts?.Dispose();
                pbCleaning.Value = 0;
                cbReport.IsEnabled = tbMonths.IsEnabled = true;
                btnStart.Visibility = Visibility.Visible;
                btnCancel.Visibility = Visibility.Hidden;
                pbCleaning.Visibility = spProgress.Visibility = Visibility.Collapsed;
            }
        }

        void OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Get the ListBox that triggered the event
            var lb = sender as ListBox;
            // Ensure an item was actually selected (not just deselected)
            if (lb?.SelectedItem != null)
            {
                // Cast the selected item to its type (e.g. file path)
                var selectedFile = lb.SelectedItem as FileResult;
                // You could also trigger your cleanup logic or open a preview here
                Debug.WriteLine($"[INFO] User selected: {selectedFile?.FilePath}");
            }
        }
        #endregion

        #region [Work Methods]
        /// <summary>
        /// Identifies and deletes temp files older than <paramref name="months"/>.
        /// Returns a list of files that could not be deleted (in use, access denied, etc).
        /// </summary>
        public async Task<List<FileResult>> CleanOldTempFilesAsync(int months, bool reportOnly, CancellationToken ct, IProgress<FileResult> progressFail = null, IProgress<FileResult> progressSuccess = null, IProgress<FileResult> progressExclude = null)
        {
            if (months >= 0)
                months *= -1;

            return await Task.Run(async () =>
            {
                var failedFiles = new List<FileResult>();
                DateTime cutoffDate = DateTime.Now.AddMonths(months);
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string[] tempFolders =
                {
                    Path.GetTempPath(),
                    Path.Combine(userProfile, "Downloads"),
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    Environment.GetEnvironmentVariable("TMP"),
                    Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.User),
                    Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.Machine),
                    @"C:\Windows\Temp"
                };

                // List of folder fragments to ignore
                var excludeFolders = new List<string>
                {
                    "\\Program Files\\",
                    "\\Program Files (x86)\\",
                    "\\AppData\\Roaming\\",
                    "\\inetpub\\",
                    "\\Servicing\\LCU\\",
                    "\\WinSAT\\",             // System Assessment Tool (%systemroot%\performance\winsat\datastore)
                    "\\WinSxS\\",             // Windows Updates
                    "\\System32\\" };

                // De-dupe the folder list
                var filtered = tempFolders.Select(p => p.ToLower().TrimEnd('\\')).Distinct().ToList();
                foreach (string folder in filtered)
                {
                    if (!Directory.Exists(folder) || ct.IsCancellationRequested)
                        continue;

                    try
                    {
                        foreach (string filePath in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                        {
                            if (ct.IsCancellationRequested)
                                break;

                            // Does the file path contain any excluded folder names?
                            bool isExcluded = excludeFolders.Any(exf => filePath.ToLower().Contains(exf.ToLower()));
                            if (isExcluded)
                            {
                                //Debug.WriteLine($"[INFO] Skipping excluded file/folder \"{filePath}\"");
                                progressExclude?.Report(new FileResult { FilePath = filePath, IsSuccess = false });
                                try
                                {
                                    FileInfo fileInfo = new FileInfo(filePath);
                                    if (fileInfo.LastWriteTime < cutoffDate)
                                    {
                                        // Include excluded files in the total size reported.
                                        _totalBytes += fileInfo.Length;
                                    }
                                }
                                catch { }
                                continue;
                            }

                            try
                            {
                                FileInfo fileInfo = new FileInfo(filePath);
                                if (fileInfo.LastWriteTime < cutoffDate)
                                {
                                    _totalBytes += fileInfo.Length;
                                    if (reportOnly)
                                    {
                                        progressSuccess?.Report(new FileResult { FilePath = filePath, IsSuccess = true });
                                    }
                                    else
                                    {
                                        // Some downloaded archives/files could be set as read-only by default.
                                        if (fileInfo.IsReadOnly)
                                            fileInfo.IsReadOnly = false;

                                        fileInfo.Delete();
                                        progressSuccess?.Report(new FileResult { FilePath = filePath, IsSuccess = true });
                                    }
                                    //await Task.Delay(1); // Delays process, but smooths out the ProgBar jumpiness
                                }
                            }
                            catch
                            {
                                failedFiles.Add(new FileResult { FilePath = filePath, IsSuccess = false });
                                progressFail?.Report(new FileResult { FilePath = filePath, IsSuccess = false });
                                //await Task.Delay(1); // Delays process, but smooths out the ProgBar jumpiness
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        CleanupError?.Invoke(this, ex);
                    }
                    await Task.Delay(10);
                }
                CleanupCompleted?.Invoke(this, failedFiles);
                return failedFiles;

            }, ct);
        }

        /// <summary>
        /// Identifies and deletes temp files older than <paramref name="months"/>.
        /// Returns a list of files that could not be deleted (in use, access denied, etc).
        /// </summary>
        public async Task<List<string>> CleanOldTempFilesAsyncPrevious(int months, bool reportOnly, CancellationToken ct, IProgress<string> progressFail = null, IProgress<string> progressSuccess = null)
        {
            if (months >= 0)
                months *= -1;

            return await Task.Run(async () =>
            {
                List<string> failedFiles = new List<string>();
                DateTime cutoffDate = DateTime.Now.AddMonths(months);
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string[] tempFolders =
                {
                    Path.GetTempPath(),
                    Path.Combine(userProfile, "Downloads"),
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    Environment.GetEnvironmentVariable("TMP"),
                    Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.User),
                    Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.Machine),
                    @"C:\Windows\Temp"
                };
                var filtered = tempFolders.Select(p => p.ToLower().TrimEnd('\\')).Distinct().ToList();
                foreach (string folder in filtered)
                {
                    if (!Directory.Exists(folder) || ct.IsCancellationRequested)
                        continue;

                    try
                    {
                        foreach (string filePath in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                        {
                            if (ct.IsCancellationRequested)
                                break;

                            if (ShouldIgnore(filePath))
                            {
                                Debug.WriteLine($"[WARNING] Ignoring \"{filePath}\"");
                                continue;
                            }

                            try
                            {
                                FileInfo fileInfo = new FileInfo(filePath);
                                if (fileInfo.LastWriteTime < cutoffDate)
                                {
                                    if (reportOnly)
                                    {
                                        progressSuccess?.Report(filePath);
                                    }
                                    else
                                    {
                                        if (fileInfo.IsReadOnly)
                                            fileInfo.IsReadOnly = false;

                                        fileInfo.Delete();

                                        progressSuccess?.Report(filePath);
                                    }
                                    // Smooth out the ProgBar jumpiness
                                    //await Task.Delay(1);
                                }
                            }
                            catch
                            {
                                failedFiles.Add(filePath);
                                progressFail?.Report(filePath);
                                // Smooth out the ProgBar jumpiness
                                //await Task.Delay(1);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        CleanupError?.Invoke(this, ex);
                    }
                }

                await Task.Delay(1);

                //CleanupCompleted?.Invoke(this, failedFiles);

                return failedFiles;

            }, ct);
        }

        #endregion

        #region [Helpers]
        async Task RunSystemFileChecker(CancellationToken ct, IProgress<FileResult> progress = null)
        {
            await Task.Run(async () =>
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "sfc.exe",
                        Arguments = "/scannow",
                        //WorkingDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32"),
                        UseShellExecute = false,        // MUST be true for elevation
                        Verb = "runas",                 // Triggers UAC elevation
                        CreateNoWindow = false,         // SFC must run in a console window
                        RedirectStandardOutput = true,  // Capture normal output
                        RedirectStandardError = true,   // Capture error output
                        StandardOutputEncoding = System.Text.Encoding.Unicode // SFC often uses UTF-16
                    };
                    using (var process = new Process { StartInfo = startInfo })
                    {
                        // Subscribe to events for real-time updates
                        process.OutputDataReceived += (s, e) => 
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                progress?.Report(new FileResult { FilePath = $"[SFC] {e.Data}", IsSuccess = true });
                                //Application.Current.Dispatcher.Invoke(() => lstFiles.Items.Add(e.Data));
                            }
                        };
                        
                        process.ErrorDataReceived += (s, e) => 
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                progress?.Report(new FileResult { FilePath = $"[SFC] {e.Data}", IsSuccess = false });
                                //Application.Current.Dispatcher.Invoke(() => lstFiles.Items.Add("ERROR: " + e.Data));
                            }
                        };

                        process.Start();

                        // Start the asynchronous read
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        // NOTE: process.WaitForExitAsync(ct) isn't available in older .NET Framework versions
                        #region [Legacy Framework Process Cancel]
                        // Create a task that completes when the process exits
                        var processExitTask = Task.Run(() => process.WaitForExit());
                        // Create a task that completes when the token is cancelled
                        var cancellationTask = Task.Delay(-1, ct);
                        // Wait for whichever happens first
                        var completedTask = await Task.WhenAny(processExitTask, cancellationTask);
                        // If cancellation was requested then kill the external process
                        if (completedTask == cancellationTask)
                        {
                            if (!process.HasExited)
                                process.Kill();
                            ct.ThrowIfCancellationRequested();
                        }
                        #endregion

                        await Task.Delay(250);
                    }
                }
                catch (OperationCanceledException)
                {
                    Dispatcher.Invoke(() =>
                    {
                        WpfMessageBox.Show("User canceled the SFC process!", false, false, owner: this);
                    });
                }
                catch (System.ComponentModel.Win32Exception) // User clicked "No" on the UAC prompt
                {
                    Dispatcher.Invoke(() =>
                    {
                        WpfMessageBox.Show("Administrative access is required to fix system files.", false, true, owner: this);
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        WpfMessageBox.Show($"RunSystemFileChecker{Environment.NewLine}{ex.Message}", false, true, owner: this);
                    });
                }
            });
        }

        /// <summary><code>
        /// "cleanmgr /sagerun:1"
        /// Runs the Windows Disk Cleanup tool using a pre-configured profile (ID 1) 
        /// created by the cleanmgr /sageset:1 command. It automates file deletion, 
        /// including temp files, system logs, and cache, based on selected settings 
        /// without requiring further user input.
        /// "cleanmgr /lowdisk"
        /// Opens the utility with all checkboxes selected by default, which is useful
        /// for a "complete scan" when the volume is running with limited free space.
        /// </code></summary>
        async Task RunDiskCleanup(CancellationToken ct, bool sageRun = true)
        {
            await Task.Run(async () =>
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "cleanmgr.exe",
                        Arguments = sageRun ? "/sagerun:1" : "/lowdisk /d C",
                        Verb = "runas",
                        UseShellExecute = true
                    };
                    using (var process = new Process { StartInfo = startInfo })
                    {
                        // NOTE: process.WaitForExitAsync(ct) isn't available in older .NET Framework versions
                        #region [Legacy framework process cancel]
                        // Create a task that completes when the process exits
                        var processExitTask = Task.Run(() => process.WaitForExit());
                        // Create a task that completes when the token is cancelled
                        var cancellationTask = Task.Delay(-1, ct);
                        // Wait for whichever happens first
                        var completedTask = await Task.WhenAny(processExitTask, cancellationTask);
                        // If cancellation was requested then kill the external process
                        if (completedTask == cancellationTask)
                        {
                            if (!process.HasExited)
                                process.Kill();
                            ct.ThrowIfCancellationRequested();
                        }
                        #endregion
                    }
                }
                catch (OperationCanceledException)
                {
                    Dispatcher.Invoke(() =>
                    {
                        WpfMessageBox.Show("User canceled the SFC process!", false, false, owner: this);
                    });
                }
                catch (System.ComponentModel.Win32Exception) // User clicked "No" on the UAC prompt
                {
                    Dispatcher.Invoke(() =>
                    {
                        WpfMessageBox.Show("Administrative access is required to clean system files.", false, true, owner: this);
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        WpfMessageBox.Show($"RunDiskCleanAuto{Environment.NewLine}{ex.Message}", false, true, owner: this);
                    });
                }
            });
        }

        /// <summary>
        /// Adds <c>StateFlags0001</c> to registry for cleanmgr.
        /// </summary>
        void PreConfigureDiskCleanup()
        {
            const string registryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches";
            try
            {
                // Open the HKLM base key with write access (Requires Admin)
                using (RegistryKey rootKey = Registry.LocalMachine.OpenSubKey(registryPath, true))
                {
                    if (rootKey != null)
                    {
                        // Iterate through every cleanup category (Recycle Bin, Temporary Files, Update Cleanup, etc.)
                        foreach (string subKeyName in rootKey.GetSubKeyNames())
                        {
                            using (RegistryKey subKey = rootKey.OpenSubKey(subKeyName, true))
                            {
                                // Set StateFlags0001 to 2 (Checked)
                                // Note: "0001" corresponds to the "1" in /sageset:1
                                subKey?.SetValue("StateFlags0001", 2, RegistryValueKind.DWord);
                            }
                        }
                        WpfMessageBox.Show("Registry pre-configured for Set 1.", false, false, owner: this);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                WpfMessageBox.Show($"Administrator privileges are required to modify registry settings.{Environment.NewLine}You can run \"cleanmgr /sageset:1\" from an elevated command prompt, then check all the boxes.", false, true, owner: this);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"{ex.Message}{Environment.NewLine}Try running \"cleanmgr /sageset:1\" from an elevated command prompt, then check all the boxes.", false, true, owner: this);
            }
        }

        bool ShouldIgnore(string path)
        {
            try
            {
                if (_cfgExcludeFolders == null)
                    return false;

                // Normalize separators and split into segments
                var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                foreach (var segment in segments)
                {
                    foreach (var ignored in _cfgExcludeFolders)
                    {
                        if (segment.Equals(ignored, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            catch
            {
                // If something goes wrong, fail safe and do NOT ignore
            }
            return false;
        }

        bool ShouldIgnoreWithWildcards(string path)
        {
            try
            {
                if (_cfgExcludeFolders == null)
                    return false;

                var segments = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var segment in segments)
                {
                    foreach (var pattern in _cfgExcludeFolders)
                    {
                        if (WildcardMatch(segment, pattern))
                            return true;
                    }
                }
            }
            catch
            {
                // Fail-safe: do NOT ignore if something goes wrong
            }
            return false;
        }


        bool WildcardMatch(string text, string pattern)
        {
            // Escape regex special chars except * and ?
            string regex = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            return Regex.IsMatch(text, regex, RegexOptions.IgnoreCase
            );
        }
        #endregion
    }

    #region [Models]
    public class FileResult
    {
        public string FilePath { get; set; }
        public bool IsSuccess { get; set; }

        public string StatusIcon => IsSuccess ? "Check" : "Alert";
        public string StatusColor => IsSuccess ? "#00FF21" : "#FFAE00";
        public string StatusText => IsSuccess ? "#F2F2F2" : "#C9C9C9";
    }
    #endregion
}
