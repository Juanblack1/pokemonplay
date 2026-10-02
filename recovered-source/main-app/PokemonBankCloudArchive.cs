using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

/// <summary>Portable, validated representation of the global collection. It deliberately excludes unrelated files.</summary>
internal static class PokemonBankCloudArchive
{
    internal const string CloudId = "PokemonBankGlobal";
    private const int MaxPokemonFiles = 20000;
    private const long MaxExpandedBytes = 64L * 1024 * 1024;

    internal static int CountValid(string folder)
    {
        if (!Directory.Exists(folder)) return 0;
        return Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Count(path => IsPokemon(path) && TryReadPokemon(path));
    }

    internal static int CountPokemonInArchive(string archivePath)
    {
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        return archive.Entries.Count(entry => IsPokemon(entry.FullName));
    }

    internal static int CountPokemonInArchiveBytes(byte[] archiveBytes)
    {
        if (archiveBytes == null) throw new ArgumentNullException(nameof(archiveBytes));
        using MemoryStream stream = new MemoryStream(archiveBytes, writable: false);
        using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
        return archive.Entries.Count(entry => IsPokemon(entry.FullName));
    }

    internal static string GenerationSummary(string archivePath)
    {
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        return string.Join(" · ", archive.Entries
            .Where(entry => IsPokemon(entry.FullName))
            .GroupBy(entry => int.Parse(Path.GetExtension(entry.FullName).Substring(3)))
            .OrderBy(group => group.Key)
            .Select(group => $"Gen {group.Key}: {group.Count()}"));
    }

    internal static void CreateVerifiedArchive(string folder, string archivePath)
        => CreateVerifiedArchive(folder, archivePath, WriteCollectionArchive);

    internal static void CreateVerifiedArchive(string folder, string archivePath, Action<string, string> createArchive)
    {
        if (createArchive == null) throw new ArgumentNullException(nameof(createArchive));
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("A pasta do banco Pokémon não existe.");
        string[] files = GetCollectionFiles(folder);
        int pokemonCount = files.Count(IsPokemon);
        if (pokemonCount == 0) throw new InvalidOperationException("O banco local não contém Pokémon válidos para enviar.");
        if (pokemonCount > MaxPokemonFiles || files.Length > MaxPokemonFiles * 2)
            throw new InvalidDataException("O banco excede o limite de 20.000 Pokémon por backup.");

        long bytes = 0;
        foreach (string file in files)
        {
            var info = new FileInfo(file);
            if (info.Length > MaxExpandedBytes - bytes) throw new InvalidDataException("O banco excede 64 MB descompactados.");
            bytes += info.Length;
            if (IsPokemon(file) && !TryReadPokemon(file)) throw new InvalidDataException("Há um arquivo Pokémon inválido no banco: " + Path.GetFileName(file));
        }
        string before = Fingerprint(files);
        AtomicArchiveFile.CreateVerified(archivePath, temporary => createArchive(folder, temporary), temporary =>
        {
            ValidateArchive(temporary);
            if (before != Fingerprint(GetCollectionFiles(folder)))
                throw new IOException("O banco mudou durante a criação do backup. Tente novamente.");
        });
    }

