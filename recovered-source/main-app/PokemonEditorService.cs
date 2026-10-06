using System;
using System.Collections.Generic;
using System.Linq;
using PKHeX.Core;

internal static class PokemonEditorService
{
    internal static GameVersion[] Versions(int format)
    {
        // Concrete games only; group versions (FRLG, HGSS, etc.) are not encounter origins.
        var games = new (int Generation, string Names)[] {
            (3,"R S E FR LG"),(4,"D P Pt HG SS"),(5,"B W B2 W2"),
            (6,"X Y AS OR"),(7,"SN MN US UM"),(8,"SW SH BD SP PLA"),(9,"SL VL") };
        return games.Where(g=>g.Generation<=format).SelectMany(g=>g.Names.Split(' '))
            .Select(name=>Enum.Parse<GameVersion>(name)).ToArray();
    }

    internal static PKM Blank(int format, SaveFile save = null, GameVersion? version = null)
    {
        PKM pk = save?.BlankPKM.Clone() ?? format switch {
            3=>new PK3(),4=>new PK4(),5=>new PK5(),6=>new PK6(),7=>new PK7(),8=>new PK8(),9=>new PK9(),
            _=>throw new ArgumentOutOfRangeException(nameof(format)) };
        if(save!=null)EntityTemplates.TemplateFields(pk,save);
        pk.Version = version ?? (save != null && Versions(format).Contains(save.Version) ? save.Version : (format switch {3=>GameVersion.FR,4=>GameVersion.Pt,5=>GameVersion.B,6=>GameVersion.X,7=>GameVersion.SN,8=>GameVersion.SW,_=>GameVersion.SL}));
        if(pk.Language==0)pk.Language=2;
        if(string.IsNullOrWhiteSpace(pk.OriginalTrainerName))pk.OriginalTrainerName="PLAYER";
        pk.Species=1;pk.CurrentLevel=5;pk.MetLevel=5;pk.Ball=4;
        pk.Nickname=SpeciesName.GetSpeciesNameGeneration(pk.Species,pk.Language,(byte)pk.Format);
        pk.Gender=pk.GetSaneGender();pk.RefreshChecksum();return pk;
    }

    internal static ITrainerInfo Trainer(PKM pk, SaveFile save = null)
        => save != null && save.Generation == pk.Format ? new SimpleTrainerInfo(save,pk.Version)
        : new SimpleTrainerInfo(pk.Version) {OT=pk.OriginalTrainerName,TID16=pk.TID16,SID16=pk.SID16,Gender=pk.OriginalTrainerGender,Language=pk.Language};

    internal static IReadOnlyList<IEncounterable> Encounters(PKM draft, SaveFile save = null)
    {
        PKM probe=draft.Clone();
        return EncounterMovesetGenerator.GenerateEncounters(probe,Trainer(probe,save),ReadOnlyMemory<ushort>.Empty,new[]{probe.Version})
            .Where(e=>e.Species==probe.Species && e.Form==probe.Form && e.Version.Contains(probe.Version)).Take(500).ToArray();
    }

    internal static PKM FromEncounter(IEncounterable encounter, PKM draft, SaveFile save = null)
    {
        PKM generated=encounter.ConvertToPKM(Trainer(draft,save),EncounterCriteria.Unrestricted);
        if(encounter.Version.Contains(draft.Version))generated.Version=draft.Version;
        if(generated.GetType()!=draft.GetType())
            generated=EntityConverter.ConvertToType(generated,draft.GetType(),out _) ?? throw new InvalidOperationException("Este encontro não pode ser convertido para o formato selecionado.");
        generated.RefreshChecksum();return generated;
    }
}

