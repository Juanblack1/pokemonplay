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
