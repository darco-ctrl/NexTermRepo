// NexTerm Terminal Engine v1.0
// Author: Darco
// Description: MainWindow.cs

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

        private TerminalEngine terminal;
        private Point _offset;
        private bool isMaximized = false;


        public MainWindow()
        {
            InitializeComponent();

            terminal = new TerminalEngine(OutputBox, InputBox, PathBlock, AnimationCanTypeBox);
            terminal.OnTerminalReady();

        }

        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                InputBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                return;
            }
            terminal.HandlePreviewKeyDown(e);
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;


        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            terminal.CloseNexTerm();
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
            terminal.InputCommandChanger(e);
        }
    }
}