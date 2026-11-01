using System.Windows;
using System.Windows.Controls;

namespace EndermanQaPortfolioViewer.Modules;

public interface IPortfolioModuleV1 : IDisposable
{
    string ModuleId { get; }
    string Version { get; }
    string SourcePath { get; }
    string SourceSha256 { get; }
    bool HasActiveWork { get; }
    string CaptureState();
    void Initialize(Panel mountPoint);
    void RestoreState(string snapshot);
}

public interface IPortfolioModuleFactoryV1
{
    string ModuleId { get; }
    string Version { get; }
    string SourcePath { get; }
    string SourceSha256 { get; }
    IPortfolioModuleV1 Create();
}

public sealed record ModuleChangeResult(bool Applied, string Message);
