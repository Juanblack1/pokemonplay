using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

internal static class BundledEmulatorArchive
{
    private const long MaximumBytes = 2L * 1024 * 1024 * 1024;
    private static readonly string[] RequiredFiles =
    {
        "Emulators/RetroArch/retroarch.exe",
        "Emulators/RetroArch/cores/mgba_libretro.dll",
        "Emulators/RetroArch/cores/melondsds_libretro.dll",
        "Emulators/Azahar/azahar.exe",
        "Emulators/THIRD_PARTY.txt"
    };

    internal static void EnsureExtracted(string runtimeDirectory)
    {
        string runtime = Path.GetFullPath(runtimeDirectory);
        string archivePath = Path.Combine(runtime, "emulators-runtime.zip");
        if (!File.Exists(archivePath)) return;
        RejectReparseAncestors(archivePath);
        string target = Path.Combine(runtime, "Emulators");
        string stage = Path.Combine(runtime, ".emulators-extract-" + Guid.NewGuid().ToString("N"));
        using (var archive = ZipFile.OpenRead(archivePath))
        {
            var entries = ValidateEntries(archive);
            if (Directory.Exists(target))
            {
                // A previous startup may have completed the atomic move before
                // deleting the archive. Never replace an existing emulator tree.
                VerifyExisting(runtime, entries);
            }
            else
            {
                if (File.Exists(target)) throw new InvalidDataException("O destino dos emuladores não é uma pasta.");
                Directory.CreateDirectory(stage);
                try
                {
                    byte[] buffer = new byte[128 * 1024];
                    var checkedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    RejectReparseAncestors(stage, checkedDirectories);
                    foreach (var item in entries)
                    {
                        string destination = GetDestination(stage, item.Path);
                        RejectReparseAncestors(destination, checkedDirectories);
                        if (item.Directory) {
                            Directory.CreateDirectory(destination);
                            RejectReparseAncestors(destination, checkedDirectories);
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destination));
                            RejectReparseAncestors(Path.GetDirectoryName(destination), checkedDirectories);
                            using var source = item.Entry.Open();
                            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length);
                            CopyExactly(source, output, item.Entry.Length, buffer);
                        }
                    }
                    foreach (string required in RequiredFiles)
                        if (!File.Exists(GetDestination(stage, required))) throw new InvalidDataException("Emulador obrigatório ausente: " + required);
                    RejectReparseAncestors(target);
                    Directory.Move(Path.Combine(stage, "Emulators"), target);
                }
                finally
                {
                    if (Directory.Exists(stage)) Directory.Delete(stage, true);
                }
            }
        }
        File.Delete(archivePath);
    }

    private static List<(ZipArchiveEntry Entry, string Path, bool Directory)> ValidateEntries(ZipArchive archive)
    {
        if (archive.Entries.Count == 0 || archive.Entries.Count > 32768)
            throw new InvalidDataException("Quantidade de arquivos inválida no pacote de emuladores.");
        var result = new List<(ZipArchiveEntry, string, bool)>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            string path = entry.FullName.Replace('\\', '/');
            bool directory = path.EndsWith("/", StringComparison.Ordinal);
            path = path.TrimEnd('/');
            if (path.Length == 0 || Path.IsPathRooted(path) || path.IndexOf(':') >= 0 ||
                !(path.StartsWith("Emulators/", StringComparison.OrdinalIgnoreCase) || (directory && path.Equals("Emulators", StringComparison.OrdinalIgnoreCase))))
                throw new InvalidDataException("Caminho inválido no pacote de emuladores.");
            foreach (string component in path.Split('/'))
            {
                if (component.Length == 0 || component == "." || component == ".." || component.EndsWith(".") || component.EndsWith(" ") || component.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new InvalidDataException("Caminho inválido no pacote de emuladores.");
                string device = component.Split('.')[0].ToUpperInvariant();
                if (device == "CON" || device == "PRN" || device == "AUX" || device == "NUL" ||
                    (device.Length == 4 && (device.StartsWith("COM") || device.StartsWith("LPT")) && device[3] >= '0' && device[3] <= '9'))
                    throw new InvalidDataException("Nome reservado no pacote de emuladores.");
            }
            int unixKind = (entry.ExternalAttributes >> 16) & 0xF000;
            if (unixKind == 0xA000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0 || !paths.Add(path))
                throw new InvalidDataException("Link ou caminho duplicado no pacote de emuladores.");
            if (directory && entry.Length != 0) throw new InvalidDataException("Pasta inválida no pacote de emuladores.");
            if (entry.Length < 0 || entry.Length > MaximumBytes - total) throw new InvalidDataException("Pacote de emuladores excede o limite de tamanho.");
            total += entry.Length;
            if (!directory) files.Add(path);
            result.Add((entry, path, directory));
        }
        foreach (var item in result)
        {
            int separator = item.Item2.LastIndexOf('/');
            while (separator >= 0)
            {
                if (files.Contains(item.Item2.Substring(0, separator))) throw new InvalidDataException("Arquivo usado como pasta no pacote de emuladores.");
                separator = item.Item2.LastIndexOf('/', separator - 1);
            }
        }
        foreach (string required in RequiredFiles)
            if (!files.Contains(required)) throw new InvalidDataException("Emulador obrigatório ausente: " + required);
        return result;
    }

    private static string GetDestination(string root, string relative)
    {
        string destination = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!destination.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Caminho fora da pasta de emuladores.");
        return destination;
    }

    private static void RejectReparseAncestors(string path, HashSet<string> checkedDirectories = null)
    {
        for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            if (checkedDirectories?.Contains(current) == true) break;
            if (File.Exists(current) || Directory.Exists(current)) {
                var attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Links não são permitidos na pasta de emuladores.");
                if ((attributes & FileAttributes.Directory) != 0) checkedDirectories?.Add(current);
            }
        }
    }

    private static void CopyExactly(Stream source, Stream output, long expected, byte[] buffer)
    {
        long copied = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) != 0)
        {
            if (read > expected - copied) throw new InvalidDataException("Tamanho inválido no arquivo de emuladores.");
            output.Write(buffer, 0, read);
            copied += read;
        }
        if (copied != expected) throw new InvalidDataException("Arquivo de emuladores incompleto.");
    }

    private static void VerifyExisting(string runtime, List<(ZipArchiveEntry Entry, string Path, bool Directory)> entries)
    {
        byte[] expected = new byte[128 * 1024];
        byte[] actual = new byte[expected.Length];
        foreach (var item in entries)
        {
            string destination = GetDestination(runtime, item.Path);
            RejectReparseAncestors(destination);
            if (item.Directory)
            {
                if (!Directory.Exists(destination)) throw new InvalidDataException("A pasta existente de emuladores não corresponde ao pacote.");
                continue;
            }
            using var existing = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (existing.Length != item.Entry.Length) throw new InvalidDataException("O emulador existente difere do pacote. Nenhum arquivo foi substituído.");
            using var source = item.Entry.Open();
            int count;
            long compared = 0;
            while ((count = source.Read(expected, 0, expected.Length)) != 0)
            {
                int offset = 0;
                while (offset < count)
                {
                    int read = existing.Read(actual, offset, count - offset);
                    if (read == 0) throw new InvalidDataException("O emulador existente está incompleto.");
                    offset += read;
                }
                for (int i = 0; i < count; i++)
                    if (expected[i] != actual[i]) throw new InvalidDataException("O emulador existente difere do pacote. Nenhum arquivo foi substituído.");
                compared += count;
                if (compared > item.Entry.Length) throw new InvalidDataException("Tamanho inválido no pacote de emuladores.");
            }
            if (compared != item.Entry.Length) throw new InvalidDataException("Arquivo de emuladores incompleto.");
        }
    }
}
