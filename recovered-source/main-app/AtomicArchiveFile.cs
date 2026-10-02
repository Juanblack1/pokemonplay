using System;
using System.IO;

internal static class AtomicArchiveFile
{
    internal static void CreateVerified(string archivePath, Action<string> createArchive, Action<string> validateArchive)
    {
        if (string.IsNullOrWhiteSpace(archivePath)) throw new ArgumentException("O destino do backup é obrigatório.", nameof(archivePath));
        if (createArchive == null) throw new ArgumentNullException(nameof(createArchive));
        if (validateArchive == null) throw new ArgumentNullException(nameof(validateArchive));

        string destination = Path.GetFullPath(archivePath);
        string directory = Path.GetDirectoryName(destination);
        if (string.IsNullOrEmpty(directory)) throw new ArgumentException("O destino do backup não tem uma pasta válida.", nameof(archivePath));
        Directory.CreateDirectory(directory);

        string temporary = Path.Combine(directory, "." + Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            createArchive(temporary);
            if (!File.Exists(temporary)) throw new InvalidDataException("O arquivo temporário do backup não foi criado.");
            validateArchive(temporary);
            File.Move(temporary, destination);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
