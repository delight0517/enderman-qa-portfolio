using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EndermanQaPortfolioViewer.Modules;

namespace EndermanQaPortfolioViewer;

public sealed class PortfolioViewerWindow : Window
{
    private readonly StackPanel _moduleMount = new();
    private readonly TextBlock _moduleStatus = new();
    private readonly ModuleHostV1<IPortfolioModuleV1> _moduleHost;
    private readonly CaseBrowserModuleFactoryV1 _moduleFactory = new();
    private readonly string _assemblyPath;
    private readonly string _assemblySha256;

    public PortfolioViewerWindow()
    {
        Title = "Enderman QA Portfolio Viewer";
        Width = 1420;
        Height = 930;
        MinWidth = 1024;
        MinHeight = 720;
        Background = new SolidColorBrush(Color.FromRgb(243, 246, 251));
        FontFamily = new FontFamily("Segoe UI");
        _assemblyPath = Assembly.GetExecutingAssembly().Location;
        _assemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(_assemblyPath)));
        _moduleHost = new ModuleHostV1<IPortfolioModuleV1>(_moduleMount);

        var root = new DockPanel();
        var banner = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(23, 34, 53)),
            Padding = new Thickness(18, 9, 18, 9),
            Child = new TextBlock
            {
                Text = "독립 공개 결과 뷰어 · Enderman QA Workbench 공식 앱이 아닙니다 · 데이터는 2026-10-31 정적 스냅샷입니다.",
                Foreground = Brushes.White, FontSize = 11, TextWrapping = TextWrapping.Wrap
            }
        };
        DockPanel.SetDock(banner, Dock.Bottom);
        root.Children.Add(banner);
        root.Children.Add(_moduleMount);
        Content = root;
        _moduleStatus.Visibility = Visibility.Collapsed;
        Loaded += (_, _) => ApplyModule();
        Closed += (_, _) => _moduleHost.Dispose();
    }

    private void ApplyModule()
    {
        var result = _moduleHost.Apply(
            _moduleFactory,
            CaseBrowserModuleV1.Id,
            CaseBrowserModuleV1.ContractVersion,
            _assemblyPath,
            _assemblySha256);
        if (result.Applied && _moduleHost.Active is CaseBrowserModuleV1 browser)
        {
            browser.ReloadRequested += OnReloadRequested;
            _moduleStatus.Text = result.Message;
        }
        else if (!result.Applied)
        {
            MessageBox.Show(this, result.Message, "화면 모듈", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnReloadRequested(object? sender, EventArgs e) => ApplyModule();
}
