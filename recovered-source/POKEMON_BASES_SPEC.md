# Ciclo: completar opções de jogos-base

## Problema

A importação aceita GBA e Nintendo DS, mas não oferece Ruby, Sapphire, Diamond e Pearl como jogo-base. Isso obriga a classificar esses jogos e suas hacks como outro título, gerando metadados e referência de capa incorretos.

## Aceitação

- Incluir Ruby/Sapphire somente no seletor GBA e Diamond/Pearl somente no seletor DS.
- Identificar esses nomes quando disponíveis no cabeçalho ou nome de arquivo; manter confirmação manual existente e nomes de hacks.
- Priorizar Omega Ruby e Alpha Sapphire antes dos nomes curtos, para não confundir as versões 3DS com as versões GBA.
- Referenciar capas pelo jogo-base quando o usuário possui a imagem; manter placeholder quando a capa está ausente, sem distribuir novas imagens ou ROMs.
- Verificar os quatro nomes, consoles, seletores, referência de capa, ausência de capa e regressões da suíte Windows. Publicar após os checks.
- Preservar dados locais e reutilizar o checkout e caches do ciclo anterior.

## Resultado verificado

O [run 37046587904](https://github.com/Juanblack1/pokemonplay/actions/runs/37046587904) aprovou `543e6e954f7117f60d24803a7216a13662731151`, com 24 novas verificações, suíte completa, empacotamento e Firestore. O PR #15 foi integrado em `bd00b90d196cebbe495bce48173e9e3a987b0a13`. Os casos 3DS com underscores preservaram Omega Ruby/Alpha Sapphire corretamente. Os cartões foram desenhados com ausência de capa sem falha; isso comprova o fallback, não a presença de imagens na instalação do usuário. As fixtures foram removidas pelo runner.
