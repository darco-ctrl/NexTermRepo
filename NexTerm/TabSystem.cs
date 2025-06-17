using System;
using System.Collections.Generic;
using System.IO;
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

            public string CurrentCommand { get; set; } = "";

            public PowerShell ps { get; private set; } = PowerShell.Create();

            public PSDataCollection<PSObject> OutputCollection { get; private set; } = new PSDataCollection<PSObject>();

            private bool _HandlersAttached = false;

            public TabData() 
            {
                ps = PowerShell.Create();
                ps.AddCommand("Set-Location").AddArgument(TabPath);
                ps.Invoke();
                ps.Commands.Clear();
            }

            public void InitiateHandlers(Action<string> pushToOutput, Action<string> updatePath)
            {
                if (_HandlersAttached) return;

                OutputCollection.DataAdded += (sender, e) =>
                {

                    string text = OutputCollection[e.Index]?.BaseObject?.ToString()?.Trim() ?? "";

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Directory.Exists(text))
                            {
                                updatePath(text);

                                if (!CurrentCommand.Contains("Get-Location") && !CurrentCommand.Contains("cd"))
                                    text = "";
                            }

                            if (!string.IsNullOrWhiteSpace(text))
                                pushToOutput($" {text}");
                        });
                    }
                };

                ps.Streams.Error.DataAdded += (sender, e) =>
                {
                    var errors = ps.Streams.Error;
                    var error = errors[e.Index];

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        pushToOutput($"\n[PowerShell Error] {error.Exception.Message}");
                    });
                };

                _HandlersAttached = true;
            }
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
            PSActivator(nexTermTabs[newTab]);

            mw.TabBlock.SelectedItem = newTab;
            SelectNewTab(newTab);
        }

        private void PSActivator(TabData session)
        {
            session.InitiateHandlers(mw.Terminal.PushToOutput, mw.Terminal.UpdateDirectory);
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
            mw.Terminal._ps = newData.ps;
            mw.Terminal.OutputCollection = newData.OutputCollection;
            mw.Terminal.current_tab = tab;
            mw.Terminal.UpdateDirectory(newData.TabPath);
            mw.Terminal.setOutputLog(newData.outputlog);
            mw.commandManager.CommandHistory = newData.TabCommandHistory;
        }
    }
}
