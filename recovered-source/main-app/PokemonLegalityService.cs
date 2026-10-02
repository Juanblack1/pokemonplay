using System;
using System.Linq;
using PKHeX.Core;

internal sealed record PokemonLegalityResult(bool IsConsistent, int IssueCount, string Report);

internal static class PokemonLegalityService
{
    internal static PokemonLegalityResult Analyze(PKM pokemon)
    {
        if (pokemon == null) throw new ArgumentNullException(nameof(pokemon));
        if (pokemon.Species == 0) throw new InvalidOperationException("Selecione um Pokémon válido antes da análise.");

        var analysis = new LegalityAnalysis(pokemon, StorageSlotType.Box);
        string report = LegalityFormatting.Report(analysis, "en", verbose: false);
        int issues = analysis.Results.Count(result => !result.Valid);
        return new PokemonLegalityResult(issues == 0, issues, report);
    }
}
