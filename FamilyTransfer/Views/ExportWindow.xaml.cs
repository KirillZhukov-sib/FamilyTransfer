using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace FamilyTransfer.Views
{
    public partial class ExportWindow : Window
    {
        private bool _isPanelVisible = false;
        private const int PANEL_WIDTH = 520;
        private const int BASE_WIDTH = 800;
        private const int BASE_HEIGHT = 550;

        public ExportWindow()
        {
            InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.Width = BASE_WIDTH;
            this.Height = BASE_HEIGHT;
            this.MinHeight = 500;
            this.MinWidth = 750;

            SettingsPanel.Visibility = Visibility.Collapsed;
            SettingsPanel.Width = 0;

            ToggleButton.Content = ">";
            _isPanelVisible = false;
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isPanelVisible = !_isPanelVisible;

            if (_isPanelVisible)
            {
                ShowPanel();
            }
            else
            {
                HidePanel();
            }
        }

        private void ShowPanel()
        {
            SettingsPanel.Visibility = Visibility.Visible;

            DoubleAnimation widthAnimation = new DoubleAnimation
            {
                From = 0,
                To = PANEL_WIDTH,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            SettingsPanel.BeginAnimation(FrameworkElement.WidthProperty, widthAnimation);

            ToggleButton.Content = "<";

            DoubleAnimation windowWidthAnimation = new DoubleAnimation
            {
                From = BASE_WIDTH,
                To = BASE_WIDTH + PANEL_WIDTH,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            this.BeginAnimation(FrameworkElement.WidthProperty, windowWidthAnimation);
        }

        private void HidePanel()
        {
            DoubleAnimation widthAnimation = new DoubleAnimation
            {
                From = PANEL_WIDTH,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            widthAnimation.Completed += (s, e) =>
            {
                SettingsPanel.Visibility = Visibility.Collapsed;
            };

            SettingsPanel.BeginAnimation(FrameworkElement.WidthProperty, widthAnimation);

            ToggleButton.Content = ">";

            DoubleAnimation windowWidthAnimation = new DoubleAnimation
            {
                From = BASE_WIDTH + PANEL_WIDTH,
                To = BASE_WIDTH,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            this.BeginAnimation(FrameworkElement.WidthProperty, windowWidthAnimation);
        }

        protected override void OnClosed(EventArgs e)
        {
            this.BeginAnimation(FrameworkElement.WidthProperty, null);
            SettingsPanel.BeginAnimation(FrameworkElement.WidthProperty, null);
            base.OnClosed(e);
        }
    }
}