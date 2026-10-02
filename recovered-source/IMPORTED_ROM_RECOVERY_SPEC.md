# Ciclo: recuperar ROMs movidas

## Problema

O catálogo conserva entradas cujo arquivo desapareceu, mas a Biblioteca esconde essas entradas. Importar novamente depois de mover a ROM cria outra identidade e outra pasta de save. O usuário precisa reencontrar o arquivo sem recriar o jogo.

## Aceitação

- Adicionar jogos oferece gerenciamento dos jogos importados, inclusive os não encontrados. A lista identifica título, sistema, disponibilidade e caminho.
- Localizar ROM exige confirmação de que é o mesmo jogo e preserva Id, título, base, selo de hack e pasta de save.
- Neste ciclo, aceitar somente o mesmo nome de arquivo e extensão no novo local. Renomear o arquivo exige uma migração de saves que será estudada separadamente; explicar a restrição antes de salvar.
- Rejeitar arquivo ausente, incompatível, já associado a outro jogo ou inacessível antes de alterar o catálogo. Não copiar, mover ou remover ROMs e saves.
- Atualizar a biblioteca após a recuperação. Catálogo inválido deve produzir uma mensagem acionável no gerenciamento e não impedir a abertura da biblioteca.
- Verificar round-trip, identidade/pasta de save, preservação do catálogo nos erros, estados da lista, layout e regressões da suíte Windows. Publicar somente após aprovação desses checks.

## Plano

1. Operação de catálogo com gravação atômica e validações prévias.
2. Janela de gerenciamento seguindo os diálogos Pixel existentes, com seleção, caminho legível e ação Localizar ROM.
3. Integração ao menu e atualização da biblioteca.
4. Testes, evidências, PR e release. Reutilizar o checkout/cache; verificar downloads em fluxo e não criar novas cópias dos ZIPs no disco.

## Resultado verificado

O [run 37045567505](https://github.com/Juanblack1/pokemonplay/actions/runs/37045567505) aprovou `3c2e0c65fdf598545003e18e2b9c10baf4ecb411`, incluindo os 13 casos novos, a suíte completa, o empacotamento Windows e as 79 verificações de regras da nuvem. O PR #14 foi integrado em `d6585959baec18190b649b0adfed0093b0a9da5c`. A configuração/catálogo permaneceu intacta nos erros; os bytes de ROM e save foram preservados no sucesso. Os controles passaram pelas verificações geométricas em 620 px. Não houve teste com emulador ou leitor de tela reais. O CI removeu suas fixtures ao terminar; o checkout e os caches locais foram reutilizados.
