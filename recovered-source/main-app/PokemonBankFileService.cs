using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PKHeX.Core;

internal static class PokemonBankFileService
{
    private const int MaxPokemonFileBytes = 512;
    internal static bool IsSupportedPokemonFile(string path)
        => Path.GetExtension(path).ToLowerInvariant() is ".pk3" or ".pk4" or ".pk5" or ".pk6" or ".pk7" or ".pk8" or ".pk9";

    public static PKM Read(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length <= 0 || info.Length > MaxPokemonFileBytes)
            throw new InvalidDataException("O arquivo Pokémon está vazio ou excede o limite de 512 bytes.");

        byte[] data = File.ReadAllBytes(path);
        PKM pokemon;
        try
        {
            pokemon = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pk3" when data.Length is 80 or 100 => new PK3(data),
            ".pk4" when data.Length is 136 or 236 => new PK4(data),
            ".pk5" when data.Length is 136 or 220 => new PK5(data),
            ".pk6" when data.Length is 232 or 260 => new PK6(data),
            ".pk7" when data.Length is 232 or 260 => new PK7(data),
            ".pk8" when data.Length is 328 or 344 => new PK8(data),
            ".pk9" when data.Length is 344 => new PK9(data),
                _ => EntityFormat.GetFromBytes(data)
            };
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or OverflowException or FormatException)
        {
            throw new InvalidDataException("O arquivo Pokémon está corrompido ou tem um formato inválido.", ex);
        }
        if (pokemon == null || !pokemon.ChecksumValid || pokemon.Species == 0)
            throw new InvalidDataException("O arquivo Pokémon tem checksum inválido ou não contém uma espécie.");
        int expectedFormat = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pk3" => 3, ".pk4" => 4, ".pk5" => 5, ".pk6" => 6, ".pk7" => 7, ".pk8" => 8, ".pk9" => 9, _ => 0
        };
        if (expectedFormat != 0 && pokemon.Format != expectedFormat)
            throw new InvalidDataException($"A extensão PK{expectedFormat} não corresponde ao formato PK{pokemon.Format} detectado.");
        return pokemon;
    }

    public static string ImportFile(string bankFolder, string sourcePath)
    {
        if (!IsSupportedPokemonFile(sourcePath))
            throw new InvalidDataException("O banco Pokémon aceita arquivos PK3 a PK9.");
        string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        PKM pokemon = Read(sourcePath);
        Directory.CreateDirectory(bankFolder);
        string name = $"{pokemon.Species:D4}-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}{extension}";
        string destination = Path.Combine(bankFolder, name);
        string origin = destination + ".origin.txt";
        string temporaryPokemon = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        string temporaryOrigin = origin + ".tmp-" + Guid.NewGuid().ToString("N");
        bool committed = false;
        try
        {
            byte[] normalized = new byte[pokemon.SIZE_PARTY];
            pokemon.RefreshChecksum();
            pokemon.WriteDecryptedDataParty(normalized);
            File.WriteAllBytes(temporaryPokemon, normalized);
            File.WriteAllText(temporaryOrigin, $"Source=Arquivo Pokémon importado\nGeneration={pokemon.Format}\nImported={DateTimeOffset.Now:O}\n");
            File.Move(temporaryPokemon, destination);
            File.Move(temporaryOrigin, origin);
            committed = true;
            return destination;
        }
        finally
        {
            if (File.Exists(temporaryPokemon)) File.Delete(temporaryPokemon);
            if (File.Exists(temporaryOrigin)) File.Delete(temporaryOrigin);
            if (!committed)
            {
                if (File.Exists(destination)) File.Delete(destination);
                if (File.Exists(origin)) File.Delete(origin);
            }
        }
    }

    public static string[] ImportFiles(string bankFolder, IEnumerable<string> sourcePaths)
    {
        if (sourcePaths == null) throw new ArgumentNullException(nameof(sourcePaths));
        string[] sources = sourcePaths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (string source in sources)
        {
            if (!IsSupportedPokemonFile(source))
                throw new InvalidDataException("O banco Pokémon aceita arquivos PK3 a PK9.");
            Read(source);
        }

        Directory.CreateDirectory(bankFolder);
        var imported = new List<string>();
        try
        {
            foreach (string source in sources)
                imported.Add(ImportFile(bankFolder, source));
            return imported.ToArray();
        }
        catch
        {
            foreach (string path in imported)
            {
                if (File.Exists(path)) File.Delete(path);
                string origin = path + ".origin.txt";
                if (File.Exists(origin)) File.Delete(origin);
            }
            throw;
        }
    }

    public static void ExportFile(string sourcePath, string destination)
    {
        PKM pokemon = Read(sourcePath);
        if (!string.Equals(Path.GetExtension(destination), "." + pokemon.Extension, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A extensão escolhida não corresponde ao formato do Pokémon selecionado.");

        string directory = Path.GetDirectoryName(Path.GetFullPath(destination));
        Directory.CreateDirectory(directory);
        string temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.Copy(sourcePath, temporary, false);
            File.Move(temporary, destination, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal static string RemoveFile(string bankFolder, string pokemonPath)
    {
        string folder = Path.GetFullPath(bankFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string source = Path.GetFullPath(pokemonPath);
        if (!string.Equals(Path.GetDirectoryName(source), folder, StringComparison.OrdinalIgnoreCase) || !IsSupportedPokemonFile(source))
            throw new InvalidDataException("Só é possível remover um Pokémon diretamente da pasta do Banco global.");
        Read(source);

        string backupFolder = RemovedFilesFolder(folder);
        Directory.CreateDirectory(backupFolder);
        string destination = Path.Combine(backupFolder, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N") + "-" + Path.GetFileName(source));
        string origin = source + ".origin.txt";
        string destinationOrigin = destination + ".origin.txt";
        string removedFrom = destination + ".removed-from.txt";
        bool pokemonMoved = false;
        bool originMoved = false;
        bool markerWritten = false;
        try
        {
            File.Move(source, destination);
            pokemonMoved = true;
            if (File.Exists(origin))
            {
                File.Move(origin, destinationOrigin);
                originMoved = true;
            }
            markerWritten = true;
            File.WriteAllText(removedFrom, Path.GetFileName(source));
            return destination;
        }
        catch
        {
            if (markerWritten && File.Exists(removedFrom)) File.Delete(removedFrom);
            if (originMoved && !File.Exists(origin)) File.Move(destinationOrigin, origin);
            if (pokemonMoved && !File.Exists(source)) File.Move(destination, source);
            throw;
        }
    }

    internal static string RestoreRemovedFile(string bankFolder, string removedPokemonPath)
    {
        string folder = Path.GetFullPath(bankFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string removedFolder = RemovedFilesFolder(folder);
        string source = Path.GetFullPath(removedPokemonPath);
        if (!string.Equals(Path.GetDirectoryName(source), removedFolder, StringComparison.OrdinalIgnoreCase) || !IsSupportedPokemonFile(source))
            throw new InvalidDataException("Escolha um Pokémon na pasta de removidos do Banco global.");
        PKM pokemon = Read(source);

        string marker = source + ".removed-from.txt";
        string originalName = File.Exists(marker) ? File.ReadAllText(marker).Trim() : ParseOriginalRemovedName(Path.GetFileName(source));
        if (string.IsNullOrWhiteSpace(originalName) || !string.Equals(Path.GetFileName(originalName), originalName, StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(originalName), Path.GetExtension(source), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Não foi possível confirmar o nome original deste Pokémon removido.");

        Directory.CreateDirectory(folder);
        string destination = Path.Combine(folder, originalName);
        if (File.Exists(destination))
            destination = Path.Combine(folder, $"{pokemon.Species:D4}-restaurado-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}{Path.GetExtension(source)}");
        string origin = source + ".origin.txt";
        string destinationOrigin = destination + ".origin.txt";
        bool pokemonMoved = false;
        bool originMoved = false;
        bool markerRemoved = false;
        try
        {
            File.Move(source, destination);
            pokemonMoved = true;
            if (File.Exists(origin))
            {
                File.Move(origin, destinationOrigin);
                originMoved = true;
            }
            if (File.Exists(marker))
            {
                File.Delete(marker);
                markerRemoved = true;
            }
            return destination;
        }
        catch
        {
            if (markerRemoved) File.WriteAllText(marker, originalName);
            if (originMoved && !File.Exists(origin)) File.Move(destinationOrigin, origin);
            if (pokemonMoved && !File.Exists(source)) File.Move(destination, source);
            throw;
        }
    }

    internal static string RemovedFilesFolder(string bankFolder)
        => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(bankFolder))!, "Backups", "Automaticos", "Pokemon-Banco-Removidos");

    internal static string[] RecoverableRemovedFiles(string bankFolder)
    {
        string folder = RemovedFilesFolder(bankFolder);
        if (!Directory.Exists(folder)) return Array.Empty<string>();
        return Directory.GetFiles(folder, "*.pk?", SearchOption.TopDirectoryOnly)
            .Where(IsSupportedPokemonFile)
            .Where(path =>
            {
                try { Read(path); return true; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return false; }
            })
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
    }

    private static string ParseOriginalRemovedName(string fileName)
    {
        if (fileName.Length <= 53 || fileName[19] != '-' || fileName[52] != '-' ||
            !DateTime.TryParseExact(fileName.Substring(0, 19), "yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _) ||
            !Guid.TryParseExact(fileName.Substring(20, 32), "N", out _))
            return string.Empty;
        return fileName.Substring(53);
    }
}
