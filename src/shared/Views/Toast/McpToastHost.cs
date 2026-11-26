#nullable disable
using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Bimwright.Nwd.Shared.Config;

namespace Bimwright.Nwd.Shared.Views.Toast
{
    public sealed class McpToastHost
    {
        private readonly object _lock = new object();
        private Thread _thread;
        private volatile Dispatcher _dispatcher;
        private volatile McpToastManager _manager;
        private volatile Dispatcher _hostDispatcher;
        private IntPtr _ownerHandle;
        private bool _shutdownRequested;
        private bool _usesDedicatedThread;
        private bool _showBranding;
        private int _idleSeconds;
        private volatile ToastPositionOptions _positionOptions;
        public event Action<bool> PositionSaved;
        public ToastPositionOptions PositionOptions => _positionOptions;
        private volatile string _instanceIdentity;
        private volatile string _agentName;
        private volatile Func<bool> _isPaused;
        private volatile Func<bool, bool> _setPaused;
        private Func<bool> _frameUsable = () => true;
        private Application _toastApplication;

        public ActivityAggregator Aggregator { get; }
        public ActivityHistory History { get; } = new ActivityHistory();
        private ToastHistoryWindow _history;
        public void ShowHistory()
        {
            Bimwright.Nwd.Shared.Update.UpdateNoticeCoordinator.TriggerFromUi();
            Post(manager =>
            {
                if (_history == null)
                {
                    _history = new ToastHistoryWindow(History, _instanceIdentity);
                    _history.Closed += (_, __) => _history = null;
                    _history.Show();
                }
                if (_history.WindowState == WindowState.Minimized) _history.WindowState = WindowState.Normal;
                _history.Activate();
            });
        }

        /// <summary>Hover wordmark. Default off; loaded from nwdmcp.config.json at startup.</summary>
        public bool ShowBranding => _showBranding;

        public McpToastHost()
        {
            _idleSeconds = PluginSettings.LoadToastIdleSeconds();
            _showBranding = PluginSettings.LoadShowBranding();
            _positionOptions = PluginSettings.LoadToastPosition();
            Aggregator = new ActivityAggregator(() => _idleSeconds);
        }

        public void SetShowBranding(bool show)
        {
            _showBranding = show;
            PostToManager(manager => manager.ApplyShowBranding());
        }

        public void SetPositionOptions(ToastPositionOptions options)
        {
            _positionOptions = options ?? new ToastPositionOptions();
            PostToManager(manager => manager.ApplyPosition());
        }

        private void SaveDraggedPosition(ToastPositionOptions options)
        {
            _positionOptions = options;
            var saved = PluginSettings.SaveToastPosition(options);
            PositionSaved?.Invoke(saved);
        }

        public void SetIdleSeconds(int seconds)
        {
            _idleSeconds = PluginSettings.NormalizeToastIdleSeconds(seconds);
        }

        /// <summary>
        /// Card title naming this instance, such as "nwd-mcp 2024". Read when each card is created.
        /// </summary>
        public void SetInstanceIdentity(string identity)
        {
            _instanceIdentity = string.IsNullOrWhiteSpace(identity) ? null : identity;
        }

        /// <summary>
        /// Agent of the latest MCP call ("Claude"), or null when the gateway did not name one. The open card's title
        /// follows it on the next render.
        /// </summary>
        public void SetAgentName(string agent)
        {
            _agentName = string.IsNullOrWhiteSpace(agent) ? null : agent;
        }

        /// <summary>"nwd-mcp 2025 · Claude"; null leaves the card's own product-name fallback.</summary>
        private string IdentityTitle()
        {
            var identity = _instanceIdentity;
            var agent = _agentName;
            if (agent == null) return identity;
            return (identity ?? BrandAssets.ProductName) + " \u00B7 " + agent;
        }

        /// <summary>The expanded card's Pause / Resume. Without handlers the card has no pause button.</summary>
        public void SetPauseHandlers(Func<bool> isPaused, Func<bool, bool> setPaused)
        {
            _isPaused = isPaused;
            _setPaused = setPaused;
        }

        public void SetFrameUsableProvider(Func<bool> provider)
        {
            if (provider != null)
                _frameUsable = provider;
        }

        public void SetHostDispatcher(Dispatcher dispatcher)
        {
            if (dispatcher == null)
                return;
            _hostDispatcher = dispatcher;
        }

        public void SetOwnerHandle(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return;
            _ownerHandle = hwnd;
            PostToManager(manager => manager.SetOwnerHandle(hwnd));
        }

