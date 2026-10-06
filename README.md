# Pokémon Play

Um launcher Windows para organizar jogos Pokémon, saves, perfis, controles, banco Pokémon e integrações com emuladores.

Copyright (C) 2026 Juanblack1 e colaboradores. O código próprio é distribuído
sob [GNU GPL versão 3](LICENSE), sem garantia. Componentes, imagens e marcas de
terceiros conservam seus direitos e licenças; a licença do projeto não concede
direitos sobre ROMs ou conteúdo Pokémon de terceiros.

## Code signing policy

A candidatura à assinatura gratuita da SignPath Foundation está em preparação.
O projeto ainda não foi aprovado e os downloads v172.0.5 continuam sem assinatura.
Consulte a [política de assinatura](CODE_SIGNING.md), a [privacidade](PRIVACY.md)
e a [preparação da candidatura](docs/signpath-application.md).

## Baixar e abrir

1. Abra [Releases](https://github.com/Juanblack1/pokemonplay/releases/latest) e baixe `pokemon-play-win-x64-setup.exe`.
2. Execute o instalador e siga as instruções. A instalação é para o usuário atual e não exige administrador.
3. Abra Pokémon Play pelo menu Iniciar ou pelo atalho opcional na área de trabalho.

O instalador coloca o aplicativo em `%LOCALAPPDATA%\Programs\PokemonPlay`. As próximas versões chegam pelo próprio launcher: não é necessário reinstalar. Saves, perfis, Banco Pokémon, configurações e backups são preservados em atualizações e reinstalações. A desinstalação remove o aplicativo e os atalhos, mantendo os dados nessa pasta. Para remover os dados definitivamente, confira e copie seu progresso antes de apagar a pasta manualmente. Feche o aplicativo e seus emuladores antes de instalar ou desinstalar; um instalador antigo não substitui uma versão mais recente.

Prefere usar sem instalar? Baixe `pokemon-play-win-x64-portable.zip`, extraia o ZIP completo em uma pasta gravável e abra `Pokemons Play.exe` na raiz. Instalações portáteis anteriores permanecem na pasta escolhida e continuam atualizando; o instalador não migra os dados de outra pasta automaticamente.

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
