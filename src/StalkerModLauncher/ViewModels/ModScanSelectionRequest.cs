namespace StalkerModLauncher.ViewModels;

public sealed class ModScanSelectionRequest : EventArgs
{
    private readonly TaskCompletionSource<IReadOnlyList<SelectableMod>?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ModScanSelectionRequest(IReadOnlyList<SelectableMod> mods)
    {
        Mods = mods;
    }

    public IReadOnlyList<SelectableMod> Mods { get; }

    public Task<IReadOnlyList<SelectableMod>?> Completion => _completion.Task;

    public void Accept(IReadOnlyList<SelectableMod> selectedMods) =>
        _completion.TrySetResult(selectedMods);

    public void Cancel() => _completion.TrySetResult(null);
}
