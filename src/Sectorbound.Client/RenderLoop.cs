using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Sectorbound.Client;

internal sealed class RenderLoop : IDisposable
{
    private readonly Action<TimeSpan> _tick;
    private readonly long _targetFrameTicks;
    private readonly Stopwatch _stopwatch = new();
    private long _lastFrameTicks;
    private bool _running;

    public RenderLoop(Action<TimeSpan> tick, int targetFramesPerSecond = 60)
    {
        _tick = tick;
        _targetFrameTicks = Stopwatch.Frequency / targetFramesPerSecond;
    }

    public void Start()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        _stopwatch.Start();
        _lastFrameTicks = _stopwatch.ElapsedTicks;
        Application.Idle += OnApplicationIdle;
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        Application.Idle -= OnApplicationIdle;
        _stopwatch.Stop();
    }

    public void Dispose() => Stop();

    private void OnApplicationIdle(object? sender, EventArgs e)
    {
        while (_running && IsApplicationIdle())
        {
            var nowTicks = _stopwatch.ElapsedTicks;
            var elapsedTicks = nowTicks - _lastFrameTicks;
            if (elapsedTicks < _targetFrameTicks)
            {
                break;
            }

            _lastFrameTicks = nowTicks;
            var deltaSeconds = Math.Min(0.1, elapsedTicks / (double)Stopwatch.Frequency);
            _tick(TimeSpan.FromSeconds(deltaSeconds));
        }
    }

    private static bool IsApplicationIdle() => !PeekMessage(out _, IntPtr.Zero, 0, 0, 0);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(
        out NativeMessage lpMsg,
        IntPtr hWnd,
        uint wMsgFilterMin,
        uint wMsgFilterMax,
        uint wRemoveMsg);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Handle;
        public uint Message;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public Point Point;
    }
}
