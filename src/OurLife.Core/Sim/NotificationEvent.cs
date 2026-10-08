using OurLife.Core.Text;

namespace OurLife.Core.Sim;

public enum NotificationSeverity
{
    Info,
    Warning,
    Danger,
}

/// <summary>A message for the player's event log, as a translation key plus arguments.</summary>
public sealed record NotificationEvent(long Tick, LocalizedText Text, NotificationSeverity Severity) : ISimEvent;
