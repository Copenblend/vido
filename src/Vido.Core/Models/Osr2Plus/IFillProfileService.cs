namespace Vido.Core.Models.Osr2Plus;

/// <summary>
/// Service for managing fill profiles (built-in and user-created).
/// </summary>
public interface IFillProfileService
{
    /// <summary>All available profiles (built-in first, then user profiles alphabetically).</summary>
    IReadOnlyList<FillProfile> Profiles { get; }

    /// <summary>Raised when the profile list changes (add, remove, rename).</summary>
    event Action? ProfilesChanged;

    /// <summary>Returns all built-in profiles.</summary>
    IReadOnlyList<FillProfile> GetBuiltInProfiles();

    /// <summary>Returns all user-created profiles.</summary>
    IReadOnlyList<FillProfile> GetUserProfiles();

    /// <summary>
    /// Creates a new user profile with the given name and axis settings.
    /// </summary>
    /// <param name="name">Profile name (non-empty, max 50 chars, trimmed).</param>
    /// <param name="axes">Axis settings dictionary keyed by axis ID.</param>
    /// <returns>The created profile.</returns>
    /// <exception cref="ArgumentException">If name is empty, too long, or already exists.</exception>
    FillProfile CreateProfile(string name, Dictionary<string, FillAxisSettings> axes);

    /// <summary>
    /// Updates an existing user profile's axis settings.
    /// </summary>
    /// <param name="name">Name of the profile to update (case-insensitive match).</param>
    /// <param name="axes">New axis settings.</param>
    /// <exception cref="InvalidOperationException">If profile is built-in or not found.</exception>
    void UpdateProfile(string name, Dictionary<string, FillAxisSettings> axes);

    /// <summary>
    /// Renames an existing user profile.
    /// </summary>
    /// <param name="currentName">Current name (case-insensitive match).</param>
    /// <param name="newName">New name (non-empty, max 50 chars, trimmed, unique).</param>
    /// <exception cref="InvalidOperationException">If profile is built-in or not found.</exception>
    /// <exception cref="ArgumentException">If newName is invalid or already exists.</exception>
    void RenameProfile(string currentName, string newName);

    /// <summary>
    /// Deletes a user profile.
    /// </summary>
    /// <param name="name">Name of the profile to delete (case-insensitive match).</param>
    /// <exception cref="InvalidOperationException">If profile is built-in or not found.</exception>
    void DeleteProfile(string name);

    /// <summary>
    /// Finds a profile by name (case-insensitive).
    /// Returns null if not found.
    /// </summary>
    /// <param name="name">The profile name to search for.</param>
    /// <returns>The matching profile, or <c>null</c> if not found.</returns>
    FillProfile? FindByName(string name);

    /// <summary>
    /// Loads profiles from disk. Called on startup.
    /// </summary>
    void Load();

    /// <summary>
    /// Persists all user profiles to disk asynchronously.
    /// </summary>
    Task SaveAsync();
}
