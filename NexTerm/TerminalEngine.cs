// NexTerm Terminal Engine v1.0
// Author: Darco
// Description: Core engine for terminal input/output + command handling

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NexTerm
{
    internal class TerminalEngine
    {
        private TextBox OutputBox;
        private TextBox InputBox;
        private TextBox PathBox;
        private Process? CMDProcess;
        private TextBlock RunningIndicator;

        private NexTermCommand ntcmd;

        private bool TerminalStarted = false;
        private bool CanPushCommand = true;
        private bool IsCommandRunning = false;

        private string current_command = "";
        private string[] loading_char = { "|", "/", "-", "\\" };

        private List<string> PreviousCommands = new List<string>();
        private int currentCmdIndex = 0;
        private int maxPreviousCommands = 10;

        private StreamWriter? InputWriter;
        private StreamReader? OutputReader;
        private StreamReader? ErrorReader;

        public TerminalEngine(TextBox outputBox, TextBox inputbox, TextBox pathBox, TextBlock loadingAnimationBox)
        {
            OutputBox = outputBox;
            InputBox = inputbox;
            PathBox = pathBox;
            RunningIndicator = loadingAnimationBox;

            ntcmd = new NexTermCommand(OutputBox, InputBox, this);
        }

        public void OnTerminalReady()
        {
            if (TerminalStarted) return;
            TerminalStarted = true;

            CMDProcess = new Process();
            CMDProcess.StartInfo.FileName = "powershell.exe";
            CMDProcess.StartInfo.Arguments = "-NoLogo -NoExit -Command \"function prompt { '' }\" \"Write-Host ' NexTerm is Ready \n\n Enter @help for NexTerm Commands\n'\"";
            CMDProcess.StartInfo.RedirectStandardInput = true;
            CMDProcess.StartInfo.RedirectStandardOutput = true;
            CMDProcess.StartInfo.RedirectStandardError = true;
            CMDProcess.StartInfo.UseShellExecute = false;
            CMDProcess.StartInfo.CreateNoWindow = true;
            CMDProcess.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            CMDProcess.StartInfo.WorkingDirectory = @"C:\";

            CMDProcess.Start();

            InputWriter = CMDProcess.StandardInput;
            OutputReader = CMDProcess.StandardOutput;
            ErrorReader = CMDProcess.StandardError;

            PathUpdater(@"cd C:\");

            Task.Run(() => ReadOutputLoop());
            Task.Run(() => ErrorOutputReader());
        }
 
        private void ReadOutputLoop()
        {
            while (CMDProcess != null && !CMDProcess.HasExited)
            {
                if (OutputReader == null || Application.Current == null) return;

                string? line = OutputReader?.ReadLine();
                if (line != null && CanPushCommand)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (line.StartsWith("PS>"))
                        {
                            line = CommandOverride(line);
                        }

                        if (line.Contains("[DONE]"))
                        {
                            line = line.Substring(0, line.Length - 6);
                            UpdateIndicator(false);
                        }

                        PushToOutput($"\n{line}");
                    });
                }
            }
        }

        private string CommandOverride(string command)
        {
            //PS>
            if (command.Length > 16)
                return $"> {command.Substring(3, command.Length - 16)}";
            else
                return "> " + command;
        }

        private void ErrorOutputReader()
        {
            while (CMDProcess != null && !CMDProcess.HasExited)
            {
                if (OutputReader == null || Application.Current == null) return;

                string? line = ErrorReader?.ReadLine();
                if (line != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        PushToOutput($"\n {line}");
                    });
                }
            }
        }

        public void HandlePreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !IsCommandRunning)
            {
                if (InputWriter != null)
                {
                    current_command = InputBox.Text;
                    InputBox.Text = "";
                    InputBox.CaretIndex = 0;
                    currentCmdIndex = PreviousCommands.Count - 1;

                    if (string.IsNullOrWhiteSpace(current_command))
                        return;

                    if (current_command.StartsWith("@"))
                    {
                        ntcmd.ExecuteCommand(current_command);
                    } else
                    {
                        if (current_command.Contains("cd ")) { PathUpdater(current_command); }
                        ntcmd.AddToHistory(current_command);

                        UpdateIndicator(true);
                        AddToPreviousCommand(current_command);

                        Debug.WriteLine($"[NexTerm] Running command: {current_command}");

                        InputWriter.WriteLine(current_command + "; echo [DONE]");
                        InputWriter.Flush();
                    }
                }
            }
        }

        private void PathUpdater(string new_cmdpath)
        {
            var parts = new_cmdpath.Split(' ', 2);
            if (parts.Length == 2)
                PathBox.Text = parts[1];
        }

        public void CloseCMD()
        {
            try
            {
                if (CMDProcess != null && !CMDProcess.HasExited)
                {
                    InputWriter?.WriteLine("exit");
                    CMDProcess.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NexTerm] Error while closing: {ex.Message}");
            }
        }

        public void PushToOutput(string text)
        {
            OutputBox.AppendText(text);
            OutputBox.ScrollToEnd();
        }

        public void ClearOutPut(string text)
        {
            OutputBox.Text = text;
            OutputBox.ScrollToEnd();
        }

        private void UpdateIndicator(bool isRunning)
        {
            IsCommandRunning = isRunning;
            RunningIndicator.Foreground = IsCommandRunning ? Brushes.Red : Brushes.LightGreen;
        }

        public void AddToPreviousCommand(string command)
        {
            if (PreviousCommands.Count == maxPreviousCommands) { PreviousCommands.RemoveAt(0); }
            PreviousCommands.Add(command);
        }

        public void InputCommandChanger(KeyEventArgs e)
        {
            if (e.Key == Key.Up)
            {
                currentCmdIndex -= 1;
                currentCmdIndex = Math.Clamp(currentCmdIndex, 0, maxPreviousCommands - 1);
                if (currentCmdIndex < PreviousCommands.Count) { InputBox.Text = PreviousCommands[currentCmdIndex]; } else { return; }
                
            }
            else if (e.Key == Key.Down)
            {
                currentCmdIndex += 1;
                currentCmdIndex = Math.Clamp(currentCmdIndex, 0, PreviousCommands.Count - 1);
                InputBox.Text = PreviousCommands[currentCmdIndex];
            }
        }
    }
}
