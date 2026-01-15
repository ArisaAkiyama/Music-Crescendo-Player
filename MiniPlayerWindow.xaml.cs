using System.Windows;
using System.Windows.Input;
using DesktopMusicPlayer.ViewModels;

namespace DesktopMusicPlayer
{
    public partial class MiniPlayerWindow : Window
    {
        public MiniPlayerWindow()
        {
            InitializeComponent();
            this.Loaded += MiniPlayerWindow_Loaded;
        }

        private void MiniPlayerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Re-center based on ActualWidth (since title length varies)
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            this.Left = (screenWidth - this.ActualWidth) / 2;
            
            // Fade In Animation
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(1.0, TimeSpan.FromSeconds(0.2));
            this.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void ExpandButton_Click(object sender, RoutedEventArgs e)
        {
            // Fade Out Animation
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(0.0, TimeSpan.FromSeconds(0.2));
            fadeOut.Completed += (s, _) => this.Close();
            this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        // --- Seek Functionality ---
        private bool _wasPlayingBeforeDrag = false;

        private void Slider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.IsDragging = true;
                _wasPlayingBeforeDrag = viewModel.IsPlaying;
                if (_wasPlayingBeforeDrag)
                {
                    viewModel.PauseCommand.Execute(null);
                }
            }
        }

        private void Slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.IsDragging = false;
                viewModel.SeekEndCommand.Execute(null);
                if (_wasPlayingBeforeDrag)
                {
                    viewModel.PlayCommand.Execute(null);
                }
            }
        }

        private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.IsDragging = true;
            }
        }

        private void Slider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.SeekEndCommand.Execute(null);
            }
        }
    }
}
