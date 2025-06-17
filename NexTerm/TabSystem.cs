using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace NexTerm
{
    class TabSystem
    {
        private MainWindow mw;
        private TerminalEngine terminal;

        public class TabData
        {
            public string TabName { get; set; } = "New Tab";
            public string TabTitle { get; set; } = "";
            public string TabPath { get; set; } = "New Tab";
            public List<string> TabCommandHistory { get; set; } = new();
        }

        public Dictionary<TabItem, TabData> TerminalTabs = new();

        public TabSystem(MainWindow mainwindow, TerminalEngine term) 
        {
            mw = mainwindow;
            terminal = term;
        }
    }
}
