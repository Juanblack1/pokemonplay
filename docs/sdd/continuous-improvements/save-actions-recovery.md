# Ciclo 8 — falhas de leitura depois de abrir Meus saves

Problema: Abrir pasta, Backup ZIP e Restaurar backup leem o perfil antes de tratar falhas. Se o JSON mudar ou ficar bloqueado depois de renderizar o detalhe, o clique lança uma exceção em vez de oferecer recuperação.

Critérios: R1 os três cliques tratam JSON inválido e arquivo bloqueado sem exceção não tratada; R2 a tela atualiza o aviso e oferece nova tentativa, mantendo outros jogos selecionáveis; R3 perfil, saves e backups existentes permanecem byte a byte; R4 falha anterior à preparação não abre Explorer, seletor de restauração nem cria ZIP; R5 após correção explícita na fixture a nova tentativa restaura os controles; R6 confirmações e validações existentes de backup/restauração permanecem; suíte anterior passa e atualização é publicada ao concluir o ciclo.

Plano: proteção compartilhada para falhas esperadas dos três handlers, reconstruindo o detalhe e apresentando status de ação interrompida. Exercitar cliques reais em fixtures sintéticas corrompidas/bloqueadas após a construção válida. Não restaurar arquivos pessoais nem acionar processos externos nos cenários de falha.