    private static void WriteCollectionArchive(string folder, string archivePath)
    {
        string[] files = GetCollectionFiles(folder);
        using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            foreach (string file in files)
            {
                if (file.EndsWith(".origin.txt", StringComparison.OrdinalIgnoreCase))
                {
                    ZipArchiveEntry entry = zip.CreateEntry(Path.GetFileName(file), CompressionLevel.Fastest);
                    using StreamWriter writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                    writer.WriteLine("Source=Save local");
                    foreach (string line in File.ReadAllLines(file))
                    {
                        int separator = line.IndexOf('=');
                        if (separator <= 0) continue;
                        string key = line.Substring(0, separator);
                        if (key is not ("Game" or "Generation" or "Box" or "Slot" or "Archived")) continue;
                        string value = line.Substring(separator + 1).Replace('\r', ' ').Replace('\n', ' ');
                        writer.WriteLine(key + "=" + value);
                    }
                }
                else zip.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.Fastest);
            }
        }
    }

    internal static void ValidateExtractedFolder(string folder)
    {
        string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
        if (files.Length == 0 || files.Length > MaxPokemonFiles * 2)
            throw new InvalidDataException("O backup global está vazio ou contém itens demais.");
        int count = 0;
        long bytes = 0;
        var names = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
        {
            string relative = Path.GetRelativePath(folder, file);
            if (relative.IndexOf(Path.DirectorySeparatorChar) >= 0 || relative.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
                throw new InvalidDataException("O backup global contém uma pasta inesperada.");
            if (!names.Add(Path.GetFileName(file))) throw new InvalidDataException("O backup global contém nomes duplicados.");
            var info = new FileInfo(file);
            if (info.Length > MaxExpandedBytes - bytes) throw new InvalidDataException("O backup global excede 64 MB descompactados.");
            bytes += info.Length;
            if (IsPokemon(file))
            {
                if (!TryReadPokemon(file)) throw new InvalidDataException("O backup contém um Pokémon inválido: " + Path.GetFileName(file));
                count++;
            }
            else if (!file.EndsWith(".origin.txt", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("O backup contém um arquivo inesperado: " + Path.GetFileName(file));
        }
        if (count == 0 || count > MaxPokemonFiles) throw new InvalidDataException("O backup não contém uma coleção Pokémon válida.");
    }

    internal static void ValidateArchive(string archivePath)
    {
        using var zip = ZipFile.OpenRead(archivePath);
        if (zip.Entries.Count == 0 || zip.Entries.Count > MaxPokemonFiles * 2)
            throw new InvalidDataException("O arquivo do banco está vazio ou contém itens demais.");
        long size = 0;
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            if (entry.FullName != Path.GetFileName(entry.FullName) || entry.FullName.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
                throw new InvalidDataException("O backup contém um caminho inválido.");
            if (entry.Length > MaxExpandedBytes - size) throw new InvalidDataException("O backup excede 64 MB descompactados.");
            size += entry.Length;
        }
    }

    internal static void Restore(byte[] payload, string destination)
        => Restore(payload, destination, Directory.Move);

    internal static void Restore(byte[] payload, string destination, Action<string, string> moveDirectory)
    {
        if (moveDirectory == null) throw new ArgumentNullException(nameof(moveDirectory));
        string target = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string staging = target + ".restore-" + Guid.NewGuid().ToString("N");
        string previous = target + ".previous-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var memory = new MemoryStream(payload))
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Read))
            {
                Directory.CreateDirectory(staging);
                byte[] buffer = new byte[65536];
                long total = 0;
                if (zip.Entries.Count == 0 || zip.Entries.Count > MaxPokemonFiles * 2)
                    throw new InvalidDataException("O backup está vazio ou contém itens demais.");
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    if (entry.Length < 0 || entry.Length > MaxExpandedBytes - total) throw new InvalidDataException("O backup excede 64 MB descompactados.");
                    total += entry.Length;
                    string output = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                    if (entry.FullName.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || !output.StartsWith(staging + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("O backup contém um caminho inválido.");
                    using Stream input = entry.Open();
                    using FileStream file = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    int read; long copied = 0;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        copied += read;
                        if (copied > entry.Length || copied > MaxExpandedBytes) throw new InvalidDataException("O backup excede o tamanho permitido.");
                        file.Write(buffer, 0, read);
                    }
                    if (copied != entry.Length) throw new InvalidDataException("Um arquivo do backup está incompleto.");
                }
            }
            ValidateExtractedFolder(staging);
            SaveProfileService.EnsureEmulatorsClosed();
            bool hadCurrent = Directory.Exists(target);
            if (hadCurrent)
            {
                string backups = Path.Combine(Path.GetDirectoryName(target), "Backups", "Automaticos");
                Directory.CreateDirectory(backups);
                string localCopy = Path.Combine(backups, "BancoPokemon-antes-da-restauracao-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N"));
                CopyDirectory(target, localCopy);
                moveDirectory(target, previous);
            }
            try { moveDirectory(staging, target); }
            catch
            {
                if (hadCurrent && !Directory.Exists(target)) moveDirectory(previous, target);
                throw;
            }
            if (Directory.Exists(previous))
            {
                try { Directory.Delete(previous, true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                try { Directory.Delete(staging, true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.TopDirectoryOnly))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);
        foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.TopDirectoryOnly))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private static string[] GetCollectionFiles(string folder)
    {
        string[] all = Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly);
        // Bad and unknown files stay untouched on this PC but are deliberately excluded from a cloud snapshot.
        var pokemon = all.Where(path => IsPokemon(path) && TryReadPokemon(path)).ToArray();
        var validNames = new System.Collections.Generic.HashSet<string>(pokemon.Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);
        return all.Where(path => pokemon.Contains(path, StringComparer.OrdinalIgnoreCase) ||
            (path.EndsWith(".origin.txt", StringComparison.OrdinalIgnoreCase) && validNames.Contains(Path.GetFileName(path.Substring(0, path.Length - ".origin.txt".Length))))).ToArray();
    }

    private static bool IsPokemon(string path) => PokemonBankFileService.IsSupportedPokemonFile(path);

    private static bool TryReadPokemon(string path)
    {
        try
        {
            PokemonBankFileService.Read(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidDataException) { return false; }
    }

    private static string Fingerprint(string[] files)
    {
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string file in files)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetFileName(file)));
            using var input = File.OpenRead(file);
            byte[] buffer = new byte[65536]; int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0) hash.AppendData(buffer, 0, read);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
