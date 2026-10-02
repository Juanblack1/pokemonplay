# Ciclo: verificar arquivos antes da sessão 3DS

## Problema

O fluxo GBA/DS valida a ROM e o executável antes de aplicar configurações. O fluxo 3DS aplicava controles e criava uma sessão antes de detectar a falta do Azahar; uma ROM importada movida sequer era validada nessa etapa.

## Aceitação

- Azahar ausente: mensagem identifica o emulador e a pasta esperada, sem alterar controles ou criar sessão.
- ROM 3DS importada ausente: mensagem identifica o arquivo; configuração existente permanece byte a byte igual, sem marcador de sessão.
- Ambos os arquivos disponíveis: manter a sessão gerenciada, pausa e recuperação existentes.
- Nenhuma mudança nas regras de nuvem, conteúdo das ROMs, saves ou políticas de segurança do Windows.

## Evidência planejada

`AzaharSessionCheck` reproduz os dois arquivos ausentes e confirma a sessão válida com fixtures que não são executadas. A suíte completa e o empacotamento Windows verificam regressões. Não é um teste de emulação real.
