# Pokémon Play

Um launcher Windows para organizar jogos Pokémon, saves, perfis, controles, banco Pokémon e integrações com emuladores.

## Baixar e abrir

1. Abra [Releases](https://github.com/Juanblack1/pokemonplay/releases/latest) e baixe `pokemon-play-win-x64-portable.zip`.
2. Extraia o ZIP completo em uma pasta gravável no computador.
3. Abra `Pokemons Play.exe` na raiz da pasta extraída.

O pacote portátil inclui um iniciador simples e o runtime do aplicativo; não exige instalação separada do .NET. As atualizações disponíveis aparecem no próprio launcher e preservam as pastas de saves. `pokemon-play-win-x64-update.zip` é destinado ao atualizador do aplicativo.

Os novos pacotes incluem **RetroArch com mGBA (GBA), melonDS DS (Nintendo DS) e Azahar (Nintendo 3DS)**. Em uma instalação nova, os emuladores são encontrados automaticamente: não é necessário instalar o RetroArch nem baixar cores. Instalações legadas e escolhas explícitas de emuladores externos são preservadas. Fontes correspondentes e licenças estão no asset `pokemon-play-emulator-sources.zip` da mesma release.

O launcher não inclui jogos, ROMs, BIOS, firmware ou chaves de console. Para jogar, abra **Biblioteca → Adicionar jogos**, selecione suas ROMs ou uma pasta, confira o jogo-base e confirme. O Pokemon Play guarda o caminho e os metadados; não copia nem envia os arquivos. Para uma hack ROM, marque **Esta é uma hack ROM de Pokémon**: o cartão mostra o nome escolhido, o selo **HACK ROM** e “Hack de [jogo-base]”. Clique no cartão importado para jogar. 3DS exige arquivo/formato e eventuais dados de console compatíveis com o Azahar; incluir o emulador não elimina essas exigências. O login Google é opcional.

Para conquistas opcionais, abra **Configurações → RetroAchievements → Abrir RetroArch · configurar conta**, ative Conquistas no RetroArch e informe sua conta RetroAchievements. A configuração fica em `Settings/Emulators/RetroArch`; saves continuam no perfil do jogo. O Azahar mantém seus dados no diretório padrão do usuário, fora do runtime substituído nas atualizações.

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
