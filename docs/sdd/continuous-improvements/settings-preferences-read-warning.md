# Avisar quando as preferências locais não puderem ser carregadas

## Problema e resultado

`SettingsView.LoadSettings` captura qualquer falha de leitura e continua em silêncio. A tela parece carregada normalmente, embora controles possam estar nos valores padrão; salvar em seguida pode substituir as preferências que a aplicação não conseguiu ler. A abertura deve informar o problema sem gravar ou alterar o arquivo.

## Requisitos e aceitação

- R1: Se o arquivo de preferências não puder ser lido, Configurações continua abrindo e exibe um aviso claro na barra de status.
- R2: O aviso também aparece em `AccessibleDescription` para tecnologia assistiva.
- R3: Abrir a tela nunca grava automaticamente as preferências; o conteúdo original permanece byte a byte igual após falha de leitura.
- R4: A leitura normal e os arquivos legados parciais continuam funcionando sem aviso.

## Plano de implementação

1. Manter o comportamento de fallback, mas expor o estado de leitura na mensagem de status.
2. Exercitar uma falha determinística usando compartilhamento exclusivo do arquivo durante a construção de `SettingsView`.
3. Confirmar texto visível/acessível, preservação de bytes e o caminho normal existente na suíte completa.

## Limites

O teste valida leitura negada no Windows CI; não simula política de antivírus, erro físico do disco ou todo leitor de tela.
