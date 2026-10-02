# Ciclo: primeiro jogo local

## Problema e referência

A distribuição pública não inclui ROMs ou emuladores. O catálogo inicial ainda apresenta títulos conhecidos, mas não explica o próximo passo. O importador oferece jogos-base de consoles incompatíveis e usa o cabeçalho como nome, mesmo quando uma hack conserva o cabeçalho original.

Referência estudada: [configuração de emuladores do Playnite](https://api.playnite.link/docs/manual/features/emulationSupport/addingNewEmulators.html). Aplicação ao Pokemon Play: explicar importação e configuração no contexto da biblioteca, sem exigir conta ou um assistente inicial obrigatório.

## Requisitos e aceitação

- Biblioteca sem ROM importada disponível e sem executável de jogo local: mostrar uma orientação com acesso ao menu de arquivos/pasta; explicar emulador e login opcional. A ação funciona por mouse e teclado.
- Não mostrar essa orientação sobre resultados filtrados, favoritos ou histórico, nem para instalações com jogos locais disponíveis. Preservar os cartões e fluxos legados.
- Importador GBA: três bases GBA. DS: sete bases DS. 3DS: oito bases 3DS. Manter a base reconhecida quando compatível e a validação antes de salvar.
- Nome sugerido vem do arquivo, preservando nomes de hacks e hífens. Edição mantém o nome salvo; campo limita a 80 caracteres. A ação de edição deve dizer Salvar.
- Confirmação de conteúdo Pokémon continua obrigatória para 3DS e arquivos não reconhecidos.
- ROMs e saves não são copiados nem alterados por essa orientação.
- Nome de save de ROM importada deriva do arquivo escolhido mesmo quando o título coincide com FireRed ou outro título legado; cobrir `.sav`, `.srm` e seleção contextual de extensão.

## Verificação

`FirstUseCheck` cobre bases, nomes, confirmação, acessibilidade, estados da biblioteca e limites dos controles em 1000 px. A suíte completa ProfilesCheck e o empacotamento do workflow Windows verificam integração. Layout geométrico é evidência parcial: não substitui inspeção visual com emulador real.

Evidência de 2026-10-02: [run 37043170957](https://github.com/Juanblack1/pokemonplay/actions/runs/37043170957), ambos os jobs aprovados para `e8ee7d8717d575c0610659a2654ab69ccbe2758b`. As 19 verificações novas passaram; a suíte completa terminou com `ALL CHECKS PASSED; temporary fixtures removed.` O PR #12 foi integrado em `693e0dfaf360d859dec497f4c717df679fef3e2f`. O build local também passou, com um aviso preexistente CS0108. A política deste computador continua impedindo a inspeção do aplicativo não assinado; os testes Windows foram executados no runner GitHub.

## Próximos ciclos candidatos

- Diagnóstico acionável de emuladores ausentes ao iniciar uma ROM.
- Recuperação de ROM movida sem perder a identidade do save.
- Prévia e resultado agregado para importações grandes.

Esses itens precisam de investigação própria; não fazem parte da conclusão deste ciclo.
