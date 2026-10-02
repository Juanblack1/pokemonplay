using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Windows.Forms;
using PKHeX.Core;

internal static class AtomicArchiveCheck
{
    private static object Call(Type type, string name, params object[] arguments)
        => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == name && method.GetParameters().Length == arguments.Length)
            .Invoke(null, arguments);

    private static void Assert(bool value, string name)
    {
        if (!value) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }

    internal static void Run(string root, Assembly app)
    {
        string cloudConfirmation = (string)Call(app.GetType("SaveManagerView"), "BuildCloudActionConfirmation", true, "Pokemon FireRed", "Principal", "trainer@example.com");
        string cloudUploadConfirmation = (string)Call(app.GetType("SaveManagerView"), "BuildCloudActionConfirmation", false, "Pokemon FireRed", "Principal", "trainer@example.com");
        Assert(cloudConfirmation.Contains("validado para este jogo") && cloudConfirmation.Contains("backup") && cloudConfirmation.Contains("trainer@example.com") &&
               cloudUploadConfirmation.Contains("validado para este jogo") && cloudUploadConfirmation.Contains("substituirá"),
            "save cloud confirmations explain game validation before upload and restore");
        Type cloudInfoType = app.GetType("CloudBankInfo");
        DateTimeOffset cloudUpdated = new DateTimeOffset(2026, 10, 1, 12, 30, 0, TimeSpan.Zero);
        object remoteSaveSnapshot = Activator.CreateInstance(cloudInfoType, new object[] { true, 0, 1572864, (DateTimeOffset?)cloudUpdated, "revision-1" });
        object sameRemoteSnapshot = Activator.CreateInstance(cloudInfoType, new object[] { true, 0, 1572864, (DateTimeOffset?)cloudUpdated, "revision-1" });
        object changedRemoteSnapshot = Activator.CreateInstance(cloudInfoType, new object[] { true, 0, 1572864, (DateTimeOffset?)cloudUpdated, "revision-2" });
        object absentSnapshot = Activator.CreateInstance(cloudInfoType, new object[] { false, 0, 0, null });
        string uploadSnapshotConfirmation = (string)Call(app.GetType("SaveManagerView"), "BuildCloudSaveUploadConfirmation", remoteSaveSnapshot, "Pokemon FireRed", "Principal", "trainer@example.com");
        bool matchingRevision = (bool)Call(app.GetType("FirebaseCloudSaveService"), "IsSameCloudSnapshot", remoteSaveSnapshot, sameRemoteSnapshot);
        bool changedRevision = (bool)Call(app.GetType("FirebaseCloudSaveService"), "IsSameCloudSnapshot", remoteSaveSnapshot, changedRemoteSnapshot);
        bool missingStillMissing = (bool)Call(app.GetType("FirebaseCloudSaveService"), "IsSameCloudSnapshot", absentSnapshot, absentSnapshot);
        bool missingNowExists = (bool)Call(app.GetType("FirebaseCloudSaveService"), "IsSameCloudSnapshot", absentSnapshot, remoteSaveSnapshot);
        string newUploadConfirmation = (string)Call(app.GetType("SaveManagerView"), "BuildCloudSaveUploadConfirmation", absentSnapshot, "Pokemon FireRed", "Principal", "trainer@example.com");
        Assert(uploadSnapshotConfirmation.Contains((1572864 / 1024d / 1024d).ToString("0.00", CultureInfo.CurrentCulture) + " MB"), "per-game cloud upload confirmation includes compressed size");
        Assert(uploadSnapshotConfirmation.Contains(cloudUpdated.ToLocalTime().ToString("dd/MM/yyyy HH:mm")), "per-game cloud upload confirmation includes update time");
        Assert(uploadSnapshotConfirmation.Contains("somente se ela continuar igual") && newUploadConfirmation.Contains("ainda não tem uma cópia"), "per-game cloud upload confirmations explain conditional replacement and new copy");
        string restoreSnapshotConfirmation = (string)Call(app.GetType("SaveManagerView"), "BuildCloudSaveRestoreConfirmation", remoteSaveSnapshot, "Pokemon FireRed", "Principal", "trainer@example.com");
        string missingRestoreConfirmation = (string)Call(app.GetType("SaveManagerView"), "BuildCloudSaveRestoreConfirmation", absentSnapshot, "Pokemon FireRed", "Principal", "trainer@example.com");
        Assert(restoreSnapshotConfirmation.Contains("1.50 MB".Replace(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator)) &&
               restoreSnapshotConfirmation.Contains(cloudUpdated.ToLocalTime().ToString("dd/MM/yyyy HH:mm")) &&
               restoreSnapshotConfirmation.Contains("só será aplicada se continuar igual") && restoreSnapshotConfirmation.Contains("backup") &&
               missingRestoreConfirmation.Contains("não tem um save"),
            "per-game cloud restore confirms the remote snapshot and handles a missing copy");
        Assert(matchingRevision && !changedRevision && missingStillMissing && !missingNowExists,
            "per-game cloud upload confirms a concrete snapshot and detects changes while confirmation is open");

        string speciesSearchRoot = Path.Combine(root, "species-name-search");
        string speciesSearchFolder = Path.Combine(speciesSearchRoot, "Pokemon Bank");
        Directory.CreateDirectory(speciesSearchFolder);
        PK9 searchablePokemon = new() { Species = 25, Language = 2 };
        searchablePokemon.RefreshChecksum();
        byte[] searchableBytes = new byte[searchablePokemon.SIZE_PARTY];
        searchablePokemon.WriteDecryptedDataParty(searchableBytes);
        File.WriteAllBytes(Path.Combine(speciesSearchFolder, "0025-pikachu.pk9"), searchableBytes);
        using (Control speciesSearchView = (Control)Activator.CreateInstance(app.GetType("PokemonBankView"), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { speciesSearchRoot }, null))
        {
            TextBox searchBox = (TextBox)app.GetType("PokemonBankView").GetField("bankSpeciesSearch", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(speciesSearchView);
            searchBox.Text = "Pikachu";
            int[] matches = (int[])app.GetType("PokemonBankView").GetField("bankViewIndices", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(speciesSearchView);
            string details = (string)Call(app.GetType("PokemonBankView"), "BuildBankPokemonDetails", searchablePokemon, 0, "0025-pikachu.pk9");
            Assert(matches.Length == 1 && details.Contains("Pikachu"), "global bank search and details use PKHeX species names");
        }

        string singularStatus = (string)Call(app.GetType("SaveManagerView"), "BuildManualBackupSuccessStatus", "fire-red.zip", 1, 1536L);
        string pluralStatus = (string)Call(app.GetType("SaveManagerView"), "BuildManualBackupSuccessStatus", "profile.zip", 4, 2L * 1024 * 1024);
        Assert(singularStatus.Contains("1 arquivo") && singularStatus.Contains("KB") && singularStatus.EndsWith("fire-red.zip", StringComparison.Ordinal),
            "manual save backup summary shows a singular file count, size and archive name");
        Assert(pluralStatus.Contains("4 arquivos") && pluralStatus.Contains("MB") && pluralStatus.EndsWith("profile.zip", StringComparison.Ordinal),
            "manual save backup summary shows plural file count, size and archive name");

        string saveFolder = Path.Combine(root, "atomic-save-backup-source");
        Directory.CreateDirectory(saveFolder);
        string saveFile = Path.Combine(saveFolder, "game.sav");
        File.WriteAllBytes(saveFile, new byte[] { 1, 2, 3, 4 });
        string saveArchive = Path.Combine(root, "save-backup.zip");
        byte[] priorSaveArchive = { 9, 8, 7 };
        File.WriteAllBytes(saveArchive, priorSaveArchive);
        Action<string, string> mutateSaveDuringArchive = (source, temporary) =>
        {
            ZipFile.CreateFromDirectory(source, temporary, CompressionLevel.Fastest, includeBaseDirectory: false);
            File.WriteAllBytes(saveFile, new byte[] { 4, 3, 2, 1 });
        };
        try
        {
            Call(app.GetType("SaveBackupService"), "CreateVerifiedArchive", saveFolder, saveArchive, mutateSaveDuringArchive);
            throw new Exception("save backup accepted a source change during archive creation");
        }
        catch (TargetInvocationException ex) when (ex.InnerException is IOException) { }
        Assert(File.ReadAllBytes(saveArchive).SequenceEqual(priorSaveArchive) && Directory.GetFiles(root, ".save-backup.zip.*.tmp").Length == 0,
            "save backup preserves the prior archive and removes an unverified temporary file");

        string bankFolder = Path.Combine(root, "atomic-bank-source");
        Directory.CreateDirectory(bankFolder);
        PK9 pokemon = new() { Species = 25 };
        pokemon.RefreshChecksum();
        byte[] pokemonBytes = new byte[pokemon.SIZE_PARTY];
        pokemon.WriteDecryptedDataParty(pokemonBytes);
        string pokemonFile = Path.Combine(bankFolder, "0025-pikachu.pk9");
        File.WriteAllBytes(pokemonFile, pokemonBytes);
        string bankArchive = Path.Combine(root, "bank-backup.zip");
        byte[] priorBankArchive = { 6, 5, 4 };
        File.WriteAllBytes(bankArchive, priorBankArchive);
        Action<string, string> mutateBankDuringArchive = (source, temporary) =>
        {
            using (FileStream output = File.Create(temporary))
            using (ZipArchive zip = new ZipArchive(output, ZipArchiveMode.Create))
                zip.CreateEntryFromFile(pokemonFile, Path.GetFileName(pokemonFile), CompressionLevel.Fastest);
            File.WriteAllBytes(pokemonFile, new byte[] { 0, 1, 2 });
        };
        try
        {
            Call(app.GetType("PokemonBankCloudArchive"), "CreateVerifiedArchive", bankFolder, bankArchive, mutateBankDuringArchive);
            throw new Exception("bank backup accepted a source change during archive creation");
        }
        catch (TargetInvocationException ex) when (ex.InnerException is IOException) { }
        Assert(File.ReadAllBytes(bankArchive).SequenceEqual(priorBankArchive) && Directory.GetFiles(root, ".bank-backup.zip.*.tmp").Length == 0,
            "global bank backup preserves the prior archive and removes an unverified temporary file");

        string countedBank = Path.Combine(root, "cloud-count-bank");
        Directory.CreateDirectory(countedBank);
        File.WriteAllBytes(Path.Combine(countedBank, "0025-pikachu.pk9"), pokemonBytes);
        string countedArchive = Path.Combine(root, "cloud-count-bank.zip");
        Call(app.GetType("PokemonBankCloudArchive"), "CreateVerifiedArchive", countedBank, countedArchive);
        int archivePokemonCount = (int)Call(app.GetType("PokemonBankCloudArchive"), "CountPokemonInArchiveBytes", File.ReadAllBytes(countedArchive));
        Call(app.GetType("FirebaseCloudSaveService"), "ValidateCloudBankPokemonCount", 1, archivePokemonCount);
        Assert(archivePokemonCount == 1,
            "cloud bank ZIP count matches its manifest before restore");
        bool cloudCountMismatchRejected = false;
        try { Call(app.GetType("FirebaseCloudSaveService"), "ValidateCloudBankPokemonCount", 2, archivePokemonCount); }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidDataException) { cloudCountMismatchRejected = true; }
        Assert(cloudCountMismatchRejected,
            "cloud bank restore rejects a manifest count that differs from its ZIP");
        Call(app.GetType("FirebaseCloudSaveService"), "ValidateCloudBankPokemonCount", 0, archivePokemonCount);
        Assert(true, "legacy cloud bank snapshots without a Pokémon count remain compatible");

        string removedBank = Path.Combine(root, "removed-count-bank", "Pokemon Bank");
        string removedFolder = (string)Call(app.GetType("PokemonBankFileService"), "RemovedFilesFolder", removedBank);
        Directory.CreateDirectory(removedFolder);
        string validRemoved = Path.Combine(removedFolder, "0025-valid.pk9");
        string invalidRemoved = Path.Combine(removedFolder, "0031-corrupt.pk9");
        File.WriteAllBytes(validRemoved, pokemonBytes);
        File.WriteAllBytes(invalidRemoved, new byte[] { 1, 2, 3 });
        byte[] validRemovedBefore = File.ReadAllBytes(validRemoved);
        byte[] invalidRemovedBefore = File.ReadAllBytes(invalidRemoved);
        string[] recoverable = (string[])Call(app.GetType("PokemonBankFileService"), "RecoverableRemovedFiles", removedBank);
        Assert(recoverable.Length == 1 && recoverable[0] == validRemoved,
            "removed Pokémon count includes only files PKHeX can validate");
        Assert(File.ReadAllBytes(validRemoved).SequenceEqual(validRemovedBefore) && File.ReadAllBytes(invalidRemoved).SequenceEqual(invalidRemovedBefore),
            "checking removed Pokémon leaves valid and corrupt recovery files untouched");

        SAV5BW wrongGameSave = new(new byte[0x80000]);
        wrongGameSave.ClearBoxes();
        wrongGameSave.OT = "TESTE";
        wrongGameSave.Language = 2;
        wrongGameSave.TID16 = 12345;
        PKM wrongGamePokemon = wrongGameSave.BlankPKM.Clone();
        EntityTemplates.TemplateFields(wrongGamePokemon, wrongGameSave);
        wrongGamePokemon.Species = 25;
        wrongGamePokemon.PID = 0x12345678;
        wrongGamePokemon.CurrentLevel = 20;
        wrongGamePokemon.RefreshChecksum();
        wrongGameSave.SetBoxSlotAtIndex(wrongGamePokemon, 0, 0);
        byte[] wrongGameBytes = wrongGameSave.Write().ToArray();
        string wrongGamePath = Path.Combine(root, "cloud-wrong-generation.sav");
        File.WriteAllBytes(wrongGamePath, wrongGameBytes);
        var parsedWrongGame = SaveUtil.GetSaveFile(wrongGamePath);
        Assert(parsedWrongGame != null && parsedWrongGame.Generation == 5 && parsedWrongGame.ChecksumsValid,
            "cloud restore rejection fixture is a valid Gen 5 save");

        object fireRed = Activator.CreateInstance(app.GetType("GameInfo"));
        fireRed.GetType().GetField("Title").SetValue(fireRed, "FireRed");
        fireRed.GetType().GetField("Generation").SetValue(fireRed, 3);
        fireRed.GetType().GetField("SaveFolderName").SetValue(fireRed, "Pokemon FireRed");
        byte[] wrongCloudPayload;
        using (MemoryStream stream = new())
        {
            using (ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                ZipArchiveEntry entry = zip.CreateEntry("Pokemon Black.sav");
                using Stream output = entry.Open();
                output.Write(wrongGameBytes);
            }
            wrongCloudPayload = stream.ToArray();
        }
        string wrongCloudArchive = Path.Combine(root, "cloud-wrong-generation.zip");
        File.WriteAllBytes(wrongCloudArchive, wrongCloudPayload);
        object pokemonBlack = Activator.CreateInstance(app.GetType("GameInfo"));
        pokemonBlack.GetType().GetField("Title").SetValue(pokemonBlack, "Black");
        pokemonBlack.GetType().GetField("Generation").SetValue(pokemonBlack, 5);
        pokemonBlack.GetType().GetField("SaveFolderName").SetValue(pokemonBlack, "Pokemon Black");
        int validationFoldersBefore = Directory.GetDirectories(Path.GetTempPath(), "pokemons-play-validate-*").Length;
        Call(app.GetType("SaveBackupService"), "ValidateArchiveForGame", wrongCloudArchive, pokemonBlack);
        Assert(Directory.GetDirectories(Path.GetTempPath(), "pokemons-play-validate-*").Length == validationFoldersBefore,
            "cloud upload preflight accepts a matching game save and removes its extracted validation folder");
        bool wrongUploadRejected = false;
        try { Call(app.GetType("SaveBackupService"), "ValidateArchiveForGame", wrongCloudArchive, fireRed); }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidDataException) { wrongUploadRejected = true; }
        Assert(wrongUploadRejected && Directory.GetDirectories(Path.GetTempPath(), "pokemons-play-validate-*").Length == validationFoldersBefore,
            "cloud upload preflight rejects a wrong-game save and cleans its extracted validation folder");

        string cloudDestination = Path.Combine(root, "cloud-restore-validation", "current-save");
        Directory.CreateDirectory(cloudDestination);
        byte[] localSave = { 8, 6, 4, 2 };
        string localSavePath = Path.Combine(cloudDestination, "local.sav");
        File.WriteAllBytes(localSavePath, localSave);
        bool wrongCloudSaveRejected = false;
        try
        {
            Call(app.GetType("FirebaseCloudSaveService"), "RestoreArchive", wrongCloudPayload, cloudDestination, fireRed);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidDataException) { wrongCloudSaveRejected = true; }
        Assert(wrongCloudSaveRejected && File.ReadAllBytes(localSavePath).SequenceEqual(localSave),
            "cloud restore rejects a valid save from another game before replacing local progress");
        Assert(Directory.GetDirectories(Path.GetDirectoryName(cloudDestination), "current-save.restore-*").Length == 0 &&
               !Directory.Exists(Path.Combine(Path.GetDirectoryName(cloudDestination), "Backups", "Automaticos")),
            "incompatible cloud restore cleans staging without creating a replacement backup");
    }
}
