using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Wagenheimer.MonoGameHelper.Storage;

/// <summary>
/// Provides atomic and crash-resilient JSON storage with automatic temporary staging and backup recovery.
/// </summary>
public static class SafeJsonStorage
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Saves data to a JSON file atomically using a temporary file and creating a backup copy.
    /// </summary>
    /// <typeparam name="T">The type of data to serialize.</typeparam>
    /// <param name="filePath">Target destination file path.</param>
    /// <param name="data">The object to serialize.</param>
    /// <param name="options">Optional JSON serialization options.</param>
    public static void Save<T>(string filePath, T data, JsonSerializerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(data);

        string dir = Path.GetDirectoryName(filePath) ?? string.Empty;
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tempPath = filePath + ".tmp";
        string backupPath = filePath + ".bak";

        options ??= DefaultOptions;
        string json = JsonSerializer.Serialize(data, options);

        // 1. Write to temporary file
        File.WriteAllText(tempPath, json);

        // 2. If target file exists, ensure backup exists
        if (File.Exists(filePath))
        {
            File.Copy(filePath, backupPath, overwrite: true);
        }

        // 3. Atomically replace/move temp file to destination
        File.Move(tempPath, filePath, overwrite: true);
    }

    /// <summary>
    /// Asynchronously saves data to a JSON file atomically using a temporary file and creating a backup copy.
    /// </summary>
    /// <typeparam name="T">The type of data to serialize.</typeparam>
    /// <param name="filePath">Target destination file path.</param>
    /// <param name="data">The object to serialize.</param>
    /// <param name="options">Optional JSON serialization options.</param>
    public static async Task SaveAsync<T>(string filePath, T data, JsonSerializerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(data);

        string dir = Path.GetDirectoryName(filePath) ?? string.Empty;
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tempPath = filePath + ".tmp";
        string backupPath = filePath + ".bak";

        options ??= DefaultOptions;
        string json = JsonSerializer.Serialize(data, options);

        await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);

        if (File.Exists(filePath))
        {
            File.Copy(filePath, backupPath, overwrite: true);
        }

        File.Move(tempPath, filePath, overwrite: true);
    }

    /// <summary>
    /// Loads and deserializes data from a JSON file. If the primary file is corrupted, attempts to recover from the .bak backup.
    /// </summary>
    /// <typeparam name="T">The type of data to deserialize.</typeparam>
    /// <param name="filePath">File path to read from.</param>
    /// <param name="defaultValue">Fallback value if neither primary nor backup exists or both are unreadable.</param>
    /// <param name="options">Optional JSON serialization options.</param>
    /// <returns>Deserialized object, or defaultValue if unavailable.</returns>
    public static T? Load<T>(string filePath, T? defaultValue = default, JsonSerializerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        options ??= DefaultOptions;

        // Try primary file
        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<T>(json, options);
            }
            catch (Exception)
            {
                // Primary file might be corrupted, fall through to backup
            }
        }

        // Try backup file
        string backupPath = filePath + ".bak";
        if (File.Exists(backupPath))
        {
            try
            {
                string backupJson = File.ReadAllText(backupPath);
                return JsonSerializer.Deserialize<T>(backupJson, options);
            }
            catch (Exception)
            {
                // Backup is also unreadable
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// Asynchronously loads and deserializes data from a JSON file. If the primary file is corrupted, attempts to recover from the .bak backup.
    /// </summary>
    /// <typeparam name="T">The type of data to deserialize.</typeparam>
    /// <param name="filePath">File path to read from.</param>
    /// <param name="defaultValue">Fallback value if neither primary nor backup exists.</param>
    /// <param name="options">Optional JSON serialization options.</param>
    /// <returns>Deserialized object, or defaultValue if unavailable.</returns>
    public static async Task<T?> LoadAsync<T>(string filePath, T? defaultValue = default, JsonSerializerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        options ??= DefaultOptions;

        if (File.Exists(filePath))
        {
            try
            {
                string json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                return JsonSerializer.Deserialize<T>(json, options);
            }
            catch (Exception)
            {
                // Primary file failed, attempt backup
            }
        }

        string backupPath = filePath + ".bak";
        if (File.Exists(backupPath))
        {
            try
            {
                string backupJson = await File.ReadAllTextAsync(backupPath).ConfigureAwait(false);
                return JsonSerializer.Deserialize<T>(backupJson, options);
            }
            catch (Exception)
            {
                // Backup failed
            }
        }

        return defaultValue;
    }
}
