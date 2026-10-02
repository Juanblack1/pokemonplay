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
