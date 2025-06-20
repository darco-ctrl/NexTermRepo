// NexTerm Terminal Engine v1.1.0
// Author: Darco
// Description: TabManager

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace NexTerm
{
    public class TabSystem
    {
        private MainWindow _mainWindow;

        public class TabData
        {
            public string OutputLog { get; set; } =
                """

                  ╔══════════════════════════════════════════════╗
                  ║                                              ║
                  ║               NexTerm v1.1.0                 ║
                  ║         Shell Engine: PowerShell             ║
                  ║                                              ║
                  ╚══════════════════════════════════════════════╝

                """;

            public string TabPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            public List<string> TabCommandHistory { get; set; } = new();
            public string CurrentCommand { get; set; } = "";

            private Process? _process;
            public StreamWriter? _stdin { get; private set; }
            private ConcurrentQueue<string>? _outputQueue = new();
            private Timer? _flushTimer;
            private Action<string>? _onOutput;
            private Action<string>? _updatePath;

            public TabData(Action<string> pushToOutput, Action<string> updatePath)
            {
                StartShell(pushToOutput, updatePath);
            }

            public void StartShell(Action<string> pushToOutput, Action<string> updatePath)
            {

                _onOutput = pushToOutput;
                _updatePath = updatePath;

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoLogo -NoProfile -NoExit -Command \"$Function:prompt = {''}\"",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = TabPath
                };

                _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        EnqueueOutput(e.Data);
                };
                _process.ErrorDataReceived += (s, e) => EnqueueOutput($"{e.Data}");

                _process.Exited += (s, e) =>
                {
                    _outputQueue?.Enqueue("[process exited]");
                };

                _process.Start();
                _stdin = _process.StandardInput;


                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                _flushTimer = new Timer(_ => FlushOutput(), null, 0, 30); // 30ms per frame flush
            }

            private void EnqueueOutput(string line)
            {
                if (_outputQueue == null) return;

                if (string.IsNullOrWhiteSpace(line)) return;

                // Try detecting directory changes (optional improvement)
                if (Directory.Exists(line.Trim()))
                {
                    _updatePath?.Invoke(line.Trim());
                    return;
                }

                _outputQueue.Enqueue(line);
            }

            private void FlushOutput()
            {
                if (_outputQueue == null) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    while (_outputQueue.TryDequeue(out string? line))
                    {
                        _onOutput?.Invoke(line + "\n");
                    }
                });
            }

            public void SendInput(string command)
            {
                if (_process == null) return;
                if (_stdin == null || _process.HasExited) return;

                CurrentCommand = command;
                TabCommandHistory.Add(command);

                _stdin.WriteLine(command);
                _stdin.Flush();
            }

            public void Dispose()
            {
                if (_process == null || _stdin == null) return;
                try
                {
                    if (!_process.HasExited)
                    {
                        _stdin.WriteLine("exit");
                        _stdin.Flush();
                        _process.WaitForExit(1000); // wait 1s max
                        if (!_process.HasExited)
                            _process.Kill(); // fallback
                    }
                }
                catch { }

                _stdin?.Dispose();
                _process?.Dispose();
                _flushTimer?.Dispose();
            }
        }

        public Dictionary<TabItem, TabData> nexTermTabs = new();

        public TabSystem(MainWindow mainwindow) 
        {
            _mainWindow = mainwindow;
            OnTabReady();
        }
        private void OnTabReady()
        {
            CreateNewTab();
        }

        public void CreateNewTab()
        {
            try
            {

                TabItem newTab = new TabItem();
                TextBox TabTextBox = CreateTabTextBox();
                Button TabClosebutton = CreateTabCloseButton(newTab);

                Binding isSelectedBinding;
                StackPanel tabheader;
                int tabindex = _mainWindow.TabBlock.Items.Count + 1;

                tabheader = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0)
                };
                tabheader.Children.Add(new ContentControl { Content = TabTextBox} );
                tabheader.Children.Add(TabClosebutton);
                newTab.Header = tabheader;

                isSelectedBinding = new Binding("IsSelected") { Source = newTab };
                TabTextBox.SetBinding(TextBox.IsEnabledProperty, isSelectedBinding);
                TabClosebutton.SetBinding(Button.IsEnabledProperty, isSelectedBinding);

                _mainWindow.TabBlock.Items.Add(newTab);
                nexTermTabs.Add(newTab, new TabData(_mainWindow.Terminal.PushToOutput, _mainWindow.Terminal.UpdateDirectory));

                _mainWindow.TabBlock.SelectedItem = newTab;
                SelectNewTab(newTab);

            } catch (Exception ex)
            {
                _mainWindow.Terminal.ShowError($"{ex}");
            }
        }

        private TextBox CreateTabTextBox()
        {
            return new TextBox
            {
                Text = $"NexTerm",
                Style = (Style)_mainWindow.FindResource("TabTextBox"),
                IsEnabled = false,
                Tag = "TagId",
            };
        }
        
        private Button CreateTabCloseButton(TabItem tab)
        {
            var button =  new Button
            {
                Style = (Style)_mainWindow.FindResource("TabButton"),
                IsEnabled = false,
                Tag = tab
            };
            button.Click += _mainWindow.OnTabCloseButtonClick;
            return button;
        }


        public void SelectNewTab(TabItem tab)
        {
            if (_mainWindow.Terminal.current_tab == tab) return;

            if (!nexTermTabs.TryGetValue(tab, out TabData? newData))
            {
                MessageBox.Show($"[Error] Couldn't find Requested TabItem {tab}");
                return;
            }

            if (_mainWindow.Terminal.current_tab != null &&
            nexTermTabs.TryGetValue(_mainWindow.Terminal.current_tab, out TabData? currentData))
            {
                currentData.TabPath = _mainWindow.Terminal.currentDir;
                currentData.OutputLog = _mainWindow.Terminal.GetCurrentOutputLog();
                currentData.TabCommandHistory = _mainWindow.commandManager.CommandHistory;
            }

            // Load new tab data

            _mainWindow.Terminal.current_tab = tab;
            _mainWindow.Terminal._streamWriter = newData._stdin;
            _mainWindow.Terminal.UpdateDirectory(newData.TabPath);
            _mainWindow.Terminal.setOutputLog(newData.OutputLog);
            _mainWindow.commandManager.CommandHistory = newData.TabCommandHistory;
        }
    }
}
