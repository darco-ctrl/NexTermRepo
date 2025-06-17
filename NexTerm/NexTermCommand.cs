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
    internal class NexTermCommand
    {
        private int MaxHistory = 100;

        private TextBox InputBox;
        private TextBox OutputBox;
        private TerminalEngine Terminal;

        public Dictionary<string, (Action<string[]> Cmd, string Description)> Commands;

        public List<string> CommandHistory = new List<string>();

        public NexTermCommand(TextBox opBox, TextBox ibox, TerminalEngine term)
        {
            OutputBox = opBox;
            InputBox = ibox;
            Terminal = term;

            Commands = new()
            {

                ["@clear"] = (args => ClearTerminal(args), "Clear Terminal Output."),
                ["@help"] = (args => NTShowHelp(args), "Shows all available NexTerm commands."),
                ["@ver"] = (args => NTversion(args), "Shows NexTerm version."),
                ["@history"] = (args => NTHistory(args), "Shows all command recently executed.")
            };

            Commands["@clear"] = (args => ClearTerminal(args), "Clear Terminal Output");

        }

        public void ExecuteCommand(string command)
        {
            command = command.Trim();

            string[] parts = command.Trim().Split(' ', 2);
            string cmdName = parts[0].ToLower();
            string[] args = parts.Length > 1 ? parts[1].Split(' ') : Array.Empty<string>();

            if (Commands.TryGetValue(cmdName, out var cmd))
            {

                Terminal.PushToOutput($"\n> {command}");
                Terminal.AddToPreviousCommand(command);

                AddToHistory(command, false);

                cmd.Cmd(args);
            }
            else
            {
                Terminal.ShowError("Unknown NexTerm command: { command}\nUse '@help' to see available commands.");
            }

        }

        private void ClearTerminal(string[] args)
        {
            Terminal.ClearOutPut(" NexTerm is Ready \n\n Enter @help for NexTerm Commands\n\n");
        }

        private void NTShowHelp(string[] args)
        {
            OutputBox.AppendText("\n\n");
            foreach (var entry in Commands)
            {
                OutputBox.AppendText($"-- {entry.Key.PadRight(12)} - {entry.Value.Description}\n");
            }
            OutputBox.ScrollToEnd();
        }

        private void NTHistory(string[] args)
        {

            if (CommandHistory.Count == 0)
            {
                Terminal.PushToOutput("\n\nYou don't have any command history.\n");
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

            Terminal.PushToOutput(sb.ToString());
        }

        private void NTversion(string[] args)
        {
            Terminal.PushToOutput
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
