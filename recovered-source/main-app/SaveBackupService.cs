using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PKHeX.Core;

internal static class SaveBackupService
{
    private const int MaxRestoreEntries = 512;
    private const long MaxRestoreBytes = 32L * 1024 * 1024;

    public static string UniqueBackupPath(string sourcePath)
    {
        return sourcePath + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N");
    }

    internal static void ReplaceFolderFromStaging(string staging, string destination, string previous)
        => ReplaceFolderFromStaging(staging, destination, previous, Directory.Move);

    internal static void ReplaceFolderFromStaging(string staging, string destination, string previous, Action<string, string> moveDirectory)
    {
        if (moveDirectory == null) throw new ArgumentNullException(nameof(moveDirectory));
        bool hadCurrent = Directory.Exists(destination);
        if (hadCurrent) moveDirectory(destination, previous);
        try { moveDirectory(staging, destination); }
        catch
        {
            if (hadCurrent && !Directory.Exists(destination) && Directory.Exists(previous))
                moveDirectory(previous, destination);
            throw;
        }

        if (Directory.Exists(previous))
        {
            try { Directory.Delete(previous, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    public static void ExtractLocalArchive(string archivePath, string destination)
    {
        using FileStream archiveFile = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        ExtractArchive(archiveFile, destination);
    }

    public static void ExtractArchive(Stream archiveStream, string destination)
    {
        string root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string rootPrefix = root + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(root);

        using ZipArchive archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
        byte[] buffer = new byte[64 * 1024];
        long totalBytes = 0;
        int entryCount = 0;
        int fileCount = 0;

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (++entryCount > MaxRestoreEntries || entry.Length < 0 || entry.Length > MaxRestoreBytes - totalBytes)
                throw new InvalidDataException("O backup excede os limites de restauração (512 itens ou 32 MB descompactados).");
            totalBytes += entry.Length;

            string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(root, relative));
            if (entry.FullName.IndexOf(':') >= 0 || !fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("O backup contém um caminho inválido.");

            if (relative.EndsWith(Path.DirectorySeparatorChar))
            {
                if (entry.Length != 0)
                    throw new InvalidDataException("O backup contém uma pasta inválida.");
                Directory.CreateDirectory(fullPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using Stream source = entry.Open();
            using FileStream output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            long copied = 0;
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                copied += read;
                if (copied > entry.Length)
                    throw new InvalidDataException("O backup contém dados além do tamanho declarado.");
                output.Write(buffer, 0, read);
            }
            if (copied != entry.Length)
                throw new InvalidDataException("Um arquivo do backup está incompleto.");
            fileCount++;
        }

        if (fileCount == 0)
            throw new InvalidDataException("O backup não contém arquivos de save.");
    }

    public static void CreateVerifiedArchive(string saves, string archivePath)
        => CreateVerifiedArchive(saves, archivePath, (source, temporary) => ZipFile.CreateFromDirectory(source, temporary, CompressionLevel.Fastest, includeBaseDirectory: false));

    internal static void CreateVerifiedArchive(string saves, string archivePath, Action<string, string> createArchive)
    {
        if (createArchive == null) throw new ArgumentNullException(nameof(createArchive));
        if (!Directory.Exists(saves))
            throw new DirectoryNotFoundException("A pasta de saves não existe.");

        string[] files = Directory.GetFiles(saves, "*", SearchOption.AllDirectories);
        if (files.Length == 0)
            throw new InvalidOperationException("Não há saves locais para enviar.");
        ValidateSourceFiles(files);

        string before = Fingerprint(saves, files);
        AtomicArchiveFile.CreateVerified(archivePath, temporary => createArchive(saves, temporary), temporary =>
        {
            ValidateArchiveEntries(temporary);
            string[] currentFiles = Directory.GetFiles(saves, "*", SearchOption.AllDirectories);
            if (!string.Equals(before, Fingerprint(saves, currentFiles), StringComparison.Ordinal))
                throw new IOException("Os saves mudaram durante a criação do backup. Feche o emulador e tente novamente.");
        });
    }

    public static string CreateIfChanged(string root, string saveFolderName)
    {
        string saves = SaveProfileService.ActiveFolder(root, saveFolderName);
        string automatic = Path.Combine(root, "Saves", "Backups", "Automaticos");
        return CreateIfChanged(saves, automatic, SaveProfileService.CloudId(root, saveFolderName));
    }

    public static string LatestAutomaticBackup(string root, string saveFolderName)
    {
        string automatic = Path.Combine(root, "Saves", "Backups", "Automaticos");
        if (!Directory.Exists(automatic)) return null;
        string prefix = Sanitize(Path.GetFileName(SaveProfileService.CloudId(root, saveFolderName))) + "-";
        return Directory.GetFiles(automatic, "*.zip", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public static List<BackupArchiveChoice> BackupsForActiveProfile(string root, string saveFolderName)
    {
        string backups = Path.Combine(root, "Saves", "Backups");
        if (!Directory.Exists(backups)) return new List<BackupArchiveChoice>();

        string profile = Path.GetFileName(SaveProfileService.CloudId(root, saveFolderName));
        string safeProfile = Sanitize(profile);
        string automatic = Path.Combine(backups, "Automaticos");
        var files = Directory.GetFiles(backups, "*.zip", SearchOption.TopDirectoryOnly)
            .Concat(Directory.Exists(automatic) ? Directory.GetFiles(automatic, "*.zip", SearchOption.TopDirectoryOnly) : Array.Empty<string>())
            .Where(path =>
            {
                string name = Path.GetFileName(path);
                return name.StartsWith(profile + " - ", StringComparison.OrdinalIgnoreCase) ||
                       name.StartsWith(safeProfile + "-", StringComparison.OrdinalIgnoreCase);
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Select(path => new BackupArchiveChoice(path))
            .ToList();
        return files;
    }

    public static void ValidateForGame(string staging, GameInfo game)
    {
        string[] files = Directory.GetFiles(staging, "*", SearchOption.AllDirectories);
        string[] saveFiles = files.Where(path => Path.GetExtension(path).ToLowerInvariant() is ".sav" or ".dsv" or ".dat" or ".bin").ToArray();
        if (saveFiles.Length == 0)
            throw new InvalidDataException("O ZIP não contém um arquivo de save reconhecido.");

        foreach (string path in saveFiles)
        {
            if (new FileInfo(path).Length > 2 * 1024 * 1024)
                throw new InvalidDataException("Um save do ZIP excede o limite de 2 MB.");
            SaveFile save;
            try { save = SaveUtil.GetSaveFile(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                throw new InvalidDataException("Não foi possível ler um dos saves do ZIP.", ex);
            }
            if (!SaveProfileService.IsSaveForGame(save, game) || !save.ChecksumsValid)
                throw new InvalidDataException("O ZIP contém um save incompatível com " + game.Title + " ou com checksums inválidos.");
        }
    }

    internal static void ValidateArchiveForGame(string archivePath, GameInfo game)
    {
        if (game == null) throw new ArgumentNullException(nameof(game));
        string staging = Path.Combine(Path.GetTempPath(), "pokemons-play-validate-" + Guid.NewGuid().ToString("N"));
        try
        {
            ExtractLocalArchive(archivePath, staging);
            ValidateForGame(staging, game);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                try { Directory.Delete(staging, recursive: true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
    }

    public static string CreateIfChanged(string saves, string automatic, string saveFolderName)
    {
        if (!Directory.Exists(saves))
            return null;

        string[] files = Directory.GetFiles(saves, "*", SearchOption.AllDirectories);
        if (files.Length == 0)
            return null;

        ValidateSourceFiles(files);
        string hash = Fingerprint(saves, files);
        Directory.CreateDirectory(automatic);

        string safeName = Sanitize(Path.GetFileName(saveFolderName));
        string destination = Path.Combine(automatic, safeName + "-" + hash + ".zip");
        if (File.Exists(destination))
            return null;

        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            ZipFile.CreateFromDirectory(saves, temporary, CompressionLevel.Fastest, includeBaseDirectory: false);
            ValidateArchiveEntries(temporary);
            string[] currentFiles = Directory.GetFiles(saves, "*", SearchOption.AllDirectories);
            if (!string.Equals(hash, Fingerprint(saves, currentFiles), StringComparison.Ordinal))
                throw new IOException("Os saves mudaram durante a criação do backup. Tente novamente com o jogo fechado.");

            try
            {
                File.Move(temporary, destination);
            }
            catch (IOException) when (File.Exists(destination))
            {
                return null;
            }
            return destination;
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static void ValidateSourceFiles(string[] files)
    {
        if (files.Length > MaxRestoreEntries)
            throw new InvalidDataException("O save excede o limite de 512 arquivos por backup.");

        long totalBytes = 0;
        foreach (string file in files)
        {
            long length = new FileInfo(file).Length;
            if (length > MaxRestoreBytes - totalBytes)
                throw new InvalidDataException("O save excede o limite de 32 MB descompactados por backup.");
            totalBytes += length;
        }
    }

    private static void ValidateArchiveEntries(string archivePath)
    {
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > MaxRestoreEntries)
            throw new InvalidDataException("O backup excede o limite de 512 itens e não pode ser restaurado.");

        long totalBytes = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (entry.Length < 0 || entry.Length > MaxRestoreBytes - totalBytes)
                throw new InvalidDataException("O backup excede o limite de 32 MB descompactados e não pode ser restaurado.");
            totalBytes += entry.Length;
        }
    }

    private static string Fingerprint(string directory, string[] files)
    {
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[64 * 1024];
        foreach (string file in files)
        {
            string relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
            byte[] name = Encoding.UTF8.GetBytes(relative);
            hash.AppendData(BitConverter.GetBytes(name.Length));
            hash.AppendData(name);

            using FileStream input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            hash.AppendData(BitConverter.GetBytes(input.Length));
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                hash.AppendData(buffer, 0, read);
            hash.AppendData(new byte[] { 0 });
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string Sanitize(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(value) ? "save" : value;
    }
}
