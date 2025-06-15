using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        private NexTermCommand ntcmd;

        private bool TerminalStarted = false;
        private bool IsRunningCommand = false;
        private bool CanPushCommand = true;

        private string current_command = "";

        private StreamWriter? InputWriter;
        private StreamReader? OutputReader;
        private StreamReader? ErrorReader;

        public TerminalEngine(TextBox outputBox, TextBox inputbox, TextBox pathBox)
        {
            OutputBox = outputBox;
            InputBox = inputbox;
            PathBox = pathBox;

            ntcmd = new NexTermCommand(OutputBox, InputBox);
        }

        public void OnTerminalReady()
        {
            if (TerminalStarted) return;
            TerminalStarted = true;

            CMDProcess = new Process();
            CMDProcess.StartInfo.FileName = "powershell.exe";
            CMDProcess.StartInfo.Arguments = "-NoLogo -NoExit \"Write-Host 'NexTerm is Ready'\"";
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
            Task.Run(() => ErrorOuPutReader());
        }

        private void ReadOutputLoop()
        {
            string previos_line = "";
            while (CMDProcess != null && !CMDProcess.HasExited)
            {
                string? line = OutputReader?.ReadLine();
                if (line != null && CanPushCommand)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (line.Contains('>') && line.StartsWith("PS "))
                        {
                            line = LineOverride(line);
                        }   

                        if (previos_line == line && previos_line == "")
                        {
                            RemoveLastLine();
                            OutputBox.AppendText("\n_");
                        }

                        if (line == " " + current_command)
                        {
                            line = ">" + line;
                        }

                        OutputBox.AppendText($"\n{line}");
                        OutputBox.ScrollToEnd();

                        previos_line = line;
                    });
                }
            }
        }
        private void ErrorOuPutReader()
        {
            while (CMDProcess != null && !CMDProcess.HasExited)
            {
                string? line = ErrorReader?.ReadLine();
                if (line != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        OutputBox.AppendText($"\n {line}");
                        OutputBox.ScrollToEnd();
                    });
                }
            }
        }

        public void HandlePreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (InputWriter != null)
                {
                    current_command = InputBox.Text;
                    if (current_command.StartsWith("@"))
                    {
                        ntcmd.CommandExcuter(current_command);
                    } else
                    {
                        if (current_command.Contains("cd ")) { PathUpdater(current_command); }
                        ntcmd.CommandHistory.Add(current_command);
                        InputWriter.WriteLine(current_command);
                        InputWriter.Flush();
                    }

                }
                InputBox.Text = "";
                InputBox.CaretIndex = 0;
            }
        }

        private string LineOverride(string line)
        {
            int splitindex = line.IndexOf('>');

            if (splitindex != -1 && splitindex + 1 < line.Length) 
            {
                return line.Substring(splitindex + 1);
            } else { return " Something went wrong"; }
        }

        private void PathUpdater(string new_cmdpath)
        {
            //cd path
            string new_path = new_cmdpath.Substring(3);
            PathBox.Text = new_path;
        }

        private void RemoveLastLine()
        {
            List<string> lines = OutputBox.Text.Split('\n').ToList<string>();

            if (lines.Count > 0)
            {
                if (lines[lines.Count - 1] == "_" || lines[lines.Count - 1] == "> ") { lines[lines.Count - 1] = ""; }
                if (lines[lines.Count - 2] == "_" || lines[lines.Count - 2] == "> ") { lines[lines.Count - 2] = ""; }
                OutputBox.Text = string.Join("\n", lines);
                OutputBox.ScrollToEnd();
            }
        }

        public void CloseCMD()
        {
            if (CMDProcess != null) {
                CMDProcess.Close();
            }
        }
    }
}
