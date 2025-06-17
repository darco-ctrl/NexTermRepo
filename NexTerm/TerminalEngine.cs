// NexTerm Terminal Engine v1.0
// Author: Darco
// Description: Core engine for terminal input/output + command handling

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Remoting;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NexTerm
{
    internal class TerminalEngine
    {
        private MainWindow mw;
        private PowerShell? _ps;

        private NexTermCommand NexTermCommandManager;
        private TabSystem tabSystem;

        private bool TerminalStarted = false;
        private bool CanPushCommand = true;
        private bool IsCommandRunning = false;

        public string current_command = "";
        private string currentDir = "";

        private List<string> PreviousCommands = new List<string>();
        private int currentCommandIndex = 0;
        private int maxPreviousCommands = 10;

        // Config Data 
        public string CommandSufix = " | Format-Table -AutoSize | Out-String -Stream";

        public TerminalEngine(MainWindow mainwindow)
        {
            mw = mainwindow;

            OnTerminalReady();
            NexTermCommandManager = new NexTermCommand(mainwindow, this);
            tabSystem = new TabSystem(mainwindow, this);
        }

        public void OnTerminalReady()
        {
            if (TerminalStarted) return;
            TerminalStarted = true;

            _ps = PowerShell.Create();
            _ps.Commands.Clear();
            _ps.AddScript("Get-Location");
            var result = _ps.Invoke();

            currentDir = result.FirstOrDefault()?.ToString() ?? "";
            mw.PathBlock.Text = currentDir;
        }

        private void ExecutePowerShellCommand(string command, bool useraw)
        {
            if (_ps != null)
            {
                try
                {

                    var outputcollection = new PSDataCollection<PSObject>();

                    outputcollection.DataAdded += (sender, e) =>
                    {
                        var output = outputcollection[e.Index];
                        var text = output.BaseObject?.ToString();
                        if (!string.IsNullOrWhiteSpace(text))
                        { 
                            Application.Current.Dispatcher.Invoke(() =>
                            {

                                if (text.Contains("__End_"))
                                {
                                    text = text.Substring(0, text.Length - 6);
                                    UpdateIndicator(false);
                                }

                                PushToOutput($" {text}");

                                if (command.StartsWith("cd") && text.Contains(":\\"))
                                {
                                    mw.PathBlock.Text = text;
                                }
                            });
                        }
                    };

                    string fullstring = command;
                    // Get-Date; Get-Process; Get-Date
                    if (!useraw)
                    {
                        if (fullstring.Contains(';'))
                        {
                            fullstring = fullstring.Replace(";", CommandSufix);
                        }
                        else
                        {
                            fullstring += CommandSufix;
                        }
                    }

                    if (fullstring.Contains("cd"))
                    {
                        fullstring += "; Get-Location";
                    }

                    _ps.Commands.Clear();
                    _ps.AddScript(fullstring + "; echo __End_");
                    _ps.BeginInvoke<PSObject, PSObject>(null, outputcollection);

                } catch (Exception ex)
                {
                    PushToOutput($"\n\n [Error] : {ex.Message}\n");
                }
            }
        }

        public void HandlePreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !IsCommandRunning)
            {
                CommandManager();
            }
        }

        private void CommandManager()
        {
            bool useRaw = false;

            current_command = mw.InputBox.Text.Trim();
            mw.InputBox.Text = "";
            mw.InputBox.CaretIndex = 0;

            if (!string.IsNullOrWhiteSpace(current_command))
            {

                if (current_command.ToLower().Contains("@raw"))
                {
                    useRaw = true;
                    current_command = current_command.Substring(4).Trim();
                }

                AddToPreviousCommand(current_command);
                if (current_command.StartsWith("@"))
                {
                    NexTermCommandManager.ExecuteCommand(current_command);
                }
                else
                {
                    UpdateIndicator(true);
                    NexTermCommandManager.AddToHistory(current_command, true);
                    PushToOutput($"\n> {current_command}");
                    ExecutePowerShellCommand(current_command, useRaw);
                }
            }
        }

        public void CloseNexTerm()
        {
            try
            {
                if (_ps != null)
                {
                    _ps.Stop();
                    _ps.Dispose();
                    _ps = null;
                }
            } catch (Exception ex) 
            {
                PushToOutput($"\n [Error] : {ex.Message}");
            }
        }

        public void ShowError(string message)
        {
            PushToOutput($"[Error] {message}");
        }

        public void PushToOutput(string text)
        {
            mw.OutputBox.AppendText(text + "\n");
            mw.OutputBox.ScrollToEnd();
        }

        public void ClearOutPut(string text)
        {
            mw.OutputBox.Text = text;
            mw.OutputBox.ScrollToEnd();
        }

        private void UpdateIndicator(bool isRunning)
        {
            IsCommandRunning = isRunning;
            mw.IsRunningIdecator.Foreground = IsCommandRunning ? Brushes.Red : Brushes.LightGreen;
        }

        public void AddToPreviousCommand(string command)
        {
            if (PreviousCommands.Count == maxPreviousCommands) { PreviousCommands.RemoveAt(0); }
            PreviousCommands.Add(command);
            currentCommandIndex = PreviousCommands.Count - 1;
        }

        public void InputCommandChanger(KeyEventArgs e)
        {
            if (e.Key == Key.Up)
            {
                currentCommandIndex -= 1;
                currentCommandIndex = Math.Clamp(currentCommandIndex, 0, maxPreviousCommands - 1);
                if (currentCommandIndex < PreviousCommands.Count) { mw.InputBox.Text = PreviousCommands[currentCommandIndex]; } else { return; }
                
            }
            else if (e.Key == Key.Down)
            {
                currentCommandIndex += 1;
                currentCommandIndex = Math.Clamp(currentCommandIndex, 0, PreviousCommands.Count - 1);
                mw.InputBox.Text = PreviousCommands[currentCommandIndex];
            }
        }
    }
}
