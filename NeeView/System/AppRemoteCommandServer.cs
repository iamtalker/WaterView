using NeeLaboratory.IO;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace NeeView
{
    public class AppRemoteCommandServer : RemoteCommandServer, IDisposable
    {
        private readonly static Lazy<AppRemoteCommandServer> _current = new();
        public static AppRemoteCommandServer Current => _current.Value;

        public static string PipeName { get; } = AppRemoteCommandTools.CreatePipeName(Process.GetCurrentProcess());
        private Task? _task;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _disposedValue;

        public AppRemoteCommandServer() : base(PipeName, AppRemoteCommandJsonContext.Default)
        {
            RegisterMethod<string[]>(nameof(Restart), Restart);
            RegisterMethod<int, bool>(nameof(IsHideWindow), IsHideWindow);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Stop();
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public void Start()
        {
            if (_task is not null) return;

            _cancellationTokenSource = new();
            _task = StartAsync(_cancellationTokenSource.Token);
        }

        public void Stop()
        {
            if (_task is null) return;

            _cancellationTokenSource?.Cancel();
            _task = null;
        }


        public void Restart(string[] args)
        {
            var done = new ManualResetEventSlim();
            App.Current.Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    await AppState.Current.ResumeAsync(args, CancellationToken.None);
                }
                catch
                {
                }
                finally
                {
                    done.Set();
                }
            });

            // 책이 뜰 때까지 응답을 미룬다. 호출한 쪽이 그 뒤에 창을 앞으로 가져온다 (이전 책이 잠깐 보이는 것을 막기 위해)
            done.Wait(TimeSpan.FromSeconds(6));
        }

        public bool IsHideWindow(int _)
        {
            try
            {
                return AppState.Current.IsTaskTrayEnabled && AppState.Current.IsHideWindow;
            }
            catch
            {
                return false;
            }
        }
    }

}