        public void EnsureStarted()
        {
            lock (_lock)
            {
                if (_dispatcher != null || _shutdownRequested)
                    return;

                // Navisworks (and most WPF hosts) already own Application.Current in this
                // AppDomain. Creating Window on a second STA thread in that case is
                // unstable — use the host dispatcher.
                var hostDispatcher = _hostDispatcher ?? Application.Current?.Dispatcher;
                if (hostDispatcher != null)
                {
                    _dispatcher = hostDispatcher;
                    _manager = CreateManager(_dispatcher);
                    if (_ownerHandle != IntPtr.Zero)
                        _manager.SetOwnerHandle(_ownerHandle);
                    _usesDedicatedThread = false;
                    return;
                }

                var started = new ManualResetEventSlim(false);
                Exception startupError = null;

                _thread = new Thread(() =>
                {
                    try
                    {
                        if (Application.Current == null)
                        {
                            _toastApplication = new Application
                            {
                                ShutdownMode = ShutdownMode.OnExplicitShutdown
                            };
                        }

                        _dispatcher = Dispatcher.CurrentDispatcher;
                        _manager = CreateManager(_dispatcher);
                        if (_ownerHandle != IntPtr.Zero)
                            _manager.SetOwnerHandle(_ownerHandle);
                    }
                    catch (Exception ex)
                    {
                        startupError = ex;
                    }
                    finally
                    {
                        started.Set();
                    }

                    if (startupError != null)
                        return;

                    try
                    {
                        Dispatcher.Run();
                    }
                    finally
                    {
                        if (_toastApplication != null)
                        {
                            try { _toastApplication.Shutdown(); }
                            catch { }
                            _toastApplication = null;
                        }
                    }
                })
                {
                    IsBackground = true,
                    Name = "Bimwright.Nwd.Toast"
                };
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.Start();
                started.Wait(TimeSpan.FromSeconds(5));
                _usesDedicatedThread = startupError == null && _dispatcher != null;

                if (startupError != null)
                    _dispatcher = null;

            }
        }

        internal void Post(Action<McpToastManager> action)
        {
            if (_shutdownRequested || action == null)
                return;

            EnsureStarted();
            PostToManager(action);
        }

        public void Post(Action action)
        {
            if (_shutdownRequested || action == null)
                return;

            EnsureStarted();
            var dispatcher = _dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
                return;

            dispatcher.BeginInvoke(action);
        }

        /// <summary>
        /// Dismiss all toasts. Prefer synchronous invoke during shutdown so Topmost
        /// windows are closed before Navisworks tears down the dispatcher.
        /// </summary>
        public void DismissAll(bool synchronous = false)
        {
            if (_shutdownRequested && !synchronous)
                return;

            var dispatcher = _dispatcher;
            var manager = _manager;
            if (dispatcher == null || manager == null || dispatcher.HasShutdownStarted)
                return;

            void Run()
            {
                try { manager.DismissAllImmediate(); }
                catch (Exception) { }
            }

            if (synchronous)
            {
                try
                {
                    if (dispatcher.CheckAccess())
                        Run();
                    else
                        dispatcher.Invoke(Run, DispatcherPriority.Send, CancellationToken.None, TimeSpan.FromSeconds(2));
                }
                catch (Exception)
                {
                }
                return;
            }

            PostToManager(m => m.DismissAllImmediate());
        }

        public void Shutdown()
        {
            try
            {
                _dispatcher?.Invoke(() => _history?.Close(), DispatcherPriority.Send,
                    CancellationToken.None, TimeSpan.FromSeconds(2));
            }
            catch { }
            // Close windows before flipping the shutdown flag so DismissAll can still run.
            DismissAll(synchronous: true);

            lock (_lock)
            {
                _shutdownRequested = true;
            }

            if (!_usesDedicatedThread)
                return;

            var dispatcher = _dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
                return;

            try
            {
                dispatcher.InvokeShutdown();
            }
            catch
            {
                // Best-effort shutdown during Navisworks exit.
            }

            var thread = _thread;
            if (thread != null && thread.IsAlive)
                thread.Join(TimeSpan.FromSeconds(2));
        }

        private McpToastManager CreateManager(Dispatcher dispatcher)
        {
            return new McpToastManager(
                dispatcher,
                Aggregator,
                () => _frameUsable(),
                showBranding: () => _showBranding,
                instanceIdentity: IdentityTitle,
                onClick: _ => ShowHistory(),
                positionOptions: () => _positionOptions,
                positionChanged: SaveDraggedPosition,
                isPaused: () => _isPaused?.Invoke() ?? false,
                setPaused: paused => _setPaused?.Invoke(paused) ?? false);
        }

        private void PostToManager(Action<McpToastManager> action)
        {
            var dispatcher = _dispatcher;
            var manager = _manager;
            if (dispatcher == null || manager == null || dispatcher.HasShutdownStarted)
                return;

            dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    action(manager);
                }
                catch (Exception)
                {
                }
            }));
        }
    }
}
