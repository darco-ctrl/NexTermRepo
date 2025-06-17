using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace NexTerm
{
    public class TabSystem
    {
        private MainWindow mw;

        public class TabData
        {
            public string outputlog { get; set; } = "";
            public string TabPath { get; set; } = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}";
            public List<string> TabCommandHistory { get; set; } = new();

            public PowerShell ps { get; set; } = PowerShell.Create();
        }

        public Dictionary<TabItem, TabData> nexTermTabs = new();

        public TabSystem(MainWindow mainwindow) 
        {
            mw = mainwindow;
            OnTabReady();
        }
        private void OnTabReady()
        {
            CreateNewTab();
        }

        public void CreateNewTab()
        {
            TabItem newTab = new TabItem();
            newTab.Name = $"NexTermTab{nexTermTabs.Count + 1}";

            StackPanel tabheader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0)
            };

            TextBox TabTextBox = new TextBox
            {
                Text = $"NexTerm{nexTermTabs.Count + 1}",
                Style = (Style)mw.FindResource("TabTextBox"),
                IsEnabled = false
            };
            ContentControl textcontainer = new ContentControl();
            textcontainer.Content = TabTextBox;

            Button TabClosebutton = new Button
            {
                Name = $"Button{nexTermTabs.Count + 1}",
                Style = (Style)mw.FindResource("TabButton"),
                IsEnabled = false
            };
            TabClosebutton.Click += mw.OnTabCloseButtonClick;

            tabheader.Children.Add(textcontainer);
            tabheader.Children.Add(TabClosebutton);

            newTab.Header = tabheader;

            mw.RegisterName(newTab.Name, newTab);

            Binding isSelectedBinding = new Binding("IsSelected")
            {
                Source = newTab
            };
            TabTextBox.SetBinding(TextBox.IsEnabledProperty, isSelectedBinding);
            TabClosebutton.SetBinding(Button.IsEnabledProperty, isSelectedBinding);

            mw.TabBlock.Items.Add(newTab);

            TabData newTabData = new TabData();

            nexTermTabs.Add(newTab, newTabData);

            mw.TabBlock.SelectedItem = newTab;
            SelectNewTab(newTab);
            mw.Terminal.SetPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }

        public void SelectNewTab(TabItem tab)
        {
            if (mw.Terminal.current_tab == tab) return;

            if (!nexTermTabs.TryGetValue(tab, out TabData? newData))
            {
                MessageBox.Show($"[Error] Couldn't find Requested TabItem {tab}");
                return;
            }

            if (mw.Terminal.current_tab != null &&
            nexTermTabs.TryGetValue(mw.Terminal.current_tab, out TabData? currentData))
            {
                currentData.TabPath = mw.Terminal.currentDir;
                currentData.outputlog = mw.Terminal.GetCurrentOutputLog();
                currentData.TabCommandHistory = mw.commandManager.CommandHistory;
            }

            // Load new tab data
            mw.Terminal.current_tab = tab;
            mw.Terminal.setOutputLog(newData.outputlog);
            mw.Terminal._ps = newData.ps;
            mw.commandManager.CommandHistory = newData.TabCommandHistory;
        }
    }
}
