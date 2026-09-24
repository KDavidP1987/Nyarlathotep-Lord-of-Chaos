#nullable enable

namespace Nyarlathotep.Logic;

/// <summary>The disk primitives of Services/Persistence.cs. The write protocol (<see cref="DataStore"/>) runs over
/// this interface, so the tests drive it with an in-memory store that can fail or stall (D8, D9). Every method
/// may throw an IOException-like failure; the protocol decides what that affects.</summary>
public interface IFileStore
{
    bool Exists(DataFile file, FileVariant variant);

    /// <summary>The whole content, or null when the file does not exist.</summary>
    byte[]? Read(DataFile file, FileVariant variant);

    DateTime WriteUtc(DataFile file, FileVariant variant);

    /// <summary>Creates or overwrites the file with exactly <paramref name="content"/>.</summary>
    void Write(DataFile file, FileVariant variant, byte[] content);

    /// <summary>Makes the .tmp the main file in one step. With <paramref name="keepBackup"/> the old main file
    /// becomes the one .bak (replacing an earlier one); without, it is discarded. A missing main file is fine.</summary>
    void Promote(DataFile file, bool keepBackup);

    /// <summary>Renames one variant to another, replacing the destination.</summary>
    void Rename(DataFile file, FileVariant from, FileVariant to);

    void Delete(DataFile file, FileVariant variant);
}
