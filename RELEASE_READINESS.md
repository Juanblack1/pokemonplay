# Prontidão para distribuição pública

**Estado da auditoria (2026-10-02): não tornar o repositório público ainda.** O repositório `Juanblack1/pokemonplay` está privado. A release v171.1.0 é a versão mais recente; os workflows Windows de PR e release passaram (runs `36955572832` e `36955753712`).

## Pronto para distribuição

- A v171.1.0 fornece `pokemon-play-win-x64-portable.zip` (62.393.080 bytes; SHA-256 `0beb411e25fd4a120a94bfca7576f5ae0d4eff0d0fb74a991748c5b6980c1787`) para primeira instalação e `pokemon-play-win-x64-update.zip` (62.390.410 bytes; SHA-256 `433aa0ce0caa0d552777021422f779ee5a601579691f128f9edc2cb3ac53d833`) para o atualizador.
- O pacote atual é compilado de forma reproduzível no workflow Windows, verifica a ausência de marcadores do segredo OAuth no assembly principal e inclui sidecars SHA-256.
- O código novo usa OAuth com PKCE e não envia `client_secret`. Jogos, ROMs, BIOS e emuladores de terceiros não são empacotados.
- A autenticação GitHub disponível permite publicar no repositório, mas o token não é conhecido nem deve ser copiado para arquivos ou para o chat. O workflow de release usa o `GITHUB_TOKEN` temporário do Actions.

## Bloqueios antes de abrir o repositório

1. **Asset legado v1.0.0:** a release ainda contém `Pokemons.Play.exe` (837.331.686 bytes; SHA-256 `26e450375e75b229f5c97855765987b1cc8e1aaa9f78f5a6bd77ea7735f5aa15`). O hash coincide com o bundle descrito em `recovered-source/README.md`; `recovered-source/tools/extract_payload.py` declara que o bundle contém imagens ROM protegidas e omite esses arquivos durante a extração. Remover esse asset da release antes de mudar a visibilidade do repositório.
2. **Segredo OAuth legado:** versões anteriores do bundle distribuíram um `client_secret`. A sessão autenticada no Google Cloud confirmou o cliente **Pokemons Play para Windows** e seu tipo **Computador**, correspondente ao `client_id` do código. A v171.1.0 usa PKCE e envia apenas `client_id` e `code_verifier`; não depende de segredo. A chave antiga ainda precisa ser desativada no Console pelo proprietário antes da abertura pública. Não exclua o cliente OAuth, pois isso também invalidaria o ID usado pelo fluxo atual. A rotação ainda não foi executada.
3. **Regras do Firestore:** confirmar no projeto Firebase que as regras do servidor limitam leitura e gravação ao UID autenticado. O código organiza documentos por UID, mas as regras implantadas não estão neste repositório. A sessão autenticada recebeu do Firebase Console a mensagem de que o projeto não existe ou a conta não tem permissão para listar apps; a lista de bancos no Cloud Console retornou erro do servidor sem linhas. Portanto, as regras continuam sem verificação. Manter sincronização na nuvem indisponível para uma distribuição pública até essa confirmação; o uso local do launcher não depende dela.

Depois de resolver e verificar os três itens, revisar a lista de assets, tornar o repositório público e testar o download da release sem autenticação em uma sessão anônima.
