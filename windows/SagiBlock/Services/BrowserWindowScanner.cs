using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using SagiBlock.Models;

namespace SagiBlock.Services;

public sealed class BrowserWindowScanner
{
    private const int MaxTitleLength = 512;
    private const int MaxChildTextLength = 8192;
    private const int WM_CLOSE = 0x0010;
    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_CLOSE = 0xF060;
    private const int SW_RESTORE = 9;
    private const uint SMTO_ABORTIFHUNG = 0x0002;
    private const int GWL_STYLE = -16;
    private const uint WS_CAPTION = 0x00C00000;
    private const uint WS_THICKFRAME = 0x00040000;
    private const uint WS_BORDER = 0x00800000;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "vivaldi", "opera", "opera_gx", "iexplore"
    };

    // 強い証拠：詐欺警告ページに特徴的な文言
    private static readonly string[] StrongPhrases =
    [
        "windows defender - security warning",
        "windows firewall protection",
        "trojan spyware",
        "toll free",
        "quick scan",
        "偽セキュリティ警告",
        "偽の警告画面",
        "偽のウイルス検知",
        "偽のウイルススキャン",
        "サポート詐欺",
        "電話をかけさせ",
        "電話をかけさせよう",
        "根拠のないメッセージ",
        "この画面は演出です",
        "support.microsoft.com を装う",
        "このpcへのアクセスはセキュリティ上の理由でブロック",
        "このpcへのアクセスはブロック",
        "トロイの木馬スパイウェア",
        "マイクロソフトサポートに連絡",
        "windowsサポートに連絡",
        "pornographic spyware",
        "harmful pornographic",
        "access to this pc has been blocked",
        "this pc is blocked"
    ];

    // 弱い証拠：単体では高スコアにならない
    private static readonly string[] WeakPhrases =
    [
        "microsoft support",
        "defender",
        "firewall",
        "virus",
        "malware",
        "blocked",
        "scan",
        "alert",
        "security center",
        "warning",
        "support",
        "security"
    ];

    // 電話番号パターン（日本国内中心）
    private static readonly Regex PhoneNumberRegex = new(
        @"(?<!\d)(?:0[5789]0[-.\s]?\d{4}[-.\s]?\d{4}|0[123456]0[-.\s]?\d{3}[-.\s]?\d{4}|0\d{1,3}[-.\s]?\d{2,4}[-.\s]?\d{4}|\+81[-.\s]?\d{1,4}[-.\s]?\d{2,4}[-.\s]?\d{4})(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public IReadOnlyList<GuardEvent> CloseScarewareWindows()
    {
        var events = new List<GuardEvent>();
        foreach (var window in EnumerateTopLevelWindows())
        {
            var process = GetProcessInfo(window.Handle);
            var processName = process?.Name ?? "";
            var isBrowser = process is not null && BrowserProcesses.Contains(process.Name);

            var title = ReadWindowTitle(window.Handle);
            var childText = ReadChildText(window.Handle);
            var evidence = BuildEvidence(title, childText);
            var state = AnalyzeWindowState(window.Handle, isBrowser);

            var score = Score(evidence, state);
            var threshold = isBrowser ? 10 : 14;

            if (score < threshold)
                continue;

            if (CloseSuspiciousWindow(window.Handle, process, isBrowser, score))
            {
                events.Add(new GuardEvent(
                    GuardEventKind.ScarewareWindowClosed,
                    L.Get("ScarewareClosedTitle"),
                    L.Format("ScarewareClosedDetail", processNameOrWindow(processName), window.Title, score),
                    $"window:{processNameOrWindow(processName)}:{window.Title}"));
            }
            else
            {
                events.Add(new GuardEvent(
                    GuardEventKind.ScarewareWindowCloseFailed,
                    L.Get("ScarewareCloseFailedTitle"),
                    L.Format("ScarewareCloseFailedDetail", processNameOrWindow(processName), window.Title, score),
                    $"window-close-failed:{processNameOrWindow(processName)}:{window.Title}"));
            }
        }

        return events;
    }

    private static string processNameOrWindow(string processName) =>
        string.IsNullOrWhiteSpace(processName) ? "window" : processName;

    private static string BuildEvidence(string title, string childText) =>
        string.IsNullOrWhiteSpace(childText)
            ? title
            : $"{title}\n{childText}";

    private static WindowState AnalyzeWindowState(IntPtr hwnd, bool isBrowser)
    {
        var state = new WindowState { IsBrowser = isBrowser };
        if (!GetWindowRect(hwnd, out var windowRect))
            return state;

        state.IsMaximized = IsZoomed(hwnd);
        state.IsFullscreen = IsFullscreen(hwnd, windowRect);
        state.IsFrameless = IsFrameless(hwnd);
        return state;
    }

    private static bool IsFullscreen(IntPtr hwnd, RECT windowRect)
    {
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
            return false;

        var monitorRect = monitorInfo.rcMonitor;
        return windowRect.Left <= monitorRect.Left &&
               windowRect.Top <= monitorRect.Top &&
               windowRect.Right >= monitorRect.Right &&
               windowRect.Bottom >= monitorRect.Bottom;
    }

    private static bool IsFrameless(IntPtr hwnd)
    {
        var style = (uint)GetWindowLong(hwnd, GWL_STYLE);
        return (style & WS_CAPTION) == 0 && (style & WS_THICKFRAME) == 0 && (style & WS_BORDER) == 0;
    }

    private static int Score(string evidence, WindowState state)
    {
        if (string.IsNullOrWhiteSpace(evidence))
            return 0;

        var text = Normalize(evidence);
        var score = 0;

        foreach (var phrase in StrongPhrases)
        {
            if (text.Contains(Normalize(phrase), StringComparison.OrdinalIgnoreCase))
                score += 5;
        }

        foreach (var phrase in WeakPhrases)
        {
            if (text.Contains(Normalize(phrase), StringComparison.OrdinalIgnoreCase))
                score += 1;
        }

        var phoneMatches = PhoneNumberRegex.Matches(evidence);
        if (phoneMatches.Count > 0)
        {
            score += 4 + (phoneMatches.Count - 1) * 2;
        }

        if (state.IsFullscreen)
            score += 4;
        if (state.IsMaximized)
            score += 2;
        if (state.IsFrameless)
            score += 2;

        if (text.Contains("microsoft") && text.Contains("support"))
            score += 2;
        if (text.Contains("windows") && text.Contains("defender"))
            score += 2;
        if (text.Contains("偽") && text.Contains("警告"))
            score += 4;
        if (text.Contains("電話") && (text.Contains("サポート") || text.Contains("support")))
            score += 3;
        if (text.Contains("リモート") && text.Contains("サポート"))
            score += 3;
        if (phoneMatches.Count > 0 && text.Contains("support"))
            score += 3;

        return score;
    }

    private static string Normalize(string value) =>
        value.ToLowerInvariant()
            .Replace(" ", "")
            .Replace("　", "")
            .Replace("-", "")
            .Replace("ー", "")
            .Replace(".", "")
            .Replace(",", "");

    private static bool CloseSuspiciousWindow(
        IntPtr hwnd,
        ProcessInfo? process,
        bool isBrowser,
        int score)
    {
        var threshold = isBrowser ? 10 : 14;
        if (isBrowser)
        {
            // タブを閉じたかどうかはウィンドウが消えたかではなく、詐欺スコアが下がったかで判断する
            var title = ReadWindowTitle(hwnd);
            var childText = ReadChildText(hwnd);
            var evidence = BuildEvidence(title, childText);
            var state = AnalyzeWindowState(hwnd, true);

            if (TryCloseBrowserTab(hwnd, evidence, state, threshold))
                return true;

            // タブ閉じが失敗した場合はウィンドウを閉じる
            TryWindowCloseCommands(hwnd);
            if (WaitUntilClosed(hwnd))
                return true;

            // 最終手段：プロセスを終了（ブラウザ全体が終了する）
            if (process is not null && score >= 14)
            {
                TryKillProcess(process.Id);
                if (WaitUntilClosed(hwnd))
                    return true;
            }

            return false;
        }

        TryWindowCloseCommands(hwnd);
        if (WaitUntilClosed(hwnd))
            return true;

        if (process is not null)
        {
            TryCloseProcessMainWindow(process.Id);
            if (WaitUntilClosed(hwnd))
                return true;
        }

        if (process is not null && score >= 16)
        {
            TryKillProcess(process.Id);
            if (WaitUntilClosed(hwnd))
                return true;
        }

        return false;
    }

    private static bool TryCloseBrowserTab(IntPtr hwnd, string originalEvidence, WindowState state, int threshold)
    {
        try
        {
            if (!IsWindow(hwnd) || !IsWindowVisible(hwnd))
                return true;

            ActivateWindow(hwnd);

            // 1. タブを閉じる
            Thread.Sleep(80);
            System.Windows.Forms.SendKeys.SendWait("^w");

            // beforeunload ダイアログが出た場合、「このページを離れる」は通常デフォルトボタン
            Thread.Sleep(120);
            if (IsWindow(hwnd) && IsWindowVisible(hwnd))
            {
                System.Windows.Forms.SendKeys.SendWait("{ENTER}");
            }

            if (WaitUntilTabClosed(hwnd, originalEvidence, state, threshold, maxAttempts: 8, intervalMs: 120))
                return true;

            // 2. それでも閉じない場合はウィンドウを閉じる（Alt+F4）
            ActivateWindow(hwnd);
            Thread.Sleep(80);
            System.Windows.Forms.SendKeys.SendWait("%{F4}");

            return WaitUntilClosed(hwnd, maxAttempts: 6, intervalMs: 120);
        }
        catch
        {
            return false;
        }
    }

    private static bool WaitUntilTabClosed(
        IntPtr hwnd,
        string originalEvidence,
        WindowState originalState,
        int threshold,
        int maxAttempts,
        int intervalMs)
    {
        var originalTitle = originalEvidence.Split('\n')[0];
        var originalScore = Score(originalEvidence, originalState);

        for (var i = 0; i < maxAttempts; i++)
        {
            Thread.Sleep(intervalMs);
            if (!IsWindow(hwnd) || !IsWindowVisible(hwnd))
                return true;

            var title = ReadWindowTitle(hwnd);
            var childText = ReadChildText(hwnd);
            var evidence = BuildEvidence(title, childText);
            var currentState = AnalyzeWindowState(hwnd, true);
            var currentScore = Score(evidence, currentState);

            // 詐欺スコアが閾値を下回ったら、タブは閉じられたと見なす
            if (currentScore < threshold)
                return true;

            // タイトルが変わっていて、スコアが下がっていれば閉じたと見なす
            if (!string.IsNullOrEmpty(title) &&
                !Normalize(title).Contains(Normalize(originalTitle)) &&
                currentScore < originalScore)
            {
                return true;
            }
        }

        return false;
    }

    private static void ActivateWindow(IntPtr hwnd)
    {
        ShowWindow(hwnd, SW_RESTORE);
        BringWindowToTop(hwnd);
        SetForegroundWindow(hwnd);
    }

    private static void TryWindowCloseCommands(IntPtr hwnd)
    {
        if (!IsWindow(hwnd))
            return;

        ActivateWindow(hwnd);
        _ = PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        _ = SendMessageTimeout(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 800, out _);
        _ = PostMessage(hwnd, WM_SYSCOMMAND, new IntPtr(SC_CLOSE), IntPtr.Zero);
    }

    private static void TryCloseProcessMainWindow(int processId)
    {
        try
        {
            using var proc = Process.GetProcessById(processId);
            proc.CloseMainWindow();
            proc.WaitForExit(1200);
        }
        catch
        {
            // Ignore and let the next fallback handle it.
        }
    }

    private static void TryKillProcess(int processId)
    {
        try
        {
            using var proc = Process.GetProcessById(processId);
            proc.Kill(entireProcessTree: true);
            proc.WaitForExit(1500);
        }
        catch
        {
            // Final fallback failed; report close failure.
        }
    }

    private static bool WaitUntilClosed(IntPtr hwnd, int maxAttempts = 8, int intervalMs = 160)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            Thread.Sleep(intervalMs);
            if (!IsWindow(hwnd) || !IsWindowVisible(hwnd))
                return true;
        }
        return false;
    }

    private static ProcessInfo? GetProcessInfo(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
            return null;

        try
        {
            using var proc = Process.GetProcessById((int)pid);
            return new ProcessInfo(proc.Id, proc.ProcessName);
        }
        catch
        {
            return null;
        }
    }

    private static string ReadChildText(IntPtr hwnd)
    {
        var sb = new StringBuilder();
        EnumChildWindows(hwnd, (child, _) =>
        {
            if (sb.Length >= MaxChildTextLength)
                return false;

            var length = GetWindowTextLength(child);
            if (length <= 0)
                return true;

            var text = new StringBuilder(Math.Min(length + 1, MaxTitleLength));
            GetWindowText(child, text, text.Capacity);
            if (text.Length > 0)
                sb.AppendLine(text.ToString());
            return true;
        }, IntPtr.Zero);
        return sb.ToString();
    }

    private static string ReadWindowTitle(IntPtr hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length <= 0)
            return "";
        var sb = new StringBuilder(Math.Min(length + 1, MaxTitleLength));
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static IEnumerable<WindowInfo> EnumerateTopLevelWindows()
    {
        var windows = new List<WindowInfo>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
                return true;
            var length = GetWindowTextLength(hwnd);
            if (length <= 0)
                return true;

            var sb = new StringBuilder(Math.Min(length + 1, MaxTitleLength));
            GetWindowText(hwnd, sb, sb.Capacity);
            var title = sb.ToString();
            if (!string.IsNullOrWhiteSpace(title))
                windows.Add(new WindowInfo(hwnd, title));
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private sealed record WindowInfo(IntPtr Handle, string Title);
    private sealed record ProcessInfo(int Id, string Name);
    private sealed class WindowState
    {
        public bool IsBrowser { get; set; }
        public bool IsFullscreen { get; set; }
        public bool IsMaximized { get; set; }
        public bool IsFrameless { get; set; }
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc enumProc, IntPtr lParam);
}
