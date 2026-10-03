# Ciclos contínuos de melhorias

Objetivo autorizado: descobrir e implementar melhorias úteis até o limite de uso impedir continuidade ou o usuário pedir para parar. Runtime: objetivo durável nesta conversa, ciclos encadeados após verificação, sem tarefa agendada paralela. Limite observado no início: janela de 5h em 78% e semanal em 12%; não consumir resets/créditos nem trocar para modelos de reserva automaticamente.

Prioridade: confiabilidade de biblioteca/importação, experiência de pesquisa, diagnóstico de jogos/controles e proteção de saves. Preservar alterações preexistentes; publicar código em PRs, sem nova release neste escopo. Uma investigação/alteração concreta por ciclo; testes proporcionais, evidências e próximos passos registrados. Parar a qualquer pedido do usuário ou bloqueio de uso real.

Ciclo 1 já verificado: controles e teste nativo GBA, PR 19, CI 37083874887 success. ROMs pessoais e controle físico permanecem sem teste real por App Control e ausência de hardware conectado.

Ciclo 2: descoberta de ROMs. Hoje a descoberta 3DS e o exame de pastas usam recursão que pode seguir junctions e falhar completamente por uma subpasta inacessível. Implementar varredura compartilhada que preserva arquivos acessíveis, não atravessa links de diretório e comunica exame incompleto na importação. Critérios: C2.1 encontra extensões compatíveis em subpastas normais, incluindo nomes/acento e arquivos ocultos; C2.2 exclui destinos de links/reparse e não entra em ciclos; C2.3 falha de uma subpasta não remove resultados das demais; C2.4 mantém limite de 500 ROMs no fluxo de importação com aviso existente; C2.5 não copia/altera ROMs; C2.6 suíte completa passa com fixtures sintéticas.

Plano: scanner de diretórios com resultado (arquivos, locais ignorados, limite), integração no catálogo 3DS e exame de pasta, testes de fronteiras/erro por diretório e junction de fixture, validação em CI Windows. Não reescrever saves nem emuladores.

Próxima fila: evitar reconstrução e leitura de capas a cada tecla de pesquisa; facilitar localizar jogo quando filtros ativos ocultam resultados; sinalizar ROMs importadas movidas preservando associação de saves.

Evidência do ciclo 2: reprodução de junction circular mostra 8 ocorrências no enumerador recursivo antigo para 3 arquivos; scanner corrigido retorna 3 e informa link ignorado. Teste focado: 8 PASS. Suíte completa local: exit 0, ALL CHECKS PASSED, fixtures removidas (`output/library-cycle-full-local.log`). Compilação Release: zero erros. Acrescentado workflow manual rápido para verificar app/atualizador e guardar logs durante ciclos sem recompilar emuladores inalterados.
