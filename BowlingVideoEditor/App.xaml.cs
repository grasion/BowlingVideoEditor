using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using BowlingVideoEditor.Services;
using BowlingVideoEditor.Views;

namespace BowlingVideoEditor
{
    public partial class App : Application
    {
        private static readonly string LogPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "error.log");

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 전역 예외 핸들러
            DispatcherUnhandledException += (s, args) =>
            {
                LogError("DispatcherUnhandledException", args.Exception);
                MessageBox.Show($"오류 발생:\n{args.Exception.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    LogError("UnhandledException", ex);
            };

            try
            {
                var setupService = new FFmpegSetupService();

                var splashWindow = new Window
                {
                    Title = "볼링 영상 편집기 - 초기화",
                    Width = 420, Height = 140,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.ToolWindow,
                    Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(30, 30, 30))
                };

                var panel = new System.Windows.Controls.StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(20)
                };

                var statusText = new System.Windows.Controls.TextBlock
                {
                    Text = "초기화 중...",
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                var progressBar = new System.Windows.Controls.ProgressBar
                {
                    Height = 20, Minimum = 0, Maximum = 100
                };

                panel.Children.Add(statusText);
                panel.Children.Add(progressBar);
                splashWindow.Content = panel;
                splashWindow.Show();

                var progress = new Progress<(string Status, double Percent)>(report =>
                {
                    statusText.Text = report.Status;
                    progressBar.Value = report.Percent;
                });

                bool success;
                try
                {
                    success = await setupService.EnsureFFmpegAsync(progress);
                }
                catch (Exception ex)
                {
                    LogError("FFmpeg setup", ex);
                    success = false;
                }

                splashWindow.Close();

                if (!success)
                {
                    var result = MessageBox.Show(
                        "FFmpeg 자동 설치에 실패했습니다.\n" +
                        "영상 편집 기능은 FFmpeg 설치 후 사용 가능합니다.\n\n" +
                        "프로그램을 계속 실행하시겠습니까?",
                        "FFmpeg 필요",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                    {
                        Shutdown();
                        return;
                    }
                }

                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                LogError("Application_Startup", ex);
                MessageBox.Show(
                    $"앱 시작 중 오류:\n\n{ex.Message}\n\n상세 로그: {LogPath}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private static void LogError(string context, Exception ex)
        {
            try
            {
                var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}]\n{ex}\n\n";
                File.AppendAllText(LogPath, msg);
            }
            catch { }
        }
    }
}
