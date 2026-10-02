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

## Resultado

Em 2026-10-02, o [run 37044110723](https://github.com/Juanblack1/pokemonplay/actions/runs/37044110723) aprovou o commit `2cddedb3306577a8c134fd7a58cf465b2677d0b6`: os dois arquivos ausentes preservaram a configuração e não criaram marcador; a sessão válida continuou funcionando. A suíte completa e o empacotamento passaram. A suíte removeu suas fixtures temporárias no runner. O build local passou com apenas o aviso preexistente CS0108.
