namespace Server.Components.Pages;

using Microsoft.JSInterop;
using Syncfusion.Blazor.DropDowns;

public partial class Index : IAsyncDisposable
{
    [Inject] private WeatherDashboardService DashboardService { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private ILogger<Index> Logger { get; set; } = null!;
    [Inject] private TimeProvider TimeProvider { get; set; } = null!;

    private WeatherDashboardPageState State { get; } = new();
    private DotNetObjectReference<Index>? _dotNetRef;
    private CancellationTokenSource? _loadCancellationTokenSource;
    private long _activeLoadVersion;
    private string TodayLabel => WeatherDashboardPageState.FormatDayLabel(GetToday());

    protected override async Task OnInitializedAsync()
    {
        State.Initialize(GetToday());
        await LoadStationList();
        await RefreshData();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef ??= DotNetObjectReference.Create(this);
            await Js.InvokeVoidAsync("weatherDashboardViewport.register", _dotNetRef);
        }
    }

    [JSInvokable]
    public Task OnViewportChanged(int width)
    {
        if (!State.UpdateViewport(width))
        {
            return Task.CompletedTask;
        }

        return InvokeAsync(StateHasChanged);
    }

    private async Task LoadStationList()
    {
        State.IsStationsLoading = true;

        try
        {
            State.SetStationOptions(await DashboardService.GetStationOptionsAsync());

            if (State.StationData.Count == 0)
            {
                State.SetError(WeatherDashboardPageState.StationsUnavailableMessage);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Unable to load the station list.");
            State.SetError(WeatherDashboardPageState.StationsUnavailableMessage);
        }
        finally
        {
            State.IsStationsLoading = false;
        }
    }

    private async Task RefreshData()
    {
        State.SetSelectedMonth(GetToday());
        await LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        State.PrepareForDashboardLoad();

        if (!State.TryNormalizeCoordinates(out var latitude, out var longitude))
        {
            State.SetError(WeatherDashboardPageState.InvalidCoordinatesMessage);
            return;
        }

        State.ApplyNormalizedCoordinates(latitude, longitude);

        var (loadVersion, cancellationToken) = BeginLoad();
        State.IsRefreshing = true;

        await InvokeAsync(StateHasChanged);

        try
        {
            var snapshot = await DashboardService.GetDashboardAsync(latitude, longitude, State.SelectedMonth, cancellationToken);

            if (!IsCurrentLoad(loadVersion))
            {
                return;
            }

            if (snapshot is null)
            {
                State.SetError(WeatherDashboardPageState.DashboardUnavailableMessage);
                return;
            }

            State.ApplySnapshot(snapshot);
        }
        catch (OperationCanceledException) when (!IsCurrentLoad(loadVersion) || cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (IsCurrentLoad(loadVersion))
            {
                Logger.LogError(exception, "Unable to load dashboard data for {Latitude}/{Longitude}.", latitude, longitude);
                State.SetError(WeatherDashboardPageState.DashboardLoadFailedMessage);
            }
        }
        finally
        {
            if (IsCurrentLoad(loadVersion))
            {
                State.IsRefreshing = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    public void OnChange(ChangeEventArgs<string, WeatherStationOption> args)
    {
        if (args.ItemData != null && args.IsInteracted)
        {
            State.SetStationCoordinates(args.ItemData);
        }
    }

    private async Task ChangeMonth(int offset)
    {
        if (!State.TryNormalizeCoordinates(out var latitude, out var longitude))
        {
            State.SetError(WeatherDashboardPageState.InvalidCoordinatesMessage);
            return;
        }

        State.ShiftSelectedMonth(offset);
        State.SetError(null);

        var (loadVersion, cancellationToken) = BeginLoad();
        State.IsMonthNavigationLoading = true;

        await InvokeAsync(StateHasChanged);

        try
        {
            var chartData = await DashboardService.GetMonthlyChartAsync(latitude, longitude, State.SelectedMonth, cancellationToken);

            if (!IsCurrentLoad(loadVersion))
            {
                return;
            }

            State.SetMonthlyChart(chartData);
        }
        catch (OperationCanceledException) when (!IsCurrentLoad(loadVersion) || cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (IsCurrentLoad(loadVersion))
            {
                Logger.LogError(exception, "Unable to change the chart month for {Latitude}/{Longitude}.", latitude, longitude);
                State.SetError(WeatherDashboardPageState.ChartLoadFailedMessage);
            }
        }
        finally
        {
            if (IsCurrentLoad(loadVersion))
            {
                State.IsMonthNavigationLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private (long LoadVersion, CancellationToken CancellationToken) BeginLoad()
    {
        var loadVersion = Interlocked.Increment(ref _activeLoadVersion);

        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource?.Dispose();
        _loadCancellationTokenSource = new CancellationTokenSource();

        return (loadVersion, _loadCancellationTokenSource.Token);
    }

    private bool IsCurrentLoad(long loadVersion) =>
        loadVersion == Volatile.Read(ref _activeLoadVersion);

    private DateTime GetToday() =>
        TimeProvider.GetLocalNow().DateTime.Date;

    public async ValueTask DisposeAsync()
    {
        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource?.Dispose();

        if (_dotNetRef is not null)
        {
            try
            {
                await Js.InvokeVoidAsync("weatherDashboardViewport.dispose");
            }
            catch (JSDisconnectedException)
            {
            }
        }

        _dotNetRef?.Dispose();
    }
}
