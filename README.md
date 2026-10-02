# Pokémon Play

Um launcher Windows para organizar jogos Pokémon, saves, perfis, controles, banco Pokémon e integrações com emuladores.

## Baixar e abrir

1. Abra [Releases](https://github.com/Juanblack1/pokemonplay/releases/latest) e baixe `pokemon-play-win-x64-portable.zip`.
2. Extraia o ZIP completo em uma pasta gravável no computador.
3. Abra `Pokemons Play.exe` na raiz da pasta extraída.

O pacote portátil inclui um iniciador simples e o runtime do aplicativo; não exige instalação separada do .NET. As atualizações disponíveis aparecem no próprio launcher e preservam as pastas de saves. `pokemon-play-win-x64-update.zip` é destinado ao atualizador do aplicativo.

O launcher não inclui jogos, ROMs, BIOS ou emuladores de terceiros. Para jogar títulos GBA, Nintendo DS e Nintendo 3DS, abra **Biblioteca → Adicionar jogos** e selecione ROMs ou uma pasta. O Pokemon Play guarda o caminho e os metadados no perfil local; não copia nem envia os arquivos. Cabeçalhos GBA/DS com Pokémon ajudam a preencher título e jogo-base; arquivos 3DS e títulos sem identificação pedem confirmação de que são Pokémon. Para uma hack ROM, marque **Esta é uma hack ROM de Pokémon**: o cartão mostra o nome escolhido, o selo **HACK ROM** e “Hack de [jogo-base]”, usando a capa do jogo-base como referência. A execução depende de um emulador compatível configurado na pasta da biblioteca. O login Google é opcional e só é necessário para usar os recursos de cópia na nuvem.

## Requisitos

- Windows 10 ou 11, 64 bits.
- Acesso à internet para consultar atualizações e, opcionalmente, usar os recursos de nuvem.

## Compilar o código-fonte

Instale o SDK .NET 10 e execute:

```powershell
dotnet build "recovered-source/main-app/Pokemons Play.csproj" -c Release
dotnet run --configuration Release --project "recovered-source/tests/ProfilesCheck/Check.csproj"
```

O pacote autocontido publicado é criado pelo workflow de release a partir de uma tag estável `v*`.
