using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace NexTerm
{
    internal class NexTermCommand
    {
        private TextBox InputBox;
        private TextBox OutputBox;

        public Dictionary<string, Action> NTermCommands;

        public List<string> CommandHistory = new List<string>();

        public NexTermCommand(TextBox opBox, TextBox ibox)
        {
            OutputBox = opBox;
            InputBox = ibox;

            NTermCommands = new Dictionary<string, Action>
            {
                ["@clear"] = ClearTerminal,
                ["@help"] = NTHelp,
                ["@exit"] = NTExit,
            };
        }

        public void CommandExcuter(string command)
        {
            Action? action;
            if (NTermCommands.TryGetValue(command.ToLower(), out action))
            {
                CommandHistory.Add(command);
                action!();
            }
            else
            {
                OutputBox.AppendText($"\nNexTerm Command Not Found, Enter '@Help' for available NexTerm commands");
            }

        }

        private void ClearTerminal()
        {
            OutputBox.Text = "> NexTerm is Ready";
        }

        private void NTHelp()
        {
            OutputBox.AppendText
            ("""
                
            > @help

            -- @help - Shows all available NexTerm commands.
            -- @clear - Clear Terminal Output

            """);
        }

        private void NTExit()
        {

        }

        private void NTHistory()
        {

        }
    }
}
