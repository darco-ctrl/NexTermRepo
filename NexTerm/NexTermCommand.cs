// NexTerm Terminal Engine v1.1.0
// Author: Darco
// Description: NexTermCommands Handler


using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace NexTerm
{
    public class NexTermCommand
    {
        private int MaxHistory = 100;

        private MainWindow mainWindow;

        public Dictionary<string, (Action<string[]> Cmd, string Description)> Commands;

        public List<string> CommandHistory = new List<string>();

        public NexTermCommand(MainWindow mainwindow)
        {
            this.mainWindow = mainwindow;

            Commands = new()
            {
                ["@clear"] = (args => ClearTerminal(args), "Clear Terminal Output."),
                ["@help"] = (args => NTShowHelp(args), "Shows all available NexTerm commands."),
                ["@ver"] = (args => NTversion(args), "Shows NexTerm version."),
                ["@history"] = (args => NTHistory(args), "Shows all recently executed commands.")
            };
        }

        public void ExecuteCommand(string command)
        {
            command = command.Trim();

            string[] parts = command.Trim().Split(' ', 2);
            string cmdName = parts[0].ToLower();
            string[] args = parts.Length > 1 ? parts[1].Split(' ') : Array.Empty<string>();

            mainWindow.Terminal.PushToOutput($"\n> {command}");
            if (Commands.TryGetValue(cmdName, out var cmd))
            {
                mainWindow.Terminal.AddToPreviousCommand(command);

                AddToHistory(command, false);

                cmd.Cmd(args);
            }
            else
            {
                mainWindow.Terminal.ShowError($"Unknown NexTerm command: {command}\nUse '@help' to see available commands.");
            }
        }
        private void ClearTerminal(string[] args)
        {
            if (args.Length == 1) 
            {
                if (args[0].Trim().ToLower() == "-force")
                {
                    mainWindow.Terminal.SetOutput("");
                } else
                {
                    mainWindow.Terminal.ShowError($"Unknown arguement '{args[0]}'");
                }
                
            } else if (args.Length > 1)
            {
                mainWindow.Terminal.ShowError("The @clear command only supports a single argument");
            } else
            {
                mainWindow.Terminal.SetOutput
                (
                """
                   ╔══════════════════════════════════════════════╗
                   ║                                              ║
                   ║               NexTerm v1.1.0                 ║
                   ║         Shell Engine: PowerShell             ║
                   ║                                              ║
                   ╚══════════════════════════════════════════════╝
                """
                );
            }

        }

        private void NTShowHelp(string[] args)
        {
            mainWindow.OutputBox.AppendText("\n\n");
            foreach (var entry in Commands)
            {
                mainWindow.OutputBox.AppendText($"-- {entry.Key.PadRight(12)} - {entry.Value.Description}\n");
            }
            mainWindow.OutputBox.ScrollToEnd();
        }

        private void NTHistory(string[] args)
        {
            if (CommandHistory.Count == 0)
            {
                mainWindow.Terminal.PushToOutput("\n\nYou don't have any command history.\n");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("\n\nCommands History :-\n");
            sb.AppendLine("       Time    |    Command       ");
            sb.AppendLine("──────────────────────────────────");

            foreach (string entry in CommandHistory)
            {
                sb.AppendLine(entry);
            }

            mainWindow.Terminal.PushToOutput(sb.ToString());
        }

        private void NTversion(string[] args)
        {
            mainWindow.Terminal.PushToOutput
            (
            """
               ╔══════════════════════════════════════════════╗
               ║              NexTerm v1.1.0                  ║
               ║        Shell Engine: PowerShell              ║
               ║     Created by: Darco (Git: darco-ctrl)      ║
               ╚══════════════════════════════════════════════╝
            """
            );
        }

        public void AddToHistory(string command, bool isShellCommand)
        {
            if (CommandHistory.Count >= MaxHistory)
                CommandHistory.RemoveAt(0);

            string prefix = isShellCommand ? ">" : "@";
            CommandHistory.Add($"{DateTime.Now: - hh:mm:ss tt} | {prefix} {command}");
        }

    }
}
