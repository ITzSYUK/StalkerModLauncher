using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class ProfileNotesStore
{
    private readonly AppPaths _paths;

    public ProfileNotesStore(AppPaths paths)
    {
        _paths = paths;
    }

    public string NotesDirectory => _paths.ConfigDirectory;

    public string GetNotesFile(ModProfile profile)
    {
        return Path.Combine(_paths.ConfigDirectory, $"notes-{profile.Id}.txt");
    }

    public string Load(ModProfile profile)
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);
        var notesFile = GetNotesFile(profile);
        MigrateLegacyNotes(profile, notesFile);
        return File.Exists(notesFile) ? File.ReadAllText(notesFile) : string.Empty;
    }

    public void Save(ModProfile profile, string notes)
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);
        File.WriteAllText(GetNotesFile(profile), notes);
    }

    private void MigrateLegacyNotes(ModProfile profile, string notesFile)
    {
        if (File.Exists(notesFile))
        {
            return;
        }

        var legacyName = FileSystemSafety.SanitizeName(profile.Name);
        var legacyFile = Path.Combine(_paths.ConfigDirectory, $"notes-{legacyName}.txt");
        if (!File.Exists(legacyFile) || FileSystemSafety.IsSameDirectory(legacyFile, notesFile))
        {
            return;
        }

        File.Move(legacyFile, notesFile);
    }
}
