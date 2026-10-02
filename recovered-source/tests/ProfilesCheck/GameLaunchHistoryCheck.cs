using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static class GameLaunchHistoryCheck
{
    private static object Call(Type type, string name, object target, params object[] args)
        => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Single(method => method.Name == name && method.GetParameters().Length == args.Length)
            .Invoke(target, args);

    private static object Get(object value, string name)
        => value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value);

    private static string CardTitle(Control card)
    {
        object game = Get(card, "game");
        return (string)game.GetType().GetField("Title").GetValue(game);
    }

    private static IEnumerable Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void Assert(bool value, string label)
    {
        if (!value) throw new Exception(label);
        Console.WriteLine("PASS " + label);
    }

    internal static void Run(string root, Assembly app)
    {
        Type history = app.GetType("GameLaunchHistoryService");
        string fixture = Path.Combine(root, "game-launch-history");
        string coverRoot = Path.Combine(fixture, "cover-fixtures");
        string coverFolder = Path.Combine(coverRoot, "Pokemon - Capas");
        Directory.CreateDirectory(coverFolder);
        string validCoverPath = Path.Combine(coverFolder, "FireRed.png");
        using (var sampleCover = new Bitmap(8, 8)) sampleCover.Save(validCoverPath, ImageFormat.Png);
        Type gameInfoType = app.GetType("GameInfo");
        object coverGame = Activator.CreateInstance(gameInfoType);
        gameInfoType.GetField("Title").SetValue(coverGame, "FireRed");
        gameInfoType.GetField("Subtitle").SetValue(coverGame, "Kanto · Game Boy Advance");
        gameInfoType.GetField("Cover").SetValue(coverGame, Path.Combine("Pokemon - Capas", "FireRed.png"));
        gameInfoType.GetField("Accent").SetValue(coverGame, Color.Red);
        gameInfoType.GetField("Generation").SetValue(coverGame, 3);
        gameInfoType.GetField("SaveFolderName").SetValue(coverGame, "Pokemon FireRed");
        Type gameCardType = app.GetType("GameCard");
        using (var validCoverCard = (Control)Activator.CreateInstance(gameCardType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { coverGame, coverRoot }, null))
        {
            Assert(Get(validCoverCard, "cover") is Image && validCoverCard.Controls.OfType<Button>().All(button => button.Text != "Abrir pasta da capa"), "game card loads a valid cover image without showing the missing-cover action");
            using FileStream coverLockCheck = new(validCoverPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert(coverLockCheck.CanRead, "loaded game cover does not keep its source file locked");
        }
        Type saveListItemType = app.GetType("SaveListItem");
        using (var saveListItem = (Control)Activator.CreateInstance(saveListItemType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { coverGame, coverRoot, new EventHandler((_, _) => { }) }, null))
        {
            Assert(Get(saveListItem, "cover") is Image, "save manager game list loads the matching cover");
            using FileStream coverLockCheck = new(validCoverPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert(coverLockCheck.CanRead, "save manager game-list cover does not keep its source file locked");
        }
        Type saveManagerType = app.GetType("SaveManagerView");
        using (var saveManager = (Control)Activator.CreateInstance(saveManagerType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { coverRoot }, null))
        {
            PictureBox detailCover = Descendants(saveManager).OfType<PictureBox>().FirstOrDefault(picture => picture.Image != null);
            Assert(detailCover != null, "save manager game detail loads the matching cover");
            using FileStream coverLockCheck = new(validCoverPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert(coverLockCheck.CanRead, "save manager game-detail cover does not keep its source file locked");
        }
        string corruptCoverPath = Path.Combine(coverFolder, "Broken.png");
        File.WriteAllText(corruptCoverPath, "not an image");
        gameInfoType.GetField("Cover").SetValue(coverGame, Path.Combine("Pokemon - Capas", "Broken.png"));
        using (var corruptCoverCard = (Control)Activator.CreateInstance(gameCardType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { coverGame, coverRoot }, null))
            Assert(Get(corruptCoverCard, "cover") == null && corruptCoverCard.AccessibleDescription.Contains("Arquivo esperado") && corruptCoverCard.Controls.OfType<Button>().Any(button => button.Text == "Abrir pasta da capa"), "corrupt cover falls back safely and offers access to its replacement folder");

        string catalogRoot = Path.Combine(fixture, "cover-discovery");
        string romsDirectory = Path.Combine(catalogRoot, "Pokemon 3DS - Arquivos", "Roms");
        string coverDirectory = Path.Combine(catalogRoot, "Pokemon 3DS - Capas");
        Directory.CreateDirectory(romsDirectory);
        Directory.CreateDirectory(Path.Combine(catalogRoot, "Pokemon 3DS - Arquivos", "Azahar"));
        File.WriteAllText(Path.Combine(catalogRoot, "Pokemon 3DS - Arquivos", "Azahar", "azahar.exe"), "fixture");
        File.WriteAllText(Path.Combine(romsDirectory, "Pokémon X.3ds"), "fixture");
        Type catalogType = app.GetType("GameCatalog");
        IEnumerable<GameInfoProxy> Discover3DsGames() => ((IEnumerable)Call(catalogType, "Build", null, catalogRoot))
            .Cast<object>().Select(game => new GameInfoProxy(game));
        GameInfoProxy discoveredGame = Discover3DsGames().Single(game => game.Title == "POKÉMON X");
        string expectedCover = Path.Combine("Pokemon 3DS - Capas", "Pokémon X.png");
        Assert(discoveredGame.Cover == expectedCover, "3DS game without a cover gets an exact default PNG path even when its cover folder is missing");
        using (var missing3dsCoverCard = (Control)Activator.CreateInstance(gameCardType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { discoveredGame.Value, catalogRoot }, null))
        {
            Button openCoverFolder = missing3dsCoverCard.Controls.OfType<Button>().Single(button => button.Text == "Abrir pasta da capa");
            string expectedCoverFolder = Path.Combine(catalogRoot, "Pokemon 3DS - Capas");
            Assert(missing3dsCoverCard.AccessibleDescription.Contains(expectedCover) && openCoverFolder.AccessibleDescription.Contains(expectedCoverFolder) && !Directory.Exists(expectedCoverFolder), "3DS card announces the suggested filename and actionable folder without creating it until clicked");
            string missingCoverPreview = Environment.GetEnvironmentVariable("POKEMONPLAY_MISSING_COVER_PREVIEW");
            if (!string.IsNullOrWhiteSpace(missingCoverPreview))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(missingCoverPreview)));
                using var image = new Bitmap(missing3dsCoverCard.Width, missing3dsCoverCard.Height);
                missing3dsCoverCard.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                image.Save(missingCoverPreview, ImageFormat.Png);
            }
        }
        string nestedCoverFolderPath = (string)gameCardType.GetMethod("GetCoverDirectoryPath", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { catalogRoot, Path.Combine("Pokemon 3DS - Capas", "Alternate", "Pokémon X.png") });
        Assert(nestedCoverFolderPath == Path.Combine(catalogRoot, "Pokemon 3DS - Capas", "Alternate"), "missing nested cover action resolves the matching subfolder");
        bool coverFolderEscapeRejected = false;
        try { gameCardType.GetMethod("GetCoverDirectoryPath", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { catalogRoot, Path.Combine("..", "outside.png") }); }
        catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException) { coverFolderEscapeRejected = true; }
        Assert(coverFolderEscapeRejected, "cover-folder action rejects paths outside the installation");
        Directory.CreateDirectory(coverDirectory);
        using (var sampleCover = new Bitmap(8, 8)) sampleCover.Save(Path.Combine(coverDirectory, "Pokémon X.jpeg"), ImageFormat.Jpeg);
        Assert(Discover3DsGames().Single(game => game.Title == "POKÉMON X").Cover == Path.Combine("Pokemon 3DS - Capas", "Pokémon X.jpeg"), "3DS cover discovery accepts the matching JPEG filename");
        using (var sampleCover = new Bitmap(8, 8)) sampleCover.Save(Path.Combine(coverDirectory, "Pokémon X.png"), ImageFormat.Png);
        Assert(Discover3DsGames().Single(game => game.Title == "POKÉMON X").Cover == expectedCover, "3DS cover discovery prefers PNG when multiple matching images exist");

        string nestedRoms = Path.Combine(romsDirectory, "Alternate");
        Directory.CreateDirectory(nestedRoms);
        string nestedRom = Path.Combine(nestedRoms, "Pokémon X.3ds");
        File.WriteAllText(nestedRom, "fixture");
        string alternateFormatRom = Path.Combine(romsDirectory, "Pokémon X.cci");
        File.WriteAllText(alternateFormatRom, "fixture");
        GameInfoProxy[] sameTitleGames = Discover3DsGames().Where(game => game.Title.StartsWith("POKÉMON X", StringComparison.Ordinal)).ToArray();
        GameInfoProxy rootCopy = sameTitleGames.Single(game => game.Title == "POKÉMON X · POKÉMON X.3DS");
        GameInfoProxy alternateFormatCopy = sameTitleGames.Single(game => game.Title.EndsWith("POKÉMON X.CCI", StringComparison.Ordinal));
        GameInfoProxy nestedCopy = sameTitleGames.Single(game => game.Title.Contains("ALTERNATE›", StringComparison.Ordinal));
        Assert(sameTitleGames.Select(game => game.Title).Distinct(StringComparer.Ordinal).Count() == 3 &&
            sameTitleGames.Select(game => game.SaveFolderName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3 &&
            sameTitleGames.Select(game => game.Arguments).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3,
            "same-title 3DS ROMs from folders and alternate formats keep distinct titles, save folders and launch paths");
        Assert(alternateFormatCopy.Cover == expectedCover, "alternate 3DS ROM formats retain the matching flat cover");
        string nestedCoverFolder = Path.Combine(coverDirectory, "Alternate");
        Directory.CreateDirectory(nestedCoverFolder);
        using (var sampleCover = new Bitmap(8, 8)) sampleCover.Save(Path.Combine(nestedCoverFolder, "Pokémon X.jpeg"), ImageFormat.Jpeg);
        Assert(Discover3DsGames().Single(game => game.Title == nestedCopy.Title).Cover == Path.Combine("Pokemon 3DS - Capas", "Alternate", "Pokémon X.jpeg"),
            "3DS cover lookup prefers a folder-matched image for duplicate ROM names");
        string nestedOnlyRom = Path.Combine(nestedRoms, "Pokémon Y.3ds");
        File.WriteAllText(nestedOnlyRom, "fixture");
        Assert(Discover3DsGames().Single(game => game.Title == "POKÉMON Y").Cover == Path.Combine("Pokemon 3DS - Capas", "Alternate", "Pokémon Y.png"),
            "3DS missing-cover guidance mirrors the ROM subfolder path");

        DateTimeOffset firstDate = new(2025, 2, 3, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset secondDate = firstDate.AddDays(1);
        DateTimeOffset latestDate = firstDate.AddDays(2);
        Assert((bool)Call(history, "TryRecordLaunch", null, fixture, "Platinum", firstDate), "first successful launch creates a local history entry");
        Assert((bool)Call(history, "TryRecordLaunch", null, fixture, "FireRed", secondDate), "recent game history records a second title");
        Assert((bool)Call(history, "TryRecordLaunch", null, fixture, "firered", latestDate), "replaying a title updates its existing history entry");
        Assert((bool)Call(history, "TryAddPlayTime", null, fixture, "FIRERED", TimeSpan.FromMinutes(75).Add(TimeSpan.FromSeconds(12))) &&
            (bool)Call(history, "TryAddPlayTime", null, fixture, "FireRed", TimeSpan.FromSeconds(35)), "completed play sessions accumulate in the existing game history entry");
        Assert((bool)Call(history, "TryRecordLaunch", null, fixture, "FireRed", latestDate), "relaunch updates the recent date without resetting accumulated play time");

        var entries = ((IEnumerable)Call(history, "Load", null, fixture)).Cast<object>().ToArray();
        string[] titles = entries.Select(entry => (string)entry.GetType().GetProperty("Title").GetValue(entry)).ToArray();
        Assert(titles.SequenceEqual(new[] { "FireRed", "Platinum" }), "history loads newest game first and keeps one entry per title");
        Assert((DateTimeOffset)entries[0].GetType().GetProperty("PlayedAt").GetValue(entries[0]) == latestDate, "history keeps the latest launch timestamp");
        Assert((long)entries[0].GetType().GetProperty("TotalPlayTimeSeconds").GetValue(entries[0]) == 4547, "history preserves and sums play time across launches");

        string legacyHistoryRoot = Path.Combine(fixture, "legacy-history");
        string legacyHistoryPath = Path.Combine(legacyHistoryRoot, "Settings", "GameLaunchHistory.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyHistoryPath));
        File.WriteAllText(legacyHistoryPath, "[{\"Title\":\"Emerald\",\"PlayedAt\":\"2025-02-03T10:00:00+00:00\"}]");
        object legacyEntry = ((IEnumerable)Call(history, "Load", null, legacyHistoryRoot)).Cast<object>().Single();
        Assert((long)legacyEntry.GetType().GetProperty("TotalPlayTimeSeconds").GetValue(legacyEntry) == 0, "older recent-history files load with play time marked as unmeasured");
        string timedSessionRoot = Path.Combine(fixture, "timed-session");
        Assert((bool)Call(history, "TryRecordLaunch", null, timedSessionRoot, "Timed Session", firstDate), "timed-session fixture starts with a recent game entry");
        string commandInterpreter = Environment.GetEnvironmentVariable("ComSpec");
        Type gameHostType = app.GetType("GameHostForm");
        using (var timedSessionHost = (Form)Activator.CreateInstance(gameHostType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { commandInterpreter, "cmd", "/c ping -n 3 127.0.0.1 > nul", "Timed Session", "Principal", timedSessionRoot }, null))
        {
            gameHostType.GetProperty("ProcessStarter", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(timedSessionHost,
                new Func<ProcessStartInfo, Process>(startInfo =>
                {
                    startInfo.UseShellExecute = false;
                    startInfo.CreateNoWindow = true;
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    return Process.Start(startInfo);
                }));
            timedSessionHost.Show();
            DateTimeOffset launchDeadline = DateTimeOffset.UtcNow.AddSeconds(8);
            while (timedSessionHost.Visible && DateTimeOffset.UtcNow < launchDeadline)
            {
                Application.DoEvents();
                Thread.Sleep(20);
            }
            bool hostClosedAfterEmulatorExit = !timedSessionHost.Visible;
            if (!hostClosedAfterEmulatorExit)
            {
                Process running = (Process)Get(timedSessionHost, "emulator");
                try { if (running != null && !running.HasExited) running.Kill(); }
                catch (InvalidOperationException) { }
                gameHostType.GetField("closing", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(timedSessionHost, true);
                timedSessionHost.Close();
                Application.DoEvents();
            }
            object timedEntry = ((IEnumerable)Call(history, "Load", null, timedSessionRoot)).Cast<object>().Single();
            Assert(hostClosedAfterEmulatorExit && (long)timedEntry.GetType().GetProperty("TotalPlayTimeSeconds").GetValue(timedEntry) >= 1,
                "emulator exit closes the game host and records its elapsed session");
        }
        Assert((string)Call(gameCardType, "FormatPlayTime", null, 7540L) == "2 h 5 min" &&
            (string)Call(gameCardType, "FormatPlayTime", null, 42L) == "menos de 1 min" &&
            (string)Call(gameCardType, "FormatPlayTime", null, 0L) == "ainda não medido", "recent-game play time uses readable hour, minute and legacy states");
        using (var playTimeCard = (Control)Activator.CreateInstance(gameCardType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { coverGame, coverRoot, latestDate, 7540L }, null))
        {
            Assert(playTimeCard.AccessibleDescription.Contains("Tempo total: 2 h 5 min") && playTimeCard.Height == 376, "recent game card announces the total play time and reserves a readable line for it");
            string recentCardPreview = Environment.GetEnvironmentVariable("POKEMONPLAY_RECENT_PLAYTIME_PREVIEW");
            if (!string.IsNullOrWhiteSpace(recentCardPreview))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(recentCardPreview)));
                using var recentPreviewImage = new Bitmap(playTimeCard.Width, playTimeCard.Height);
                playTimeCard.DrawToBitmap(recentPreviewImage, new Rectangle(Point.Empty, recentPreviewImage.Size));
                recentPreviewImage.Save(recentCardPreview, ImageFormat.Png);
            }
        }

        string corruptRoot = Path.Combine(fixture, "corrupt");
        string corruptPath = Path.Combine(corruptRoot, "Settings", "GameLaunchHistory.json");
        Directory.CreateDirectory(Path.GetDirectoryName(corruptPath));
        File.WriteAllText(corruptPath, "{ invalid history");
        Assert(!(bool)Call(history, "TryRecordLaunch", null, corruptRoot, "Emerald", latestDate), "corrupt game history does not block or overwrite data");
        Assert(File.ReadAllText(corruptPath) == "{ invalid history", "corrupt game history remains recoverable");
        Assert(!(bool)Call(history, "TryRecordLaunch", null, fixture, "../outside", latestDate), "history rejects unsafe game titles");

        string clearRoot = Path.Combine(fixture, "clear-history");
        Call(history, "TryRecordLaunch", null, clearRoot, "FireRed", latestDate);
        Assert((bool)Call(history, "Clear", null, clearRoot), "clearing recent history succeeds for a valid local history");
        Assert(!File.Exists(Path.Combine(clearRoot, "Settings", "GameLaunchHistory.json")) && !((IEnumerable)Call(history, "Load", null, clearRoot)).Cast<object>().Any(), "clearing recent history removes saved entries");

        string libraryRoot = Path.Combine(fixture, "library");
        Call(history, "TryRecordLaunch", null, libraryRoot, "Platinum", firstDate);
        Call(history, "TryRecordLaunch", null, libraryRoot, "FireRed", latestDate);
        Type favorites = app.GetType("GameFavoriteService");
        Call(favorites, "Set", null, libraryRoot, "FireRed", true);

        string favoriteFocusRoot = Path.Combine(fixture, "favorite-focus");
        Call(favorites, "Set", null, favoriteFocusRoot, "FireRed", true);
        using (var focusLibrary = (Control)Activator.CreateInstance(app.GetType("LibraryView"), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { null, favoriteFocusRoot }, null))
        using (var focusHost = new Form { Width = 1280, Height = 840 })
        {
            focusHost.Controls.Add(focusLibrary);
            focusLibrary.Dock = DockStyle.Fill;
            focusHost.Show();
            Application.DoEvents();
            Control[] gameCards = Descendants(focusLibrary).OfType<Control>().Where(control => control.GetType().Name == "GameCard").ToArray();
            MethodInfo findNeighbor = gameCards[0].GetType().GetMethod("FindDirectionalGameCard", BindingFlags.Static | BindingFlags.NonPublic);
            Keys[] directions = { Keys.Left, Keys.Right, Keys.Up, Keys.Down };
            var directionTarget = gameCards.SelectMany(card => directions.Select(direction => new { Card = card, Direction = direction, Target = (Control)findNeighbor.Invoke(null, new object[] { card, direction }) }))
                .FirstOrDefault(candidate => candidate.Target != null);
            if (directionTarget == null) throw new Exception("game library has no adjacent card for keyboard navigation");
            directionTarget.Card.Focus();
            var gameCardIsInputKey = directionTarget.Card.GetType().GetMethod("IsInputKey", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(directions.All(direction => (bool)gameCardIsInputKey.Invoke(directionTarget.Card, new object[] { direction })), "focused game cards treat all four arrow keys as navigation input");
            var navigationEvent = new KeyEventArgs(directionTarget.Direction);
            directionTarget.Card.GetType().GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(directionTarget.Card, new object[] { navigationEvent });
            Assert(navigationEvent.Handled && navigationEvent.SuppressKeyPress && directionTarget.Target.Focused, "arrow navigation moves focus to the nearest game card in the requested direction");
            var crossGenerationTarget = gameCards.SelectMany(card => directions.Select(direction => new { Card = card, Direction = direction, Target = (Control)findNeighbor.Invoke(null, new object[] { card, direction }) }))
                .FirstOrDefault(candidate => candidate.Target != null && candidate.Card.Parent != candidate.Target.Parent);
            if (crossGenerationTarget == null) throw new Exception("game library has no adjacent cards across generation sections");
            crossGenerationTarget.Card.Focus();
            var crossGenerationEvent = new KeyEventArgs(crossGenerationTarget.Direction);
            crossGenerationTarget.Card.GetType().GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(crossGenerationTarget.Card, new object[] { crossGenerationEvent });
            Assert(crossGenerationEvent.Handled && crossGenerationTarget.Target.Focused, "arrow navigation continues across game generation sections");
            Control platinumCard = Descendants(focusLibrary).OfType<Control>().Single(control => control.GetType().Name == "GameCard" && CardTitle(control) == "Platinum");
            Button platinumStar = Descendants(platinumCard).OfType<Button>().Single(button => button.AccessibleName == "Adicionar aos favoritos");
            Assert(platinumStar.AccessibleDescription == "Platinum", "favorite button identifies its game to assistive technology before interaction");
            platinumStar.Focus();
            platinumStar.PerformClick();
            Assert(!platinumCard.IsDisposed && platinumStar.Focused && platinumStar.AccessibleName == "Remover dos favoritos" && platinumStar.AccessibleDescription == "Platinum", "changing a favorite outside the favorites filter keeps its game label and keyboard focus in place");

            ((Button)Get(focusLibrary, "favoriteFilter")).PerformClick();
            platinumCard = Descendants(focusLibrary).OfType<Control>().Single(control => control.GetType().Name == "GameCard" && CardTitle(control) == "Platinum");
            Button filteredPlatinumStar = Descendants(platinumCard).OfType<Button>().Single(button => button.AccessibleName == "Remover dos favoritos");
            filteredPlatinumStar.Focus();
            filteredPlatinumStar.PerformClick();
            Control fireRedCard = Descendants(focusLibrary).OfType<Control>().Single(control => control.GetType().Name == "GameCard" && CardTitle(control) == "FireRed");
            Button fireRedStar = Descendants(fireRedCard).OfType<Button>().Single(button => button.AccessibleName == "Remover dos favoritos");
            Assert(!Descendants(focusLibrary).OfType<Control>().Any(control => control.GetType().Name == "GameCard" && CardTitle(control) == "Platinum") && fireRedStar.Focused, "removing a favorite from the filtered view moves focus to the next game card");
            fireRedStar.Focus();
            fireRedStar.PerformClick();
            Button emptyAction = Descendants(focusLibrary).OfType<Button>().Single(button => button.Text == "MOSTRAR TODOS");
            Assert(emptyAction.Focused, "removing the last visible favorite moves focus to the empty-state action");
            focusHost.Controls.Remove(focusLibrary);
        }

        Type libraryType = app.GetType("LibraryView");
        using var library = (Control)Activator.CreateInstance(libraryType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { null, libraryRoot }, null);
        Control searchControl = (Control)Get(library, "search");
        Control systemFilterControl = (Control)Get(library, "systemFilter");
        Assert(searchControl.AccessibleName == "Buscar jogos" && searchControl.AccessibleDescription.Contains("acentuação") &&
            Descendants(searchControl).OfType<TextBox>().Single().AccessibleName == "Buscar jogos", "library search announces its purpose and instructions on both the wrapper and text field");
        Assert(systemFilterControl.AccessibleName == "Filtrar por sistema: Todos os sistemas" &&
            systemFilterControl.AccessibleDescription.Contains("favoritos"), "library system filter exposes its context alongside the selected option");
        systemFilterControl.GetType().GetProperty("SelectedIndex").SetValue(systemFilterControl, 1);
        Assert(systemFilterControl.AccessibleName == "Filtrar por sistema: Game Boy Advance", "library system filter keeps its accessible context as the selected option changes");
        systemFilterControl.GetType().GetProperty("SelectedIndex").SetValue(systemFilterControl, 0);
        Button favoriteFilter = (Button)Get(library, "favoriteFilter");
        Button recentChip = Descendants(library).OfType<Button>().Single(button => button.Text == "Recentes");
        recentChip.PerformClick();
        Assert(((Button)Get(library, "clearRecentButton")).Text == "Limpar recentes", "recent view provides an explicit local history control");
        Assert(Descendants(library).OfType<Label>().Any(label => label.Text == "Jogados recentemente"), "recent library uses a matching section heading");
        Assert(((Label)Get(library, "sortHelper")).Text == "Ordenados pela abertura mais recente", "recent library describes its current sort order");
        string[] recentTitles = Descendants(library).OfType<Control>().Where(control => control.GetType().Name == "GameCard")
            .Select(CardTitle).ToArray();
        Assert(recentTitles.Take(2).SequenceEqual(new[] { "FireRed", "Platinum" }), "library recent view orders games by their successful launch time");
        Control recentFireRedCard = Descendants(library).OfType<Control>().Single(control => control.GetType().Name == "GameCard" && CardTitle(control) == "FireRed");
        Assert(((DateTimeOffset?)Get(recentFireRedCard, "lastPlayedAt")).Value == latestDate, "recent game card exposes the exact latest launch time");

        Call(history, "TryAddPlayTime", null, libraryRoot, "FireRed", TimeSpan.FromMinutes(125));
        Call(history, "TryAddPlayTime", null, libraryRoot, "Platinum", TimeSpan.FromMinutes(35));
        Button mostPlayedChip = Descendants(library).OfType<Button>().Single(button => button.Text == "Mais jogados");
        mostPlayedChip.PerformClick();
        Assert(((Label)Get(library, "sortHelper")).Text == "Ordenados pelo tempo total jogado" &&
            Descendants(library).OfType<Label>().Any(label => label.Text == "Mais jogados · tempo total"), "most-played library view explains its total-time ordering");
        Button clearHistoryButton = (Button)Get(library, "clearRecentButton");
        Assert(clearHistoryButton.Visible && clearHistoryButton.Text == "Limpar histórico" &&
            clearHistoryButton.AccessibleName.Contains("tempos totais jogados"), "most-played library exposes an accessible action to clear recent dates and accumulated play time");
        string[] mostPlayedTitles = Descendants(library).OfType<Control>().Where(control => control.GetType().Name == "GameCard")
            .Select(CardTitle).ToArray();
        Assert(mostPlayedTitles.SequenceEqual(new[] { "FireRed", "Platinum" }), "most-played library view sorts by accumulated session time");
        Control mostPlayedFireRedCard = Descendants(library).OfType<Control>().Single(control => control.GetType().Name == "GameCard" && CardTitle(control) == "FireRed");
        Assert(mostPlayedFireRedCard.AccessibleDescription.Contains("Tempo total: 2 h 5 min"), "most-played cards announce the duration used by the current sort");
        favoriteFilter.PerformClick();
        Assert(Descendants(library).OfType<Control>().Where(control => control.GetType().Name == "GameCard").Select(CardTitle).SequenceEqual(new[] { "FireRed" }),
            "most-played ordering combines with the favorites filter");
        favoriteFilter.PerformClick();
        object mostPlayedSystem = Get(library, "systemFilter");
        var mostPlayedSearch = (Control)Get(library, "search");
        mostPlayedSystem.GetType().GetProperty("SelectedIndex").SetValue(mostPlayedSystem, 2);
        mostPlayedSearch.Text = "plat";
        Assert(Descendants(library).OfType<Control>().Where(control => control.GetType().Name == "GameCard").Select(CardTitle).SequenceEqual(new[] { "Platinum" }),
            "most-played ordering combines with game-system and title search filters");
        mostPlayedSearch.Text = string.Empty;
        mostPlayedSystem.GetType().GetProperty("SelectedIndex").SetValue(mostPlayedSystem, 0);

        Descendants(library).OfType<Button>().Single(button => button.Text == "A–Z").PerformClick();
        Assert(!clearHistoryButton.Visible, "leaving recent and most-played views hides the history-clearing action");
        Assert(Descendants(library).OfType<Label>().Any(label => label.Text == "Todos os jogos · A–Z"), "alphabetical view groups games from every generation together");
        string[] alphabeticalTitles = Descendants(library).OfType<Control>().Where(control => control.GetType().Name == "GameCard")
            .Select(CardTitle).ToArray();
        Assert(alphabeticalTitles.Length > 1 && alphabeticalTitles.SequenceEqual(alphabeticalTitles.OrderBy(title => title, StringComparer.CurrentCultureIgnoreCase)), "alphabetical library view sorts game titles A to Z");
        Assert(((Label)Get(library, "sortHelper")).Text == "Ordenados de A a Z", "alphabetical library describes its current sort order");
        Control alphabeticalFireRedCard = Descendants(library).OfType<Control>().Single(control => control.GetType().Name == "GameCard" && CardTitle(control) == "FireRed");
        Assert(Get(alphabeticalFireRedCard, "lastPlayedAt") == null, "alphabetical game cards omit recent-only launch details");
        favoriteFilter = (Button)Get(library, "favoriteFilter");
        favoriteFilter.PerformClick();
        string[] alphabeticalFavorites = Descendants(library).OfType<Control>().Where(control => control.GetType().Name == "GameCard")
            .Select(CardTitle).ToArray();
        Assert(alphabeticalFavorites.SequenceEqual(new[] { "FireRed" }), "alphabetical library view combines with the favorites filter");
        favoriteFilter.PerformClick();
        recentChip.PerformClick();

        favoriteFilter = (Button)Get(library, "favoriteFilter");
        favoriteFilter.PerformClick();
        string[] favoriteRecent = Descendants(library).OfType<Control>().Where(control => control.GetType().Name == "GameCard")
            .Select(CardTitle).ToArray();
        Assert(favoriteRecent.SequenceEqual(new[] { "FireRed" }), "recent library view combines with the favorites filter");

        using (var interactionHost = new Form { Width = 1280, Height = 840 })
        {
            interactionHost.Controls.Add(library);
            library.Dock = DockStyle.Fill;
            interactionHost.Show();
            Application.DoEvents();
            interactionHost.FormBorderStyle = FormBorderStyle.None;
            interactionHost.Width = 1140;
            Application.DoEvents();
            Control sortHelper = (Control)Get(library, "sortHelper");
            Control clearRecentAction = (Control)Get(library, "clearRecentButton");
            Assert(clearRecentAction.Visible && !sortHelper.Visible,
                "library hides the sort caption when recent-history actions leave insufficient toolbar space");
            for (int clientWidth = interactionHost.ClientSize.Width + 32; !sortHelper.Visible && clientWidth <= 4096; clientWidth += 32)
            {
                interactionHost.ClientSize = new Size(clientWidth, interactionHost.ClientSize.Height);
                Application.DoEvents();
            }
            Assert(sortHelper.Visible && sortHelper.Left >= clearRecentAction.Right + 16,
                "library restores the sort caption with a clear gap when the wide toolbar has room");
            Call(history, "TryAddPlayTime", null, libraryRoot, "FireRed", TimeSpan.FromMinutes(90));
            Call(library.GetType(), "RefreshAfterGameSession", library);
            Application.DoEvents();
            Control refreshedRecentCard = Descendants(library).OfType<Control>().Single(control => control.GetType().Name == "GameCard" && CardTitle(control) == "FireRed");
            Assert(refreshedRecentCard.AccessibleDescription.Contains("Tempo total: 3 h 35 min"), "returning from a paused session refreshes recent cards with newly recorded play time");
            var searchBox = (Control)Get(library, "search");
            searchBox.Text = "no matching game";
            Application.DoEvents();
            var clearRecentButton = (Button)Get(library, "clearRecentButton");
            Assert(clearRecentButton.Visible, "recent clear control stays available when history exists behind an empty filter result");
            Descendants(library).OfType<Button>().Single(button => button.Text == "MOSTRAR TODOS").PerformClick();
            Assert(!clearRecentButton.Visible, "leaving the recent empty state hides its clear-history control");
            interactionHost.Controls.Remove(library);
        }

        string emptyRoot = Path.Combine(fixture, "empty-library");
        using var emptyLibrary = (Control)Activator.CreateInstance(libraryType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { null, emptyRoot }, null);
        Descendants(emptyLibrary).OfType<Button>().Single(button => button.Text == "Mais jogados").PerformClick();
        Assert(Descendants(emptyLibrary).OfType<Label>().Any(label => label.Text == "Ainda sem tempo jogado") &&
            Descendants(emptyLibrary).OfType<Label>().Any(label => label.Text.Contains("encerre o emulador")), "most-played library explains how the first session duration is recorded");
        Descendants(emptyLibrary).OfType<Button>().Single(button => button.Text == "MOSTRAR TODOS").PerformClick();
        Assert(Descendants(emptyLibrary).OfType<Control>().Count(control => control.GetType().Name == "GameCard") == 10, "most-played empty state returns to the full library");
        Descendants(emptyLibrary).OfType<Button>().Single(button => button.Text == "Recentes").PerformClick();
        Assert(Descendants(emptyLibrary).OfType<Label>().Any(label => label.Text == "Nenhum jogo recente"), "recent library filter explains its empty state");
        Descendants(emptyLibrary).OfType<Button>().Single(button => button.Text == "MOSTRAR TODOS").PerformClick();
        Assert(((Label)Get(emptyLibrary, "sortHelper")).Text == "Ordenados por geração de lançamento", "resetting the recent empty state restores the library sort description");
        Assert(Descendants(emptyLibrary).OfType<Control>().Count(control => control.GetType().Name == "GameCard") == 10, "resetting an empty recent view rebuilds and restores all library games");

        string refreshRoot = Path.Combine(fixture, "refresh-library");
        string refreshRoms = Path.Combine(refreshRoot, "Pokemon 3DS - Arquivos", "Roms");
        string refreshAzahar = Path.Combine(refreshRoot, "Pokemon 3DS - Arquivos", "Azahar", "azahar.exe");
        Directory.CreateDirectory(refreshRoms);
        Directory.CreateDirectory(Path.GetDirectoryName(refreshAzahar));
        File.WriteAllText(refreshAzahar, "fixture");
        using (var refreshLibrary = (Control)Activator.CreateInstance(libraryType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { null, refreshRoot }, null))
        using (var refreshHost = new Form { Width = 1000, Height = 760 })
        {
            refreshHost.Controls.Add(refreshLibrary);
            refreshLibrary.Dock = DockStyle.Fill;
            refreshHost.Show();
            Application.DoEvents();
            Button refreshButton = (Button)Get(refreshLibrary, "refreshButton");
            Assert(Descendants(refreshLibrary).OfType<Control>().Count(control => control.GetType().Name == "GameCard") == 10 && refreshButton.Visible && refreshButton.AccessibleName.Contains("F5"), "library exposes a visible, accessible refresh action when 3DS ROM discovery is available");
            string romPath = Path.Combine(refreshRoms, "Pokémon X.3ds");
            File.WriteAllText(romPath, "fixture");
            var refreshProcessCmdKey = libraryType.GetMethod("ProcessCmdKey", BindingFlags.NonPublic | BindingFlags.Instance);
            object[] refreshArguments = { Message.Create(IntPtr.Zero, 0x100, IntPtr.Zero, IntPtr.Zero), Keys.F5 };
            bool refreshShortcutHandled = (bool)refreshProcessCmdKey.Invoke(refreshLibrary, refreshArguments);
            string[] refreshedTitles = Descendants(refreshLibrary).OfType<Control>().Where(control => control.GetType().Name == "GameCard").Select(CardTitle).ToArray();
            var refreshedBanner = Get(refreshLibrary, "heroBanner");
            Assert(refreshShortcutHandled && refreshedTitles.Contains("POKÉMON X") && (int)refreshedBanner.GetType().GetProperty("GameCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(refreshedBanner) == 11, "F5 discovers a newly added 3DS game and updates the library count");
            ((Control)Get(refreshLibrary, "search")).Text = "pokemon x";
            File.Delete(romPath);
            refreshButton.PerformClick();
            Assert(((Control)Get(refreshLibrary, "search")).Text == "pokemon x" && !Descendants(refreshLibrary).OfType<Control>().Any(control => control.GetType().Name == "GameCard" && CardTitle(control) == "POKÉMON X") && (int)refreshedBanner.GetType().GetProperty("GameCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(refreshedBanner) == 10, "refresh button removes a deleted ROM while preserving the active search");
            refreshHost.FormBorderStyle = FormBorderStyle.None;
            refreshHost.Width = 762;
            Application.DoEvents();
            var chipStrip = (Control)Get(refreshLibrary, "chips");
            Button[] generationChips = chipStrip.Controls.OfType<Button>().Where(button => button != refreshButton).ToArray();
            Assert(refreshButton.Visible && refreshButton.Right <= chipStrip.ClientSize.Width && generationChips.All(button => !button.Bounds.IntersectsWith(refreshButton.Bounds)), "refresh action fits beside generation filters at compact width");
            Assert(generationChips.All(button => button.Width >= 92 && button.Right <= chipStrip.ClientSize.Width) && generationChips.Select(button => button.Top).Distinct().Count() == 2, "generation filters wrap into readable rows beside refresh at the minimum library width");
            refreshHost.Width = 720;
            Application.DoEvents();
            Assert(!refreshButton.Visible && refreshButton.AccessibleName.Contains("F5") && generationChips.All(button => button.Right <= chipStrip.ClientSize.Width && button.Width >= 92), "compact layout keeps generation chips clear and F5 refresh available");
            refreshHost.Controls.Remove(refreshLibrary);
        }

        string accentedRoot = Path.Combine(fixture, "accented-games");
        string roms = Path.Combine(accentedRoot, "Pokemon 3DS - Arquivos", "Roms");
        string azahar = Path.Combine(accentedRoot, "Pokemon 3DS - Arquivos", "Azahar");
        Directory.CreateDirectory(roms);
        Directory.CreateDirectory(azahar);
        File.WriteAllText(Path.Combine(azahar, "azahar.exe"), "fixture");
        File.WriteAllText(Path.Combine(roms, "Pokémon X.3ds"), "fixture");
        using var accentedLibrary = (Control)Activator.CreateInstance(libraryType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { null, accentedRoot }, null);
        ((Control)Get(accentedLibrary, "search")).Text = "pokemon x";
        string[] accentlessSearchResults = Descendants(accentedLibrary).OfType<Control>().Where(control => control.GetType().Name == "GameCard")
            .Select(CardTitle).ToArray();
        Assert(accentlessSearchResults.SequenceEqual(new[] { "POKÉMON X" }), "game search ignores accents in ROM titles when the query omits them");
        ((Control)Get(accentedLibrary, "search")).Text = "Pokémon X";
        Assert(Descendants(accentedLibrary).OfType<Control>().Any(control => control.GetType().Name == "GameCard" && CardTitle(control) == "POKÉMON X"), "game search still matches an accented query exactly");

        using (var keyboardHost = new Form { Width = 1000, Height = 760 })
        {
            keyboardHost.Controls.Add(library);
            library.Dock = DockStyle.Fill;
            keyboardHost.Show();
            Application.DoEvents();
            Descendants(library).OfType<Button>().Single(button => button.Text == "A–Z").Focus();
            var processCmdKey = libraryType.GetMethod("ProcessCmdKey", BindingFlags.NonPublic | BindingFlags.Instance);
            object[] commandArguments = { Message.Create(IntPtr.Zero, 0x100, IntPtr.Zero, IntPtr.Zero), Keys.Control | Keys.F };
            bool shortcutHandled = (bool)processCmdKey.Invoke(library, commandArguments);
            Assert(shortcutHandled && ((Control)Get(library, "search")).ContainsFocus, "Ctrl+F focuses the library search field");
            var searchBox = (Control)Get(library, "search");
            var systemFilter = Get(library, "systemFilter");
            systemFilter.GetType().GetProperty("SelectedIndex").SetValue(systemFilter, 2);
            searchBox.Text = "platinum";
            Assert(Descendants(library).OfType<Control>().Count(control => control.GetType().Name == "GameCard") == 1, "library search narrows results within the active system filter");
            object[] escapeArguments = { Message.Create(IntPtr.Zero, 0x100, IntPtr.Zero, IntPtr.Zero), Keys.Escape };
            bool escapeHandled = (bool)processCmdKey.Invoke(library, escapeArguments);
            Assert(escapeHandled && searchBox.Text.Length == 0 && searchBox.ContainsFocus && (int)systemFilter.GetType().GetProperty("SelectedIndex").GetValue(systemFilter) == 2 && Descendants(library).OfType<Control>().Count(control => control.GetType().Name == "GameCard") == 7, "Escape clears the library query while preserving the active system filter and focus");
            systemFilter.GetType().GetProperty("SelectedIndex").SetValue(systemFilter, 0);
            keyboardHost.Controls.Remove(library);
        }

        string preview = Environment.GetEnvironmentVariable("POKEMONPLAY_LIBRARY_RECENT_PREVIEW");
        if (!string.IsNullOrWhiteSpace(preview))
        {
            Directory.CreateDirectory(preview);
            using var host = new Form { Width = 1280, Height = 840, BackColor = System.Drawing.Color.FromArgb(10, 16, 38) };
            recentChip.PerformClick();
            favoriteFilter.PerformClick();
            host.Controls.Add(library);
            library.Dock = DockStyle.Fill;
            favoriteFilter.PerformClick();
            host.Show();
            Application.DoEvents();
            using var bitmap = new System.Drawing.Bitmap(host.Width, host.Height);
            host.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.Save(Path.Combine(preview, "library-recent.png"));
            favoriteFilter.PerformClick();
            Descendants(library).OfType<Button>().Single(button => button.Text == "A–Z").PerformClick();
            Application.DoEvents();
            using var alphabeticalBitmap = new System.Drawing.Bitmap(host.Width, host.Height);
            host.DrawToBitmap(alphabeticalBitmap, new System.Drawing.Rectangle(0, 0, alphabeticalBitmap.Width, alphabeticalBitmap.Height));
            alphabeticalBitmap.Save(Path.Combine(preview, "library-alphabetical.png"));
            mostPlayedChip.PerformClick();
            Application.DoEvents();
            using var mostPlayedBitmap = new System.Drawing.Bitmap(host.Width, host.Height);
            host.DrawToBitmap(mostPlayedBitmap, new System.Drawing.Rectangle(0, 0, mostPlayedBitmap.Width, mostPlayedBitmap.Height));
            mostPlayedBitmap.Save(Path.Combine(preview, "library-most-played.png"));
            recentChip.PerformClick();
            favoriteFilter.PerformClick();
            host.Controls.Remove(library);
            host.Close();
            using var compactHost = new Form { Width = 762, Height = 840, FormBorderStyle = FormBorderStyle.None, BackColor = System.Drawing.Color.FromArgb(10, 16, 38) };
            compactHost.Controls.Add(library);
            library.Dock = DockStyle.Fill;
            compactHost.Show();
            Application.DoEvents();
            Control[] compactToolbarControls = { (Control)Get(library, "search"), (Control)Get(library, "systemFilter"), (Control)Get(library, "favoriteFilter"), (Control)Get(library, "clearRecentButton") };
            Assert(compactToolbarControls.All(control => control.Right <= control.Parent.ClientSize.Width), "recent toolbar controls fit the minimum-width content area");
            Control chipPanel = (Control)Get(library, "chips");
            Assert(chipPanel.Controls.Cast<Control>().Where(control => control.Visible).All(control => control.Left >= 0 && control.Top >= 0 && control.Right <= chipPanel.ClientSize.Width && control.Bottom <= chipPanel.ClientSize.Height),
                "generation, recent and play-time chips wrap without clipping at the minimum width");
            using var compactBitmap = new System.Drawing.Bitmap(compactHost.Width, compactHost.Height);
            compactHost.DrawToBitmap(compactBitmap, new System.Drawing.Rectangle(0, 0, compactBitmap.Width, compactBitmap.Height));
            compactBitmap.Save(Path.Combine(preview, "library-recent-compact.png"));
            compactHost.Controls.Remove(library);
            compactHost.Close();
        }
    }

    private sealed class GameInfoProxy
    {
        internal object Value { get; }
        internal string Title => (string)Value.GetType().GetField("Title").GetValue(Value);
        internal string Cover => (string)Value.GetType().GetField("Cover").GetValue(Value);
        internal string SaveFolderName => (string)Value.GetType().GetField("SaveFolderName").GetValue(Value);
        internal string Arguments => (string)Value.GetType().GetField("Arguments").GetValue(Value);
        internal GameInfoProxy(object value) => Value = value;
    }
}
