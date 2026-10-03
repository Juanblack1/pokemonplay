# Ciclo 8 — falhas de leitura depois de abrir Meus saves

Problema: Abrir pasta, Backup ZIP e Restaurar backup leem o perfil antes de tratar falhas. Se o JSON mudar ou ficar bloqueado depois de renderizar o detalhe, o clique lança uma exceção em vez de oferecer recuperação.

Critérios: R1 os três cliques tratam JSON inválido e arquivo bloqueado sem exceção não tratada; R2 a tela atualiza o aviso e oferece nova tentativa, mantendo outros jogos selecionáveis; R3 perfil, saves e backups existentes permanecem byte a byte; R4 falha anterior à preparação não abre Explorer, seletor de restauração nem cria ZIP; R5 após correção explícita na fixture a nova tentativa restaura os controles; R6 confirmações e validações existentes de backup/restauração permanecem; suíte anterior passa e atualização é publicada ao concluir o ciclo.

Plano: proteção compartilhada para falhas esperadas dos três handlers, reconstruindo o detalhe e apresentando status de ação interrompida. Exercitar cliques reais em fixtures sintéticas corrompidas/bloqueadas após a construção válida. Não restaurar arquivos pessoais nem acionar processos externos nos cenários de falha.

Evidência RED: execução local bloqueada por App Control antes dos testes; não é usada como reprodução. CI37092320658 no commit3dedd3e5ef50b459bf46022beb4447f163a85fef reproduziu JsonException propagada de Load/ActiveFolder/SelectedFolder pelo clique real de Abrir pasta. Correção compila localmente com zero erros; GREEN pendente no CI. Acrescentada verificação de backup válido depois de liberar o bloqueio, conferindo conteúdo do ZIP e preservação de origem e backup anterior.
