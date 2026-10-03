# Ciclo 7 — recuperação em Meus saves

Problema: a tela lê perfis e enumera arquivos sem tratar falhas; um JSON inválido impede sua abertura. A ação de nuvem também lê perfis depois de desabilitar a interface, antes do try/finally, deixando-a travada se a leitura falhar.

Critérios: R1 falhas esperadas de leitura mostram aviso com jogo e orientação, preservando a seleção de outros jogos; R2 aviso oferece Tentar novamente e mantém arquivos intactos, sem resetar perfis; R3 nenhum botão de backup, restauração ou criação de perfil fica disponível sem dados válidos; R4 correção explícita do arquivo seguida de nova tentativa reconstrói o detalhe; R5 leitura que falha antes de operação de nuvem não consulta rede nem deixa busy/seleção presos; R6 estado válido mantém controles e suíte anterior passa.

Plano: isolar a construção do detalhe, descartar controles parciais no erro e apresentar recuperação; trazer leituras de contexto para o try/finally da ação de nuvem. Testes sintéticos de JSON, bloqueio de arquivo, seleção e nova tentativa, preservação de bytes e falha anterior à rede. Não executar login, upload ou download reais.

Verificação: a reprodução anterior falhou com JsonException em BuildProfileControls, propagada até o construtor de SaveManagerView. Dez regressões passaram após a correção, incluindo arquivo bloqueado e recuperação após liberação. A suíte local completa com previews passou com 546 verificações. Renderizações nativas em 760 e 1100 pixels inspecionadas: mensagem e nova tentativa legíveis, sem ações de escrita no estado de erro. Falha de preflight da nuvem testada antes de qualquer await de rede; nenhuma conta, login, upload ou restauração real executados.
