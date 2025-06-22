// NexTerm Terminal Engine v1.1.0
// Author: Darco
// Description: Core engine for terminal input/output + command handling

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
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
using SNMM = System.Net.Mime.MediaTypeNames;
using WSPath = System.Windows.Shapes.Path;

namespace NexTerm
{
    public class TerminalEngine
    {
        private MainWindow mainWindow;

        private readonly Dictionary<int, Brush> ForegroundColors = new()
    {
        { 30, Brushes.Black },
        { 31, Brushes.Red },
        { 32, Brushes.Green },
        { 33, Brushes.Yellow },
        { 34, Brushes.Blue },
        { 35, Brushes.Magenta },
        { 36, Brushes.Cyan },
        { 37, Brushes.White },

        // Bright variants (90–97)
        { 90, Brushes.DarkGray },
        { 91, Brushes.OrangeRed },
        { 92, Brushes.LightGreen },
        { 93, Brushes.LightYellow },
        { 94, Brushes.LightBlue },
        { 95, Brushes.Plum },
        { 96, Brushes.LightCyan },
        { 97, Brushes.White }
    };


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

        public TerminalEngine(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
        }

        public void OnTerminalReady()
        {
            if (TerminalStarted) return;
            TerminalStarted = true;
            PushToOutput("\x1B[31mHello\x1B[0m World");
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
                // First clean escape sequences that aren't color-related
                string cleaned = Regex.Replace(text, @"\x1B\].*?\x07", "");

                string[] lines = cleaned.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (string rawLine in lines)
                    {
                        string line = rawLine.TrimEnd('\r', '\n');

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        if (Regex.IsMatch(line.Trim(), @"^[A-Z]:\\.*>$"))
                        {
                            UpdateDirectory(line.TrimEnd('>'));
                            continue;
                        }

                        // 👇 Just convert this line into paragraph (with color)
                        Paragraph paragraph = AsciiToColor(line);

                        if (paragraph.Inlines.Count > 0)
                        {
                            mainWindow.OutputBox.Document.Blocks.Add(paragraph);
                            mainWindow.OutputBox.ScrollToEnd();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                File.AppendAllText("crashlog.txt", $"PushToOutput crash: {ex}\ntext={text}\n");
            }
        }


        private Paragraph AsciiToColor(string line)
        {
            Paragraph paragraph = new Paragraph
            {
                Margin = new Thickness(0)
            };

            bool foundAnsi = false;

            foreach (string word in line.Split(' '))
            {
                if (word.Contains('\x1B'))
                {
                    int start = word.IndexOf("\x1B[");
                    int end = word.IndexOf('m', start);
                    if (start >= 0 && end > start)
                    {
                        foundAnsi = true;

                        string ansiCode = word.Substring(start, end - start + 1);
                        string cleaned = word.Substring(end + 1);
                        string code = ansiCode.TrimStart('\x1B').TrimStart('[').TrimEnd('m');

                        if (int.TryParse(code, out int result) && ForegroundColors.TryGetValue(result, out Brush? brush))
                        {
                            string fallbackCleaned = Regex.Replace(cleaned, @"\x1B\[[0-9;?]*[A-Za-z]", "");
                            Run run = new Run(fallbackCleaned + " ") // keep spacing
                            {
                                Foreground = brush
                            };
                            paragraph.Inlines.Add(run);
                        }
                        else
                        {
                            Run fallbackRun = new Run(cleaned + " ");
                            paragraph.Inlines.Add(fallbackRun);
                        }
                    }
                }
                else
                {
                    string fallbackCleaned = Regex.Replace(line, @"\x1B\[[0-9;?]*[A-Za-z]", "");
                    Run run = new Run(fallbackCleaned + " ");
                    paragraph.Inlines.Add(run);
                }
            }

            if (!foundAnsi && paragraph.Inlines.Count == 0)
            {
                Run run = new Run(line)
                {
                    Foreground = Brushes.White 
                };
                paragraph.Inlines.Add(run);
            }

            return paragraph;
        }

        public void SetOutput(string text)
        {
            mainWindow.OutputBox.Document.Blocks.Clear();
            Paragraph para = new Paragraph();
            Run run = new Run(text);
            para.Inlines.Add(run);
            mainWindow.OutputBox.Document.Blocks.Add(para);
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
            string fulltext = new TextRange(
                mainWindow.OutputBox.Document.ContentStart,
                mainWindow.OutputBox.Document.ContentEnd).Text;
            return fulltext;
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
