using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EndermanQaPortfolioViewer.Modules;

namespace EndermanQaPortfolioViewer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var app = new Application
        {
            ShutdownMode = args.Contains("--verify", StringComparer.OrdinalIgnoreCase)
                ? ShutdownMode.OnExplicitShutdown
                : ShutdownMode.OnMainWindowClose
        };
        if (args.Contains("--verify", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                VerifyModuleLifecycle();
                Console.WriteLine("PASS: dataset, search, module reload/state restore, stale-source rejection, init-failure rollback");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex.Message);
                return 2;
            }
            finally
            {
                app.Shutdown();
            }
        }

        app.Run(new PortfolioViewerWindow());
        return 0;
    }

    private static void VerifyModuleLifecycle()
    {
        var mount = new StackPanel();
        using var host = new ModuleHostV1<IPortfolioModuleV1>(mount);
        var factory = new CaseBrowserModuleFactoryV1();
        var applied = host.Apply(factory, factory.ModuleId, factory.Version, factory.SourcePath, factory.SourceSha256);
        if (!applied.Applied) throw new InvalidOperationException(applied.Message);

        var search = Find<TextBox>(mount) ?? throw new InvalidOperationException("Search input is missing.");
        search.Text = "TC-014";
        var grid = Find<DataGrid>(mount) ?? throw new InvalidOperationException("Case grid is missing.");
        if (grid.Items.Count != 1 || (grid.Items[0] as CaseRecord)?.Id != "TC-014")
            throw new InvalidOperationException("Search filter did not return TC-014.");
        grid.SelectedItem = grid.Items[0];
        var category = Find<ComboBox>(mount) ?? throw new InvalidOperationException("Category filter is missing.");
        category.SelectedItem = (grid.SelectedItem as CaseRecord)?.Category;
        if (grid.Items.Count != 1 || (grid.Items[0] as CaseRecord)?.Id != "TC-014")
            throw new InvalidOperationException("Category filter did not retain the matching case.");
        var expectedCategory = category.SelectedItem?.ToString();

        var reloaded = host.Apply(factory, factory.ModuleId, factory.Version, factory.SourcePath, factory.SourceSha256);
        if (!reloaded.Applied) throw new InvalidOperationException(reloaded.Message);
        var restoredSearch = Find<TextBox>(mount) ?? throw new InvalidOperationException("Search input missing after module reload.");
        var restoredGrid = Find<DataGrid>(mount) ?? throw new InvalidOperationException("Case grid missing after module reload.");
        var restoredCategory = Find<ComboBox>(mount) ?? throw new InvalidOperationException("Category filter missing after module reload.");
        if (restoredGrid.Items.Count != 1 || restoredSearch.Text != "TC-014" || restoredCategory.SelectedItem?.ToString() != expectedCategory ||
            (restoredGrid.SelectedItem as CaseRecord)?.Id != "TC-014")
            throw new InvalidOperationException("Search/selection state did not survive module reload.");

        var active = host.Active;
        var badFactory = new FailingModuleFactory(factory.SourcePath, factory.SourceSha256);
        var rejected = host.Apply(badFactory, badFactory.ModuleId, badFactory.Version, badFactory.SourcePath, badFactory.SourceSha256);
        if (rejected.Applied || !ReferenceEquals(host.Active, active) ||
            (Find<DataGrid>(mount)?.SelectedItem as CaseRecord)?.Id != "TC-014")
            throw new InvalidOperationException("Failed candidate did not preserve the active module and UI state.");

        var stale = host.Apply(factory, factory.ModuleId, factory.Version, factory.SourcePath, new string('0', 64));
        if (stale.Applied || !ReferenceEquals(host.Active, active))
            throw new InvalidOperationException("Stale source was not rejected.");
    }

    private static T? Find<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T found) return found;
        foreach (var child in LogicalTreeHelper.GetChildren(root))
            if (child is DependencyObject dependency && Find<T>(dependency) is { } match) return match;
        return null;
    }

    private sealed class FailingModuleFactory(string sourcePath, string sourceSha256) : IPortfolioModuleFactoryV1
    {
        public string ModuleId => CaseBrowserModuleV1.Id;
        public string Version => CaseBrowserModuleV1.ContractVersion;
        public string SourcePath => sourcePath;
        public string SourceSha256 => sourceSha256;
        public IPortfolioModuleV1 Create() => new FailingModule(SourcePath, SourceSha256);
    }

    private sealed class FailingModule(string sourcePath, string sourceSha256) : IPortfolioModuleV1
    {
        public string ModuleId => CaseBrowserModuleV1.Id;
        public string Version => CaseBrowserModuleV1.ContractVersion;
        public string SourcePath => sourcePath;
        public string SourceSha256 => sourceSha256;
        public bool HasActiveWork => false;
        public string CaptureState() => string.Empty;
        public void Initialize(Panel mountPoint) => throw new InvalidOperationException("synthetic init failure");
        public void RestoreState(string snapshot) { }
        public void Dispose() { }
    }
}
