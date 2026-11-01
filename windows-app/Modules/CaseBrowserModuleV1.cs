using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace EndermanQaPortfolioViewer.Modules;

public sealed class CaseBrowserModuleFactoryV1 : IPortfolioModuleFactoryV1
{
    public string ModuleId => CaseBrowserModuleV1.Id;
    public string Version => CaseBrowserModuleV1.ContractVersion;
    public string SourcePath => Assembly.GetExecutingAssembly().Location;
    public string SourceSha256 => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(SourcePath)));
    public IPortfolioModuleV1 Create() => new CaseBrowserModuleV1(SourcePath, SourceSha256);
}

public sealed class CaseBrowserModuleV1 : IPortfolioModuleV1
{
    public const string Id = "enderman-public-case-browser";
    public const string ContractVersion = "1.0.0";

    private const string Ink = "#172235";
    private const string Muted = "#65758B";
    private const string Accent = "#4F46E5";
    private readonly string _sourcePath;
    private readonly string _sourceSha256;
    private PortfolioDataset? _dataset;
    private ObservableCollection<CaseRecord> _visibleCases = [];
    private TextBox? _search;
    private ComboBox? _category;
    private DataGrid? _grid;
    private StackPanel? _detail;
    private TextBlock? _status;
    private Button? _reload;
    private RoutedEventHandler? _reloadHandler;
    private TextChangedEventHandler? _searchHandler;
    private SelectionChangedEventHandler? _categoryHandler;
    private SelectionChangedEventHandler? _gridHandler;
    private bool _suppress;
    private Panel? _mount;

    public CaseBrowserModuleV1(string sourcePath, string sourceSha256)
    {
        _sourcePath = Path.GetFullPath(sourcePath);
        _sourceSha256 = sourceSha256;
    }

    public string ModuleId => Id;
    public string Version => ContractVersion;
    public string SourcePath => _sourcePath;
    public string SourceSha256 => _sourceSha256;
    public bool HasActiveWork => false;

    public void Initialize(Panel mountPoint)
    {
        ArgumentNullException.ThrowIfNull(mountPoint);
        var dataPath = Path.Combine(AppContext.BaseDirectory, "data", "cases.json");
        var json = File.ReadAllText(dataPath);
        _dataset = JsonSerializer.Deserialize<PortfolioDataset>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("QA 공개 데이터가 비어 있습니다.");
        ValidateDataset(_dataset);
        _mount = mountPoint;
        BuildView(mountPoint);
        RefreshRows(selectFirst: true);
        _status!.Text = InitialStatus();
    }

    public string CaptureState()
    {
        var state = new BrowserState(
            _search?.Text ?? string.Empty,
            _category?.SelectedItem?.ToString() ?? "전체 분류",
            (_grid?.SelectedItem as CaseRecord)?.Id);
        return JsonSerializer.Serialize(state);
    }

    public void RestoreState(string snapshot)
    {
        if (_search is null || _category is null || _dataset is null) return;
        if (string.IsNullOrWhiteSpace(snapshot)) return;
        var state = JsonSerializer.Deserialize<BrowserState>(snapshot);
        if (state is null) return;
        _suppress = true;
        _search.Text = state.Search ?? string.Empty;
        _category.SelectedItem = _category.Items.Cast<string>().FirstOrDefault(x => x == state.Category) ?? "전체 분류";
        _suppress = false;
        RefreshRows(selectFirst: false);
        var selected = _visibleCases.FirstOrDefault(x => x.Id == state.SelectedId) ?? _visibleCases.FirstOrDefault();
        _grid!.SelectedItem = selected;
        ShowDetails(selected);
        _status!.Text = InitialStatus();
    }

