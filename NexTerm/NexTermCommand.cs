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

        private MainWindow mw;

        public Dictionary<string, (Action<string[]> Cmd, string Description)> Commands;

        public List<string> CommandHistory = new List<string>();

        public NexTermCommand(MainWindow mainwindow)
        {
            this.mw = mainwindow;

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

            if (Commands.TryGetValue(cmdName, out var cmd))
            {

                mw.Terminal.PushToOutput($"\n> {command}");
                mw.Terminal.AddToPreviousCommand(command);

                AddToHistory(command, false);

                cmd.Cmd(args);
            }
            else
            {
                mw.Terminal.ShowError("Unknown NexTerm command: { command}\nUse '@help' to see available commands.");
            }
        }

        private void ClearTerminal(string[] args)
        {
            mw.Terminal.ClearOutPut(" NexTerm is Ready \n\n Enter @help for NexTerm Commands\n\n");
        }

        private void NTShowHelp(string[] args)
        {
            mw.OutputBox.AppendText("\n\n");
            foreach (var entry in Commands)
            {
                mw.OutputBox.AppendText($"-- {entry.Key.PadRight(12)} - {entry.Value.Description}\n");
            }
            mw.OutputBox.ScrollToEnd();
        }

        private void NTHistory(string[] args)
        {
            if (CommandHistory.Count == 0)
            {
                mw.Terminal.PushToOutput("\n\nYou don't have any command history.\n");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("\n\nCommands History :-\n");
            sb.AppendLine("   Time        |   Command");
            sb.AppendLine("--------------------------------------");

            foreach (string entry in CommandHistory)
            {
                sb.AppendLine(entry);
            }

            mw.Terminal.PushToOutput(sb.ToString());
        }

        private void NTversion(string[] args)
        {
            mw.Terminal.PushToOutput
            (
            """

            -- NexTerm v2.0
            -- ShellEngine: PowerShell

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
