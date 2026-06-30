namespace ScreenRecorder.App.Services;

/// <summary>Abstracts user-facing dialogs so view models remain unit-testable.</summary>
public interface IDialogService
{
    void ShowInfo(string title, string message);

    void ShowError(string title, string message);

    bool Confirm(string title, string message);

    /// <summary>Shows a folder picker; returns the chosen path or <c>null</c> if cancelled.</summary>
    string? BrowseForFolder(string title, string? initialDirectory = null);
}