    private void BuildView(Panel mount)
    {
        mount.Children.Clear();
        var page = new Grid { Background = Brush("#F3F6FB"), Margin = new Thickness(20) };
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mount.Children.Add(page);

        var heading = new Grid { Margin = new Thickness(0, 0, 0, 15) };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleStack = new StackPanel();
        titleStack.Children.Add(Text("ENDERMAN QA · PORTFOLIO", 11, Accent, true));
        titleStack.Children.Add(Text("100개 테스트 케이스", 27, Ink, true));
        titleStack.Children.Add(Text("공개 QA 사례를 검색하고, 케이스별 확인 범위와 한계를 살펴보는 독립 뷰어입니다.", 13, Muted));
        heading.Children.Add(titleStack);
        var disclosure = new Border
        {
            Background = Brush("#E8EAFE"), CornerRadius = new CornerRadius(10), Padding = new Thickness(13, 9, 13, 9),
            Child = Text("Enderman QA Workbench 공식 앱이 아닌 포트폴리오 뷰어", 11, Accent, true),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(disclosure, 1);
        heading.Children.Add(disclosure);
        Grid.SetRow(heading, 0);
        page.Children.Add(heading);

        var intro = new Border
        {
            Background = Brushes.White, BorderBrush = Brush("#DCE4F0"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 13)
        };
        var introStack = new StackPanel();
        introStack.Children.Add(Text("프로젝트 소개", 14, Ink, true));
        introStack.Children.Add(Text("Minecraft Java 엔더맨의 피해·이동·사망 동작을 사전 조건, 예상 행동, 관측 기준으로 나눠 확인합니다. 부분 서버 관측은 전체 클라이언트 동작을 보증하지 않습니다.", 12, Muted));
        intro.Child = introStack;
        Grid.SetRow(intro, 1);
        page.Children.Add(intro);

        var summary = BuildSummary();
        Grid.SetRow(summary, 2);
        page.Children.Add(summary);

        var work = new Grid { Margin = new Thickness(0, 13, 0, 0) };
        work.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.02, GridUnitType.Star) });
        work.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.98, GridUnitType.Star) });
        var left = BuildCaseList();
        work.Children.Add(left);
        var right = BuildDetails();
        Grid.SetColumn(right, 1);
        work.Children.Add(right);
        Grid.SetRow(work, 3);
        page.Children.Add(work);

        var footer = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _status = Text("불러오는 중…", 10, Muted);
        footer.Children.Add(_status);
        _reload = new Button
        {
            Content = "화면 모듈 다시 적용", Padding = new Thickness(12, 6, 12, 6),
            Background = Brushes.White, BorderBrush = Brush("#DCE4F0"), Foreground = Brush(Ink)
        };
        Grid.SetColumn(_reload, 1);
        footer.Children.Add(_reload);
        Grid.SetRow(footer, 4);
        page.Children.Add(footer);
        AttachHandlers();
    }

    private UIElement BuildSummary()
    {
        var panel = new Grid();
        for (var i = 0; i < 4; i++) panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        string[] labels = ["전체 범위 Pass", "부분 범위 Pass", "미확정", "미실행"];
        string[] keys = ["기록 범위 Pass", "부분 범위 Pass", "미확정", "미실행"];
        for (var i = 0; i < labels.Length; i++)
        {
            var value = _dataset!.Counts.TryGetValue(keys[i], out var count) ? count.ToString() : "0";
            var card = new Border
            {
                Background = Brushes.White, BorderBrush = Brush("#DCE4F0"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11), Padding = new Thickness(13), Margin = new Thickness(i == 0 ? 0 : 7, 0, 7, 0)
            };
            var stack = new StackPanel();
            stack.Children.Add(Text(value, 24, i < 2 ? Accent : Muted, true));
            stack.Children.Add(Text(labels[i], 11, Muted));
            card.Child = stack;
            Grid.SetColumn(card, i);
            panel.Children.Add(card);
        }
        return panel;
    }

    private UIElement BuildCaseList()
    {
        var card = Card();
        var stack = new DockPanel();
        card.Child = stack;
        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        heading.Children.Add(Text("케이스 목록", 15, Ink, true));
        var filters = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        _search = new TextBox { Height = 34, Padding = new Thickness(9, 6, 9, 6), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "ID, 케이스명, 기대 행동 검색" };
        _category = new ComboBox { Height = 34, Margin = new Thickness(8, 0, 0, 0), VerticalContentAlignment = VerticalAlignment.Center };
        _category.Items.Add("전체 분류");
        foreach (var category in _dataset!.Cases.Select(x => x.Category).Distinct().OrderBy(x => x, StringComparer.CurrentCulture)) _category.Items.Add(category);
        _category.SelectedIndex = 0;
        filters.Children.Add(_search);
        Grid.SetColumn(_category, 1);
        filters.Children.Add(_category);
        heading.Children.Add(filters);
        DockPanel.SetDock(heading, Dock.Top);
        stack.Children.Add(heading);

        _grid = new DataGrid
        {
            ItemsSource = _visibleCases, AutoGenerateColumns = false, IsReadOnly = true,
            CanUserAddRows = false, CanUserDeleteRows = false, CanUserReorderColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            RowHeaderWidth = 0, SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow,
            Background = Brushes.White, BorderBrush = Brush("#DCE4F0"), AlternatingRowBackground = Brush("#F7F9FC"),
            FontSize = 11, MinColumnWidth = 52
        };
        _grid.Columns.Add(Column("ID", "Id", 65));
        _grid.Columns.Add(Column("판정", "Status", 105));
        _grid.Columns.Add(Column("분류", "Category", 100));
        _grid.Columns.Add(Column("테스트 케이스", "Title", new DataGridLength(1, DataGridLengthUnitType.Star)));
        _grid.Columns.Add(Column("시도", "Attempts", 50));
        stack.Children.Add(_grid);
        return card;
    }

    private UIElement BuildDetails()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(12, 0, 0, 0) };
        var card = Card();
        _detail = new StackPanel();
        card.Child = _detail;
        scroll.Content = card;
        return scroll;
    }

    private void RefreshRows(bool selectFirst)
    {
        if (_dataset is null || _grid is null) return;
        var search = _search?.Text?.Trim() ?? string.Empty;
        var category = _category?.SelectedItem?.ToString() ?? "전체 분류";
        var matches = _dataset.Cases.Where(item =>
            (category == "전체 분류" || item.Category == category) &&
            (search.Length == 0 || string.Join(' ', item.Id, item.Title, item.Category, item.ExpectedActions, item.ExpectedObservation, item.Status)
                .Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            .OrderBy(item => item.Number).ToArray();
        _visibleCases = new ObservableCollection<CaseRecord>(matches);
        _grid.ItemsSource = _visibleCases;
        if (selectFirst) _grid.SelectedItem = _visibleCases.FirstOrDefault();
        ShowDetails(_grid.SelectedItem as CaseRecord);
        if (_status is not null) _status.Text = $"{_dataset.LastReconciled} 기준 · {matches.Length}/{_dataset.TotalCases}건 · 시도 { _dataset.DispatchedAtLeastOnce}/{_dataset.TotalCases}";
    }

    private void ShowDetails(CaseRecord? item)
    {
        if (_detail is null) return;
        _detail.Children.Clear();
        if (item is null)
        {
            _detail.Children.Add(Text("조건에 맞는 케이스가 없습니다.", 14, Muted));
            return;
        }
        _detail.Children.Add(Text($"#{item.Number:D3} · {item.Title}", 19, Ink, true));
        _detail.Children.Add(new Border { Background = Brush("#EEF0FF"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Margin = new Thickness(0, 9, 0, 12), Child = Text($"{item.Status} · {item.Attempts}회 시도 · {item.Category}", 11, Accent, true) });
        AddDetail("사전 조건", item.Preconditions);
        AddDetail("기대 행동", item.ExpectedActions);
        AddDetail("기대 관측", item.ExpectedObservation);
        AddDetail("통과 기준", item.PassCriteria);
        AddDetail("실제 기록 요약", item.ActualSummary);
        AddDetail("실제 확인 범위", item.Coverage);
        AddDetail("관측 한계", item.Limitation);
        AddDetail("증거", item.VisualEvidence ? "이미지 또는 영상 근거 있음" : "공개 데이터에 이미지·영상 증거 없음");
    }

    private void AddDetail(string label, string? value)
    {
        if (_detail is null) return;
        var block = new Border { Background = Brush("#F7F9FC"), BorderBrush = Brush("#E0E7F1"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 7) };
        var stack = new StackPanel();
        stack.Children.Add(Text(label, 11, Accent, true));
        stack.Children.Add(Text(string.IsNullOrWhiteSpace(value) ? "기록 없음" : value, 11, Ink));
        block.Child = stack;
        _detail.Children.Add(block);
    }

    private void AttachHandlers()
    {
        if (_search is not null)
        {
            _searchHandler = (_, _) => { if (!_suppress) RefreshRows(selectFirst: true); };
            _search.TextChanged += _searchHandler;
        }
        if (_category is not null)
        {
            _categoryHandler = (_, _) => { if (!_suppress) RefreshRows(selectFirst: true); };
            _category.SelectionChanged += _categoryHandler;
        }
        if (_grid is not null)
        {
            _gridHandler = (_, _) => ShowDetails(_grid.SelectedItem as CaseRecord);
            _grid.SelectionChanged += _gridHandler;
        }
        if (_reload is not null)
        {
            _reloadHandler = (_, _) => ReloadRequested?.Invoke(this, EventArgs.Empty);
            _reload.Click += _reloadHandler;
        }
    }

    public event EventHandler? ReloadRequested;

    private string InitialStatus() => _dataset is null ? "데이터를 읽지 못했습니다." :
        $"기준일 {_dataset.LastReconciled} · 계획 {_dataset.TotalCases}건 · 시도 {_dataset.DispatchedAtLeastOnce}/{_dataset.TotalCases} · QA 진행 중 · 모듈 v{Version} · SHA-256 {_sourceSha256[..12]}";

    private static void ValidateDataset(PortfolioDataset data)
    {
        if (data.TotalCases != 100 || data.Cases.Count != 100 || data.Cases.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != 100)
            throw new InvalidDataException("공개 데이터는 100개의 고유 케이스여야 합니다.");
        string[] labels = ["기록 범위 Pass", "부분 범위 Pass", "미확정", "미실행"];
        if (data.Counts.Values.Any(count => count < 0) || data.Counts.Values.Sum() != data.TotalCases ||
            data.DispatchedAtLeastOnce < 0 || data.DispatchedAtLeastOnce > data.TotalCases)
            throw new InvalidDataException("공개 데이터의 합계 또는 시도 수가 올바르지 않습니다.");
        var rowsByStatus = data.Cases.GroupBy(item => item.Status).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        foreach (var label in labels)
            if (!data.Counts.TryGetValue(label, out var count) || !rowsByStatus.TryGetValue(label, out var rowCount) || count != rowCount)
                throw new InvalidDataException($"요약 집계와 케이스별 판정이 일치하지 않습니다: {label}");
        if (data.Cases.Count(item => item.Dispatched) != data.DispatchedAtLeastOnce)
            throw new InvalidDataException("요약 시도 수와 케이스별 시도 기록이 일치하지 않습니다.");
    }

    private static DataGridTextColumn Column(string header, string path, double width) => new()
    {
        Header = header, Binding = new Binding(path), Width = new DataGridLength(width), MinWidth = 48
    };

    private static DataGridTextColumn Column(string header, string path, DataGridLength width) => new()
    {
        Header = header, Binding = new Binding(path), Width = width, MinWidth = 90
    };

    private static Border Card() => new()
    {
        Background = Brushes.White, BorderBrush = Brush("#DCE4F0"), BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12), Padding = new Thickness(13)
    };

    private static TextBlock Text(string value, double size, string color, bool bold = false) => new()
    {
        Text = value, FontSize = size, Foreground = Brush(color), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2)
    };

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));

    public void Dispose()
    {
        if (_search is not null && _searchHandler is not null) _search.TextChanged -= _searchHandler;
        if (_category is not null && _categoryHandler is not null) _category.SelectionChanged -= _categoryHandler;
        if (_grid is not null && _gridHandler is not null) _grid.SelectionChanged -= _gridHandler;
        if (_reload is not null && _reloadHandler is not null) _reload.Click -= _reloadHandler;
        ReloadRequested = null;
        _search = null; _category = null; _grid = null; _detail = null; _status = null; _reload = null; _mount = null;
    }

    private sealed record BrowserState(string? Search, string? Category, string? SelectedId);
}

internal sealed class PortfolioDataset
{
    public string LastReconciled { get; set; } = string.Empty;
    public int TotalCases { get; set; }
    public int DispatchedAtLeastOnce { get; set; }
    public Dictionary<string, int> Counts { get; set; } = [];
    public List<CaseRecord> Cases { get; set; } = [];
}

internal sealed class CaseRecord
{
    public int Number { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Verdict { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public bool Dispatched { get; set; }
    public string Preconditions { get; set; } = string.Empty;
    public string ExpectedActions { get; set; } = string.Empty;
    public string ExpectedObservation { get; set; } = string.Empty;
    public string PassCriteria { get; set; } = string.Empty;
    public string ActualSummary { get; set; } = string.Empty;
    public string Coverage { get; set; } = string.Empty;
    public string Limitation { get; set; } = string.Empty;
    public bool VisualEvidence { get; set; }
}
