// NexTerm Terminal Engine v1.1.0
// Author: Darco
// Description: Core

using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NexTerm
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public partial class MainWindow : Window
    {

        public TerminalEngine Terminal;
        public TabSystem TabManager;
        public NexTermCommand commandManager;

        private bool isMaximized = false;


        public MainWindow()
        {
            InitializeComponent();

            this.Terminal = new TerminalEngine(this);
            this.commandManager = new NexTermCommand(this);
            this.TabManager = new TabSystem(this);

            Terminal.OnTerminalReady();
        }

        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                InputBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                return;
            }
            Terminal.HandlePreviewKeyDown(e);
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;


        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Terminal.CloseNexTerm();
            this.Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Keyboard.ClearFocus();
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
            if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
                ToggleMaximize(sender, e);
        }

        private void ToggleMaximize(object sender, RoutedEventArgs e)
        {
            WindowState = isMaximized ? WindowState.Normal : WindowState.Maximized;
            isMaximized = !isMaximized;
        }

        private void PreviousCommand(object sender, KeyEventArgs e)
        {
            Terminal.InputCommandChanger(e);
        }

        public void OnTabCloseButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button closeButton && closeButton.Tag is TabItem tabToClose)
            {
                int idx = TabBlock.Items.IndexOf(tabToClose);
                if (TabBlock.Items.Count > 1)
                { 

                    if (TabManager.nexTermTabs.TryGetValue(tabToClose, out var tabData))
                    {
                        tabData.ps.Dispose();
                        TabManager.nexTermTabs.Remove(tabToClose);
                    }

                    TabBlock.Items.Remove(tabToClose);

                    if (TabBlock.Items.Count > 0)
                    {
                        TabBlock.SelectedIndex = idx - 1;
                    }
                } else
                {
                    CloseButton_Click(sender, e);
                }
            }
        }

        private void OnAddTabClicked(object sender, RoutedEventArgs e)
        {
            if (TabManager.nexTermTabs.Count < 9)
            {
                TabManager.CreateNewTab();
            } else
            {
                MessageBox.Show("Maximum of 9 tabs allowed.", "Limit Reached", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
        }

        private void TabBlock_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TabItem? selectedTab = TabBlock.SelectedItem as TabItem;
            if (selectedTab != null && TabManager != null)
                TabManager.SelectNewTab(selectedTab);
        }
    }
}