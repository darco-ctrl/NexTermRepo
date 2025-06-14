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
        private Process? CMDProcess;

        private bool TerminalStarted = false;
        private bool IsRunningCommand = false;

        private StreamWriter? InputWriter;
        private StreamReader? OutputReader;
        private StreamReader? ErrorReader;

        public TerminalEngine(TextBox outputBox, TextBox inputBox)
        {
            OutputBox = outputBox;
            InputBox = inputBox;
        }

        public void OnTerminalReady()
        {
            if (TerminalStarted) return;
            TerminalStarted = true;

            CMDProcess = new Process();
            CMDProcess.StartInfo.FileName = "powershell.exe";
            CMDProcess.StartInfo.Arguments = "-NoLogo -NoExit -Command \"function prompt { '< ' }\" \"Write-Host 'Terminal Ready'\"";
            CMDProcess.StartInfo.RedirectStandardInput = true;
            CMDProcess.StartInfo.RedirectStandardOutput = true;
            CMDProcess.StartInfo.RedirectStandardError = true;
            CMDProcess.StartInfo.UseShellExecute = false;
            CMDProcess.StartInfo.CreateNoWindow = true;
            CMDProcess.StartInfo.StandardOutputEncoding = Encoding.UTF8;

            CMDProcess.Start();

            InputWriter = CMDProcess.StandardInput;
            OutputReader = CMDProcess.StandardOutput;
            ErrorReader = CMDProcess.StandardError;


            Task.Run(() => ReadOutputLoop());
            Task.Run(() => ErrorOuPutReader());
        }

        private void ReadOutputLoop()
        {
            string previos_line = "";
            while (CMDProcess != null && !CMDProcess.HasExited)
            {
                string? line = OutputReader?.ReadLine();
                if (line != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (previos_line == line && previos_line == "")
                        {
                            RemoveLastLine();
                            OutputBox.AppendText("\n_");
                        }

                        OutputBox.AppendText("\n> " + line);
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
                SendCommand(InputBox.Text);
                InputBox.Text = "";
            }
        }

        private void SendCommand(string command)
        {
            if (InputWriter != null)
            {
                InputWriter.WriteLine(command);
                InputWriter.Flush();
            }
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
    }
}
