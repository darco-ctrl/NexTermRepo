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
using System.Management.Automation.Runspaces;
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
using WSPath = System.Windows.Shapes.Path;

namespace NexTerm
{
    public class TerminalEngine
    {
        private MainWindow mw;


        // Tab info
        public TabItem? current_tab;
        public PowerShell? _ps;
        public PSDataCollection<PSObject>? OutputCollection;

        private bool TerminalStarted = false;
        private bool IsCommandRunning = false;

        public string current_command = "";
        public string currentDir = "";

        private List<string> PreviousCommands = new List<string>();
        private int currentCommandIndex = 0;
        private int maxPreviousCommands = 10;

        // Config Data 
        public string CommandSufix = " | Format-Table -AutoSize | Out-String -Stream;";

        public TerminalEngine(MainWindow mw)
        {
            this.mw = mw;
        }

        public void OnTerminalReady()
        {
            if (TerminalStarted) return;
            TerminalStarted = true;
        }

        private void ExecutePowerShellCommand(string command, bool useRaw)
        {
            if (_ps == null || OutputCollection == null) return;

            try
            {
                string fullCommand = command;
                if (!useRaw)
                {
                    if (fullCommand.Contains(';'))
                        fullCommand = fullCommand.Replace(";", CommandSufix);
                    else
                        fullCommand += CommandSufix;
                }

                if (!command.Contains("Get-Location") && !command.Contains("cd"))
                {
                    fullCommand += "; Get-Location";
                }

                _ps.Commands.Clear();
                _ps.Streams.ClearStreams();

                _ps.AddScript(fullCommand);
                _ps.BeginInvoke<PSObject, PSObject>(null, OutputCollection);
            }
            catch (Exception ex)
            {
                PushToOutput($"\n\n [Error] : {ex.Message}\n");
            }
        }

        public void HandlePreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !IsCommandRunning)
            {
                if (current_tab == null) return;

                current_command = mw.InputBox.Text.Trim();
                mw.InputBox.Text = "";
                mw.InputBox.CaretIndex = 0;

                mw.TabManager.nexTermTabs[current_tab].CurrentCommand = current_command;
                TerminalCommandExecuter(current_command);
            }
        }

        private void TerminalCommandExecuter(string command)
        {
            bool useRaw = false;
            UpdateIndicator(true);

            if (!string.IsNullOrWhiteSpace(command))
            {

                if (command.ToLower().Contains("@raw"))
                {
                    useRaw = true;
                    command = command.Substring(4).Trim();
                }

                AddToPreviousCommand(command);
                if (command.StartsWith("@"))
                {
                    mw.commandManager.ExecuteCommand(command);
                }
                else
                {
                    mw.commandManager.AddToHistory(command, true);
                    PushToOutput($"\n> {command}");

                    ExecutePowerShellCommand(command, useRaw);
                }
            }
            UpdateIndicator(false);
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
            currentCommandIndex = PreviousCommands.Count;
            if (PreviousCommands.Count == maxPreviousCommands) { PreviousCommands.RemoveAt(0); }
            PreviousCommands.Add(command);
            currentCommandIndex = PreviousCommands.Count;
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

        public string GetCurrentOutputLog()
        {
            return mw.OutputBox.Text;
        }

        public void setOutputLog(string log)
        {
            mw.OutputBox.Text = log;
            mw.OutputBox.ScrollToEnd();
        }

        public void UpdateDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                currentDir = path;
                mw.PathBlock.Text = currentDir;
            }
        }
    }
}
