namespace SagiBlock.Models;

public enum GuardEventKind
{
    ScarewareWindowClosed,
    ScarewareWindowCloseFailed,
    NotificationPermissionBlocked,
    CheckFailed
}

public sealed record GuardEvent(
    GuardEventKind Kind,
    string Title,
    string Detail,
    string Key);
