using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// Loads the default National Pokédex front sprites published by PokeAPI.
/// Images are cached outside the installation so app updates do not replace them.
/// </summary>
internal static class PokemonSpriteService
{
    private const int MaximumCachedSprites = 512;
    private const int MaximumSpriteBytes = 1024 * 1024;
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(6) };
    private static readonly SemaphoreSlim Downloads = new(4, 4);

    internal static string CacheDirectory
    {
        get
        {
            string overridePath = Environment.GetEnvironmentVariable("POKEMONPLAY_SPRITE_CACHE");
            return !string.IsNullOrWhiteSpace(overridePath)
                ? Path.GetFullPath(overridePath)
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pokemons Play", "SpriteCache");
        }
    }

    internal static IReadOnlyList<Uri> SpriteUris(int species, bool shiny, bool female)
    {
        if (species is < 1 or > 1025)
            throw new ArgumentOutOfRangeException(nameof(species));

        string id = species.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var uris = new List<Uri>(3);
        if (shiny && female)
            uris.Add(new Uri($"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/shiny/female/{id}.png"));
        if (shiny)
        {
            uris.Add(new Uri($"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/shiny/{id}.png"));
            if (female)
                uris.Add(new Uri($"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/female/{id}.png"));
        }
        else if (female)
            uris.Add(new Uri($"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/female/{id}.png"));

        if (shiny || female)
            uris.Add(new Uri($"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{id}.png"));
        if (uris.Count == 0)
            uris.Add(new Uri($"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{id}.png"));
        return uris;
    }

    internal static async Task<Image> LoadAsync(int species, bool shiny, bool female)
    {
        if (species is < 1 or > 1025)
            return null;

        string variant = (shiny ? "-shiny" : string.Empty) + (female ? "-female" : string.Empty);
        string path = Path.Combine(CacheDirectory, species.ToString("D4") + variant + ".png");
        byte[] bytes = await ReadCachedAsync(path).ConfigureAwait(false);
        if (bytes == null)
            bytes = await DownloadAsync(species, shiny, female, path).ConfigureAwait(false);
        if (bytes == null)
            return null;

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using Image decoded = Image.FromStream(stream);
            return new Bitmap(decoded);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (OutOfMemoryException)
        {
            return null;
        }
    }

    private static async Task<byte[]> ReadCachedAsync(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            if (IsValidSprite(bytes))
            {
                try { File.SetLastAccessTimeUtc(path, DateTime.UtcNow); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                return bytes;
            }
            TryDelete(path);
            return null;
        }
        catch (IOException)
        {
            TryDelete(path);
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task<byte[]> DownloadAsync(int species, bool shiny, bool female, string path)
    {
        await Downloads.WaitAsync().ConfigureAwait(false);
        try
        {
            // Another visible card may have completed this sprite while this request waited.
            byte[] cached = await ReadCachedAsync(path).ConfigureAwait(false);
            if (cached != null)
                return cached;

            byte[] bytes = null;
            foreach (Uri source in SpriteUris(species, shiny, female))
            {
                using HttpResponseMessage response = await Client.GetAsync(source, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaximumSpriteBytes)
                    continue;
                byte[] candidate = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                if (IsValidSprite(candidate))
                {
                    bytes = candidate;
                    break;
                }
            }
            if (bytes == null)
                return null;

            string temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(CacheDirectory);
                await File.WriteAllBytesAsync(temporary, bytes).ConfigureAwait(false);
                File.Move(temporary, path, overwrite: true);
                PruneCache();
            }
            catch (IOException)
            {
                // Keep the in-memory result usable when the cache is read-only or full.
                TryDelete(temporary);
            }
            catch (UnauthorizedAccessException)
            {
                // Keep the in-memory result usable when the cache is unavailable.
                TryDelete(temporary);
            }
            return bytes;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        finally
        {
            Downloads.Release();
        }
    }

    private static void PruneCache()
    {
        try
        {
            var directory = new DirectoryInfo(CacheDirectory);
            foreach (FileInfo temporary in directory.GetFiles("*.tmp"))
            {
                try { temporary.Delete(); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            FileInfo[] files = directory.GetFiles("*.png");
            foreach (FileInfo oldFile in files.OrderBy(file => file.LastAccessTimeUtc).Take(Math.Max(0, files.Length - MaximumCachedSprites)))
            {
                try { oldFile.Delete(); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static bool IsValidSprite(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 8 || bytes.Length > MaximumSpriteBytes ||
            bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71)
            return false;
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using Image image = Image.FromStream(stream);
            return image.Width is > 0 and <= 512 && image.Height is > 0 and <= 512;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (OutOfMemoryException)
        {
            return false;
        }
    }
}
