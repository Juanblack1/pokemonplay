# Confirmar antes de substituir preferências ilegíveis

## Problema e resultado

O ciclo anterior passou a avisar quando `input-presets.txt` não pode ser lido, mas o botão Salvar ainda poderia substituir o conteúdo desconhecido com valores padrão. A tela deve pedir uma confirmação explícita antes de qualquer gravação nessa condição.

## Requisitos e aceitação

- R1: Depois de uma falha de leitura, Salvar pergunta se o usuário quer substituir o arquivo inacessível pelos valores atualmente mostrados. A opção padrão é Não.
- R2: Cancelar mantém o arquivo original byte a byte e explica que nada foi substituído.
- R3: Confirmar permite salvar preferências e perfis normalmente.
- R4: Depois de uma gravação bem-sucedida, as gravações seguintes não repetem a confirmação.
- R5: Arquivos ausentes e arquivos legados legíveis continuam salvando sem confirmação.
- R6: A atualização pública v2.0.0 usa uma tag técnica crescente (v172.0.0) para alcançar os binários antigos da linha v171; o código novo mantém a ordenação da linha 2.x. Testar o serviço novo com uma versão antiga como parâmetro não prova que o binário antigo entende a tag v2.0.0.

## Plano de implementação

1. Marcar a falha de leitura no `SettingsView`.
2. Pedir confirmação conservadora antes de salvar quando essa marca estiver ativa.
3. Comparar a nova linha v2 depois da v171 legada sem alterar a versão exibida no app.
4. Testar Não, Sim, a limpeza da marca após sucesso e a preservação byte a byte.

## Limites

A automação testa as decisões da confirmação por injeção determinística; a ordem padrão da caixa de diálogo é definida explicitamente como Não.
