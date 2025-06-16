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


        public Dictionary<string, (Action Cmd, string Description)> NTermCommands;

        public List<string> CommandHistory = new List<string>();

        public NexTermCommand(TextBox opBox, TextBox ibox, TerminalEngine term)
        {
            OutputBox = opBox;
            InputBox = ibox;
            Terminal = term;

            NTermCommands = new()
            {
                ["@clear"] = (ClearTerminal, "Clear Terminal Output."),
                ["@help"] = (NTShowHelp, "Shows all available NexTerm commands."),
                ["@ver"] = (NTversion, "Shows NexTerm version."),
                ["@history"] = (NTHistory, "Shows all command recently executed."),
            };
        }

        public void ExecuteCommand(string command)
        {
            if (NTermCommands.TryGetValue(command.ToLower(), out var cmd))
            {
                Terminal.PushToOutput($"> {command}");
                Terminal.AddToPreviousCommand(command);
                AddToHistory(command);
                cmd.Cmd();
            }
            else
            {
                Terminal.PushToOutput($"[ERROR] Unknown NexTerm command: {command}\nUse '@help' to see available commands.");
            }

        }

        private void ClearTerminal()
        {
            Terminal.ClearOutPut(" NexTerm is Ready \n\n Enter @help for NexTerm Commands\n\n");
        }

        private void NTShowHelp()
        {
            OutputBox.AppendText("\n\n");
            foreach (var entry in NTermCommands)
            {
                OutputBox.AppendText($"-- {entry.Key.PadRight(12)} - {entry.Value.Description}\n");
            }
            OutputBox.ScrollToEnd();
        }

        private void NTHistory()
        {
            if (CommandHistory.Count > 0) 
            {
                string histring = string.Join("\n", CommandHistory);
                Terminal.PushToOutput($"\n\nCommands History :-\n\n   Time        |   Command\n--------------------------------------\n{histring}\n");
            } else
            {
                Terminal.PushToOutput($"\n\nYou dont have any command history\n");
            }
        }

        private void NTversion()
        {
            Terminal.PushToOutput
            (
            """

            -- NexTerm v1.0.1 by Darco
            -- ShellEngine: PowerShell

            """
            );
        }

        public void AddToHistory(string command)
        {
            if (CommandHistory.Count >= MaxHistory)
                CommandHistory.RemoveAt(0);
            CommandHistory.Add($"{DateTime.Now: - hh:mm:ss tt}           -           {command}");
        }
    }
}
