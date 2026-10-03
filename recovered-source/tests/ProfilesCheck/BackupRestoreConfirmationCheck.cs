using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class BackupRestoreConfirmationCheck
{
    internal static void Run(string root)
    {
        string fixture = Path.Combine(root, "backup-confirmation-fixture");
        Directory.CreateDirectory(fixture);
        string prefix = "Pokemon Platinum - AAAA";
        string suffix = "20261003-120000.zip";
        string[] names = { prefix + "branch-one-" + suffix, prefix + "branch-two-" + suffix, "manual.zip" };
        byte[] payload = { 0, 1, 127, 128, 254, 255, 42 };
        DateTime modified = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Local);
        string[] paths = names.Select(name => Path.Combine(fixture, name)).ToArray();
        foreach (string path in paths) { File.WriteAllBytes(path, payload); File.SetLastWriteTime(path, modified); }
        DateTime[] originalTimes = paths.Select(File.GetLastWriteTimeUtc).ToArray();
        var failures = new List<string>();
        void Verify(bool condition, string description)
        {
            if (condition) Console.WriteLine("PASS " + description);
            else { failures.Add(description); Console.WriteLine("FAIL " + description); }
        }
        try
        {
            Assembly app = Assembly.Load("Pokemons Play");
            Type choiceType = app.GetType("BackupArchiveChoice", throwOnError: true);
            Type dialogType = app.GetType("BackupRestoreDialog", throwOnError: true);
            MethodInfo builder = dialogType.GetMethod("BuildRestoreConfirmation", BindingFlags.Static | BindingFlags.NonPublic);
            object[] choices = paths.Select(path => Activator.CreateInstance(choiceType, new object[] { path })).ToArray();
            string[] summaries = choices.Select(choice => choice.ToString()).ToArray();
            Verify(names[0] != names[1] && names[0].Length > 42 && names[1].Length > 42 &&
                names[0].Substring(0, 23) == names[1].Substring(0, 23) &&
                names[0].Substring(names[0].Length - 18) == names[1].Substring(names[1].Length - 18),
                "backup confirmation fixture has distinct long names differing only in the omitted middle");
            Verify(summaries[0] == summaries[1] && !summaries[0].Contains(names[0], StringComparison.Ordinal),
                "actual backup list summaries collide for equal size and modification minute");
            const string profile = "Treinador de teste";
            string[] confirmations = choices.Select(choice => (string)builder.Invoke(null, new object[] { profile, choice })).ToArray();
            Verify(confirmations[0] != confirmations[1], "restore confirmations distinguish backups with colliding list summaries");
            for (int index = 0; index < names.Length; index++)
            {
                Verify(confirmations[index].Contains(names[index], StringComparison.Ordinal),
                    "restore confirmation includes complete selected basename " + index);
                Verify(confirmations[index].Contains(summaries[index], StringComparison.Ordinal) &&
                    confirmations[index].Contains(profile, StringComparison.Ordinal) &&
                    confirmations[index].Contains("substituirá os arquivos locais", StringComparison.Ordinal) &&
                    confirmations[index].Contains("estado atual será guardado em Backups/Automaticos", StringComparison.Ordinal),
                    "restore confirmation retains backup metadata, target profile, replacement and automatic safety warning " + index);
            }
        }
        finally
        {
            for (int index = 0; index < paths.Length; index++)
                Verify(File.Exists(paths[index]) && File.ReadAllBytes(paths[index]).SequenceEqual(payload) &&
                    File.GetLastWriteTimeUtc(paths[index]) == originalTimes[index],
                    "confirmation leaves owned input fixture bytes and modification time unchanged " + index);
        }
        if (failures.Count > 0) throw new Exception("Backup restore confirmation checks failed: " + string.Join("; ", failures));
        Console.WriteLine("ALL BACKUP RESTORE CONFIRMATION CHECKS PASSED");
    }
}
