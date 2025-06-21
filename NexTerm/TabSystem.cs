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
using System.Runtime.InteropServices;
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

        const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
        const int STARTF_USESTDHANDLES = 0x00000100;
        const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;

        // Structs
        [StructLayout(LayoutKind.Sequential)]
        struct COORD
        {
            public short X;
            public short Y;
            public COORD(short x, short y)
            {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        // DLL Imports
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint GetLastError();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, IntPtr lpPipeAttributes, int nSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CreateProcessW(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            int dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            [In] ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool InitializeProcThreadAttributeList(
            IntPtr lpAttributeList,
            int dwAttributeCount,
            int dwFlags,
            ref IntPtr lpSize
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList,
            uint dwFlags,
            IntPtr attribute,
            IntPtr lpValue,
            IntPtr cbSize,
            IntPtr lpPreviousValue,
            IntPtr lpReturnSize
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, int nNumberOfBytesToRead, out int lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, int nNumberOfBytesToWrite, out int lpNumberOfBytesWritten, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr hPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int ClosePseudoConsole(IntPtr hPC);

        private MainWindow _mainWindow;

        public class TabData
        {
            public string OutputLog { get; set; } = "";
            public string TabPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            public List<string> TabCommandHistory { get; set; } = new();
            public string CurrentCommand { get; set; } = "";

            public IntPtr ConPTYHandle { get; private set; }
            public IntPtr StdInWrite { get; private set; }
            public IntPtr StdOutRead { get; private set; }
            public Thread? OutputReaderThread { get; private set; }

            private ConcurrentQueue<string>? _outputQueue = new();
            public Action<string> InputHandler => SendInput;
            private Timer? _flushTimer;
            private Action<string>? _onOutput;
            private Action<string>? _updatePath;

            public TabData(Action<string> pushToOutput, Action<string> updatePath)
            {
                StartShell(pushToOutput, updatePath);
            }

            public void StartShell(Action<string> pushToOutput, Action<string> updatePath)
            {
                COORD consoleSize = new COORD(200, 1);
                IntPtr hPC = IntPtr.Zero;

                // Create pipes
                if (!CreatePipe(out IntPtr hPipeInRead, out IntPtr hPipeInWrite, IntPtr.Zero, 0) ||
                    !CreatePipe(out IntPtr hPipeOutRead, out IntPtr hPipeOutWrite, IntPtr.Zero, 0))
                {
                    Console.WriteLine($"CreatePipe failed: {Marshal.GetLastWin32Error()}");
                    return;
                }

                // Create pseudo console
                int result = CreatePseudoConsole(consoleSize, hPipeInRead, hPipeOutWrite, 0, out hPC);
                if (result != 0)
                {
                    Console.WriteLine($"CreatePseudoConsole failed: {result}");
                    return;
                }

                // Prepare Startup Info
                STARTUPINFOEX siEx = new STARTUPINFOEX
                {
                    StartupInfo = new STARTUPINFO
                    {
                        cb = Marshal.SizeOf<STARTUPINFOEX>(),
                        hStdInput = hPipeInRead,
                        hStdOutput = hPipeOutWrite,
                        hStdError = hPipeOutWrite,
                        dwFlags = STARTF_USESTDHANDLES
                    }
                };

                // Get required size for attribute list
                IntPtr lpSize = IntPtr.Zero;
                if (!InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize))
                {
                    int lastError = Marshal.GetLastWin32Error();
                    if (lastError != 122) 
                    {
                        Console.WriteLine($"InitializeProcThreadAttributeList (size query) failed: {lastError}");
                        return;
                    }
                }

                // Allocate and initialize attribute list
                siEx.lpAttributeList = Marshal.AllocHGlobal(lpSize);
                if (!InitializeProcThreadAttributeList(siEx.lpAttributeList, 1, 0, ref lpSize))
                {
                    Console.WriteLine($"InitializeProcThreadAttributeList (init) failed: {Marshal.GetLastWin32Error()}");
                    Marshal.FreeHGlobal(siEx.lpAttributeList);
                    return;
                }

                // Update attribute with pseudo console handle
                if (!UpdateProcThreadAttribute(
                    siEx.lpAttributeList,
                    0,
                    (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    hPC,
                    (IntPtr)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
                {
                    Console.WriteLine($"UpdateProcThreadAttribute failed: {Marshal.GetLastWin32Error()}");
                    DeleteProcThreadAttributeList(siEx.lpAttributeList);
                    Marshal.FreeHGlobal(siEx.lpAttributeList);
                    return;
                }

                // Create process
                PROCESS_INFORMATION pi;
                bool success = CreateProcessW(
                    null!,
                    "cmd.exe",
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    EXTENDED_STARTUPINFO_PRESENT,
                    IntPtr.Zero,
                    null!,
                    ref siEx,
                    out pi);

                if (!success)
                {
                    Console.WriteLine($"CreateProcessW failed: {Marshal.GetLastWin32Error()}");
                    DeleteProcThreadAttributeList(siEx.lpAttributeList);
                    Marshal.FreeHGlobal(siEx.lpAttributeList);
                    return;
                }

                // Clean up our handles (child process has its own copies)
                CloseHandle(hPipeInRead);
                CloseHandle(hPipeOutWrite);

                StdInWrite = hPipeInWrite;
                StdOutRead = hPipeOutRead;
                ConPTYHandle = hPC;

                _onOutput = pushToOutput;
                _updatePath = updatePath;

                // Start the output reading thread
                OutputReaderThread = new Thread(() =>
                {
                    byte[] buffer = new byte[1024];
                    while (true)
                    {
                        if (!ReadFile(StdOutRead, buffer, buffer.Length, out int bytesRead, IntPtr.Zero) || bytesRead == 0)
                            break;

                        string output = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        foreach (string line in output.Split('\n'))
                        {
                            EnqueueOutput(line.TrimEnd('\r'));
                        }
                    }
                });
                OutputReaderThread.Start();

                // Start auto-flushing to UI
                _flushTimer = new Timer(_ => FlushOutput(), null, 0, 30);
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
                byte[] input = Encoding.UTF8.GetBytes(command + "\r\n");
                WriteFile(StdInWrite, input, input.Length, out _, IntPtr.Zero);
            }

            public void Dispose()
            {
                ClosePseudoConsole(ConPTYHandle);
                CloseHandle(StdInWrite);
                CloseHandle(StdOutRead);
                OutputReaderThread?.Join();
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
            _mainWindow.Terminal._sendInput = newData.InputHandler;
            _mainWindow.Terminal.UpdateDirectory(newData.TabPath);
            _mainWindow.Terminal.setOutputLog(newData.OutputLog);
            _mainWindow.commandManager.CommandHistory = newData.TabCommandHistory;
        }
    }
}
