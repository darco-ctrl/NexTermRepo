// NexTerm Terminal Engine v1.1.0
// Author: Darco
// Description: Core engine for terminal input/output + command handling

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
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
        private MainWindow mainWindow;


        // Tab info
        public TabItem? current_tab;

        private bool TerminalStarted = false;
        private bool IsCommandRunning = false;

        public string current_command = "";
        public string currentDir = "";

        private List<string> PreviousCommands = new List<string>();
        private int currentCommandIndex = 0;
        private int maxPreviousCommands = 10;

        public Action<string>? _sendInput;

        private string _lineBuffer = "";

        public TerminalEngine(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
        }

        public void OnTerminalReady()
        {
            if (TerminalStarted) return;
            TerminalStarted = true;

        }

        private void ExecutePowerShellCommand(string command)
        {
            try
            {
                _sendInput?.Invoke(command);
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

                current_command = mainWindow.InputBox.Text.Trim();
                mainWindow.InputBox.Text = "";
                mainWindow.InputBox.CaretIndex = 0;

                mainWindow.TabManager.nexTermTabs[current_tab].CurrentCommand = current_command;
                UpdateIndicator(true);
                CommandModifier(current_command);
                UpdateIndicator(false);
            }
        }

        private void CommandModifier(string command)
        {

            if (!string.IsNullOrWhiteSpace(command))
            {

                AddToPreviousCommand(command);
                if (command.StartsWith("@"))
                {
                    mainWindow.commandManager.ExecuteCommand(command);
                }
                else
                {
                    mainWindow.commandManager.AddToHistory(command, true);

                    ExecutePowerShellCommand(command);
                }
            }
        }

        public void CloseNexTerm()
        {
            try
            {
                foreach (var tab in mainWindow.TabManager.nexTermTabs.Values)
                {
                    try
                    {
                        tab.Dispose();
                    } catch (Exception ex)
                    {
                        MessageBox.Show($"Could not close tab.\n\nException: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }


            } catch (Exception ex) 
            {
                PushToOutput($"\n [Error] : {ex.Message}");
            }
        }

        public void ShowError(string message)
        {
            PushToOutput($"\n\n[Error] {message}");
        }

        public void PushToOutput(string text)
        {
            try
            {
                // Step 1: Strip ANSI and OSC sequences
                string cleaned = Regex.Replace(text, @"\x1B\[[0-9;?]*[A-Za-z]", "");
                cleaned = Regex.Replace(cleaned, @"\x1B\].*?\x07", "");

                _lineBuffer += cleaned;
                string trimmed = _lineBuffer.Trim();

                // Step 2: Detect prompt and hide it
                if (Regex.IsMatch(trimmed, @"^[A-Z]:\\.*>$"))
                {
                    UpdateDirectory(trimmed.TrimEnd('>'));
                    _lineBuffer = "";
                    return;
                }

             
                if ((cleaned.EndsWith("\n") || cleaned.EndsWith("\r")) &&
                    !string.IsNullOrWhiteSpace(trimmed) && _lineBuffer.Trim() != "")
                {
                    if (trimmed == current_command)
                    {
                        _lineBuffer = $"\n> {trimmed}\n";
                    }
                    else
                    {
                        _lineBuffer = _lineBuffer.TrimEnd('\r', '\n');
                    }

                    mainWindow.OutputBox.AppendText(_lineBuffer);
                    mainWindow.OutputBox.ScrollToEnd();
                    _lineBuffer = "";
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("crashlog.txt", $"PushToOutput crash: {ex}\ntext={text}\n");
            }
        }



        public void SetOutput(string text)
        {
            mainWindow.OutputBox.Text = text;
            mainWindow.OutputBox.ScrollToEnd();
        }

        private void UpdateIndicator(bool isRunning)
        {
            IsCommandRunning = isRunning;
            mainWindow.IsRunningIdecator.Foreground = IsCommandRunning ? Brushes.Red : Brushes.LightGreen;
        }

        // Add to privous Command
        public void AddToPreviousCommand(string command)
        {
            currentCommandIndex = PreviousCommands.Count;
            if (PreviousCommands.Count == maxPreviousCommands) { PreviousCommands.RemoveAt(0); }
            PreviousCommands.Add(command);
            currentCommandIndex = PreviousCommands.Count;
        }

        // Press Up and down arrow key to get previos command
        public void InputCommandChanger(KeyEventArgs e)
        {
            if (e.Key == Key.Up)
            {
                currentCommandIndex -= 1;
                currentCommandIndex = Math.Clamp(currentCommandIndex, 0, maxPreviousCommands - 1);
                if (PreviousCommands.Count > 0 && currentCommandIndex < PreviousCommands.Count)
                {
                    mainWindow.InputBox.Text = PreviousCommands[currentCommandIndex];
                } else
                {
                    return;
                }
                mainWindow.InputBox.CaretIndex = mainWindow.InputBox.Text.Length;
                
            }
            else if (e.Key == Key.Down)
            {
                currentCommandIndex += 1;
                currentCommandIndex = Math.Clamp(currentCommandIndex, 0, PreviousCommands.Count - 1);
                mainWindow.InputBox.Text = PreviousCommands[currentCommandIndex];
            }
        }
        
        public string GetCurrentOutputLog()
        {
            return mainWindow.OutputBox.Text;
        }

        public void setOutputLog(string log)
        {
            mainWindow.OutputBox.Text = log;
            mainWindow.OutputBox.ScrollToEnd();
        }

        public void UpdateDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                currentDir = path;
                mainWindow.PathBlock.Text = currentDir;
            }
        }
    }
}
