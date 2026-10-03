using System;
using System.IO;
using System.Text.Json;

internal sealed record GameProfilePresentation(string Label,string Problem)
{
    internal static GameProfilePresentation Read(string root,GameInfo game)
    {
        if(game.Generation>=6)return new(game.IsImported?"Jogar · Padrão":"Jogar",null);
        try {
            var profiles=SaveProfileService.Load(root,game.SaveFolderName);
            var active=profiles.Profiles.Find(profile=>profile.Id==profiles.ActiveId);
            return new(game.IsImported?"Jogar · Padrão":"Jogar · "+active.Name,null);
        }catch(Exception error) when(error is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or System.Security.SecurityException) {
            return new("Verificar perfis","Não foi possível ler os perfis deste jogo. Confira Settings/SaveProfiles/"+game.SaveFolderName+".json. Os arquivos de perfil e saves foram preservados.");
        }
    }
}
