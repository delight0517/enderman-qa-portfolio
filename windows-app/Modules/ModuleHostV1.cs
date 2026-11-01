using System.Security.Cryptography;
using System.IO;
using System.Windows.Controls;

namespace EndermanQaPortfolioViewer.Modules;

/// <summary>
/// In-process feature host. This composes a module from the already-loaded app assembly;
/// it does not load or hot-swap DLLs. Binary changes require an app restart.
/// </summary>
public sealed class ModuleHostV1<TModule> : IDisposable where TModule : class, IPortfolioModuleV1
{
    private readonly Panel _mountPoint;
    private TModule? _active;
    private bool _disposed;

    public ModuleHostV1(Panel mountPoint) => _mountPoint = mountPoint;

    public TModule? Active => _active;

    public ModuleChangeResult Apply(
        IPortfolioModuleFactoryV1 factory,
        string expectedModuleId,
        string expectedVersion,
        string expectedSourcePath,
        string expectedSourceSha256)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(factory);
        if (_active?.HasActiveWork == true)
            return new(false, "모듈이 작업 중이라 교체를 보류했습니다.");
        if (!string.Equals(factory.ModuleId, expectedModuleId, StringComparison.Ordinal) ||
            !string.Equals(factory.Version, expectedVersion, StringComparison.Ordinal) ||
            !string.Equals(factory.SourcePath, expectedSourcePath, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(factory.SourceSha256, expectedSourceSha256, StringComparison.OrdinalIgnoreCase))
            return new(false, "후보 모듈의 ID·버전·출처 해시가 기대값과 다릅니다.");

        string actualHash;
        try
        {
            actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(expectedSourcePath)));
        }
        catch (Exception ex)
        {
            return new(false, "모듈 출처를 읽지 못했습니다: " + ex.Message);
        }
        if (!string.Equals(actualHash, expectedSourceSha256, StringComparison.OrdinalIgnoreCase))
            return new(false, "로드된 모듈 파일 해시가 후보와 다릅니다.");

        var previous = _active;
        string snapshot;
        IPortfolioModuleV1? candidate = null;
        var staging = new StackPanel();
        try
        {
            snapshot = previous?.CaptureState() ?? string.Empty;
            candidate = factory.Create();
            Validate(candidate, expectedModuleId, expectedVersion, expectedSourcePath, actualHash);
            if (candidate is not TModule)
                throw new InvalidOperationException("생성된 모듈 형식이 호스트 형식과 다릅니다.");
            candidate.Initialize(staging);
            if (previous is not null)
                candidate.RestoreState(snapshot);
        }
        catch (Exception ex)
        {
            candidate?.Dispose();
            return new(false, "후보 모듈 준비에 실패했습니다. 이전 모듈은 유지됩니다: " + ex.Message);
        }

        // Commit only after candidate init and state restoration have both succeeded.
        _mountPoint.Children.Clear();
        while (staging.Children.Count > 0)
        {
            var child = staging.Children[0];
            staging.Children.RemoveAt(0);
            _mountPoint.Children.Add(child);
        }
        _active = (TModule)candidate;
        previous?.Dispose();
        return new(true, $"{candidate.ModuleId} · {candidate.Version} · SHA-256 {actualHash[..12]}");
    }

    private static void Validate(IPortfolioModuleV1 module, string id, string version, string path, string hash)
    {
        if (!string.Equals(module.ModuleId, id, StringComparison.Ordinal) ||
            !string.Equals(module.Version, version, StringComparison.Ordinal) ||
            !string.Equals(Path.GetFullPath(module.SourcePath), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(module.SourceSha256, hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("생성된 모듈의 출처 계약이 후보와 다릅니다.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _active?.Dispose();
        _active = null;
        _mountPoint.Children.Clear();
    }
}
