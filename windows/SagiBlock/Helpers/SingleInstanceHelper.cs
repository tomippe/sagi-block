namespace SagiBlock.Helpers;

/// <summary>
/// トレイ常駐アプリの二重起動を防ぐ。トーストクリック等で再実行された場合は既存インスタンスへ通知する。
/// </summary>
internal static class SingleInstanceHelper
{
    private const string MutexName = "jp.tomippe.sagiBlock.SingleInstance";
    private const string ActivateEventName = "jp.tomippe.sagiBlock.Activate";

    private static Mutex? _mutex;
    private static EventWaitHandle? _activateEvent;
    private static volatile bool _listen;

    public static bool TryBecomePrimary()
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            SignalPrimary();
            return false;
        }

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        return true;
    }

    public static void SignalPrimary()
    {
        try
        {
            EventWaitHandle.OpenExisting(ActivateEventName).Set();
        }
        catch
        {
            // Primary instance not ready yet.
        }
    }

    public static void StartListening(Action onActivate)
    {
        if (_activateEvent is null)
            return;

        _listen = true;
        Task.Run(() =>
        {
            while (_listen)
            {
                if (!_activateEvent.WaitOne(200))
                    continue;

                try
                {
                    onActivate();
                }
                catch (Exception ex)
                {
                    StartupLog.Write(ex, "SingleInstance activate handler failed");
                }
            }
        });
    }

    public static void StopListening() => _listen = false;
}
