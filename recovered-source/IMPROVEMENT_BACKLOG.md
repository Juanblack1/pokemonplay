# Backlog de melhorias do Pokemons Play

Estado atual em 2026-10-01: v170 compila em Release; a suíte completa passou incluindo a regressão de layout da barra da Biblioteca em 1140 px e 1280 px. A suíte cria fixtures em `%TEMP%`, remove tudo ao terminar mesmo com falha e restaura/remove o executável auxiliar de teste. Sessões DS/GBA/Azahar e restauração de atalho personalizado também passaram nos ciclos anteriores. O payload local não contém os executáveis do Azahar ou RetroArch, portanto a integração visual com os emuladores reais permanece sem smoke test.

Estado confirmado em 2026-10-01: a fonte v160 compila com o SDK .NET 10.0.401 instalado no cache do usuário, e a suíte completa foi executada com sucesso, incluindo os novos testes da sessão RetroArch embutida. Permanecem um aviso CS0108 já existente em InputWorkbench.Capture e um aviso NU1702 do projeto de testes ao referenciar o atualizador .NET Framework 4.8; não houve erros de compilação ou testes. A execução com RetroArch real ainda depende de ele estar instalado. v161 compila em Release e passou smoke check visual isolado da barra em 1000 px, cobrindo título ativo longo e sessão pausada; a suíte completa não foi executada novamente neste ciclo. RetroAchievements é opcional para GBA/DS; os testes validam detecção local de cores, seleção, configuração temporária, notificações, pausa ao perder o foco, retorno ao menu, retomada ou encerramento da sessão, foco e rótulos acessíveis, layout mínimo sem sobreposição, gravação parcial do tempo ativo, atualização imediata do histórico, saída coordenada e importação de `.srm` com executáveis simulados. Não há RetroArch instalado aqui, então o teste de jogo e conta reais permanece pendente. As capas da Biblioteca, da lista de jogos e do detalhe em Meus saves são carregadas sem manter os arquivos de imagem bloqueados. Slots ocupados de saves GBA/DS carregam sprites junto ao nome e nível; a verificação desenha o cartão e confirma os pixels do sprite em cache. No modo Mais jogados, a Biblioteca permite limpar o histórico completo, com confirmação que esclarece a remoção de datas e tempos; o controle adapta rótulo e acessibilidade ao modo e fica oculto fora de Recentes/Mais jogados. O histórico local mantém compatibilidade com os arquivos existentes, preserva o tempo total ao relançar e soma o tempo da sessão quando o emulador encerra. Os cartões de Recentes mostram duração formatada e leitores de tela recebem a mesma informação; a tela atualiza o total ao retornar ao launcher. Capas existentes de jogos 3DS são reconhecidas nos formatos PNG/JPG/JPEG com prioridade determinística; subpastas acompanham as ROMs e há fallback para capas planas. Quando a capa está ausente ou inválida, o cartão agora oferece uma ação para abrir/criar a pasta esperada. O Banco global carrega sprites do PokeAPI sem bloquear a interface, prioriza variantes shiny/fêmea quando disponíveis e usa sprites padrão como fallback; testes confirmam os cartões com cache local e uma consulta direta verificou imagens 96×96 padrão, shiny e fêmea com HTTP 200. O Banco explica quando o filtro de espécies repetidas não tem resultados e permite limpar filtros; a descrição é específica ao filtro, com busca e outros filtros ativos. O catálogo 3DS diferencia ROMs pelo caminho relativo completo quando nomes normalizados coincidem, mantendo títulos, saves e argumentos de execução distintos mesmo para formatos diferentes no mesmo diretório. O Banco global filtra espécies repetidas em relação aos resultados dos filtros ativos, pode manter um representante por espécie e restaura o modo ao limpar filtros. O resumo do Banco mostra Pokémon válidos e espécies distintas, contando repetidos apenas no primeiro número; arquivos inválidos permanecem destacados e excluídos. As consultas ao processo e à janela do emulador toleram objetos encerrados e liberam handles candidatos não selecionados. Falhas ao iniciar o emulador agora mostram orientação no GameHost, não entram em Recentes e permitem voltar sem confirmação de encerramento. Na Biblioteca, o botão de favorito identifica seu jogo para leitores de tela desde a primeira interação; cartões focados navegam pelas setas entre cartões vizinhos inclusive em seções de geração distintas; `Enter` ou `Espaço` inicia o fluxo de jogo; alterar favoritos conserva o foco ou o transfere para um jogo visível ou ação de recuperação. Na Biblioteca e no Banco global, `Esc` limpa a busca sem apagar filtros ativos; no Banco, `Ctrl+F` leva direto ao campo de busca e a busca textual ignora acentos. O download do atualizador não captura o contexto de interface durante leitura, gravação e extração assíncronas. O Banco global exibe sprites com variantes e fallback, tipos primário/secundário, navegação pelas setas e troca de página com `PageUp`/`PageDown`. A Biblioteca permite atualizar jogos 3DS pelo botão ou F5; filtros por geração se reorganizam em linhas quando necessário. Upload e restauração de save validam formato, jogo e checksums; restaurações por jogo confirmam a revisão; restauração do Banco compara ZIP e manifesto; envios recusam revisão concorrente. Os 99 arquivos monitorados foram comparados por caminho, tamanho e SHA-256, sem diferenças. A sessão Firebase local não está disponível; validação remota ao vivo segue pendente.

## Próximas melhorias priorizadas

1. **Pré-validar o limite da nuvem antes do envio — concluído em v67.** A tela mostra tamanho e quantidade do ZIP que será enviado, bloqueia acima de 4 MB e reutiliza o mesmo arquivo validado após a confirmação.
2. **Exibir idade e tamanho da cópia remota ao restaurar — concluído em v68.** A confirmação mostra conta, quantidade, data, tamanho e avisa que o banco local será substituído após criar backup.
3. **Cobrir recuperação por falha de I/O na restauração — concluído em v69.** Um teste injeta falha na troca e verifica que arquivos, bytes e pastas temporárias ficam consistentes.
4. **Validar o fluxo Firebase com uma sessão Google autenticada.** O fluxo remoto não foi testado ao vivo; verificar login, consulta, envio, conflito entre dispositivos e restauração sem substituir dados reais sem confirmação clara.

## Ideias em avaliação

- Embutir jogos via RetroArch na janela principal, mantendo navegação e retorno à Biblioteca sem encerrar a sessão: implementado e validado em v160. Próximo passo possível: avaliar pausa equivalente para VBA-M, melonDS e Azahar sem suspender processos à força.


## Ciclo mais recente
- v170 (verificado): o contexto da ordenação da Biblioteca só aparece quando cabe depois das ações visíveis. A posição e a visibilidade são recalculadas quando a ordenação ou a ação de limpar o histórico muda; testes reproduzem a barra em 1140 px e 1280 px. Suíte completa aprovada.
- Higiene de verificações (verificada): a suíte completa deixou de gravar screenshots e fixtures em `output/profiles-check`; usa uma pasta temporária única e remove a pasta e o auxiliar `melonDS.exe` ao final. Execução a partir da raiz: suíte completa aprovada e `ALL CHECKS PASSED; temporary fixtures removed.`
- v169 (verificado): Configurações agora abre a lista oficial atual de sistemas e cores compatíveis com conquistas, ao lado da configuração de conta do RetroArch. Os dois botões têm nomes e descrições acessíveis, cabem na largura compacta e abrem destinos distintos; build Release e 27 verificações focadas passaram.
- v168 (verificado): Configurações ganhou **Abrir RetroArch · configurar conta**; o atalho só habilita quando o executável salvo existe, abre o app sem iniciar jogo e orienta o usuário a entrar em Configurações > Conquistas. A acessibilidade descreve o destino e confirma que o Pokemon Play não guarda a senha; layout compacto, estado desabilitado e inicialização foram verificados. Build Release e 25 verificações focadas passaram.
- v167 (verificado): o botão Retomar sempre pode usar F4 mesmo quando o usuário personalizou o atalho do Azahar. O atalho do usuário é sobrescrito apenas na configuração temporária da sessão e restaurado depois; mudanças em outros atalhos são preservadas. Build Release e 22 verificações focadas passaram.
- v166 (verificado): falhas ao gravar a configuração temporária do Azahar agora removem também o marcador e a pasta de sessão vazia. O teste força a falha de I/O e confirma que nenhum artefato temporário sobra; build Release e 20 verificações da sessão DS/GBA/Azahar passaram.
- v165 (verificado): jogos 3DS agora usam a sessão embutida do Azahar, com pausa ao perder foco, retorno à Biblioteca e retomada pelo atalho F4 padrão depois que o launcher recupera o foco. A configuração original é restaurada ao fechar o jogo ou recuperada no próximo início após uma interrupção; controles e outras preferências são preservados. Build Release, 18 verificações da sessão DS/GBA/Azahar, testes de barra/capas/sprites e suíte completa aprovados. O executável do Azahar não está incluído no payload local, então não foi possível validar a captura da janela e o F4 contra o emulador real.
- v161 (validado por smoke check): título de jogo longo recebe reticências sem invadir os controles; Retomar fica limitado a 180 px e conserva nome completo de jogo/perfil para acessibilidade; sob 80 px disponíveis, o título redundante do topo cede espaço à página. Build Release e smoke check WinForms em largura mínima aprovados; suíte completa não repetida.
- v160 (verificado): sessão RetroArch ocupa o painel central do launcher, preservando a barra e a navegação; trocar de tela pausa e mantém **Retomar** no topo; a página anterior e seus filtros são restaurados, e o histórico da Biblioteca é atualizado; configuração temporária força modo janela sem moldura; build Release sem erros e suíte completa aprovada, incluindo os testes de integração, navegação, pausa, retomada e configuração. Execução manual com RetroArch real não disponível neste ambiente.
- v159: ao voltar à Biblioteca, retomada/encerramento identificam título e perfil; o foco de teclado vai para a ação de retomar; teste visual confirma que as ações não se sobrepõem na largura mínima; suíte completa: 345 verificações aprovadas.
- v158: fechar o Pokemon Play com sessão pausada mantém a confirmação de encerramento e completa a saída do aplicativo quando o jogo fecha; cancelar preserva o app e a sessão; suíte completa: 344 verificações aprovadas.
- v157: ao pausar/retornar à Biblioteca, grava imediatamente os segundos ativos e atualiza Recentes/Mais jogados; retomadas somam somente o intervalo ainda não salvo; suíte completa: 343 verificações aprovadas.
- v156: o cronômetro do RetroArch para ao perder o foco ou voltar à Biblioteca e reinicia ao retomar; Recentes/Mais jogados passam a refletir tempo ativo; testes cobrem parada e retomada do cronômetro; suíte completa: 342 verificações aprovadas.
- v155: a Biblioteca distingue retomar do RetroArch e encerrar a sessão pausada; encerrar reutiliza confirmação de fechamento e o cancelamento preserva o jogo; ações da barra são removidas ao fechar; suíte completa: 341 verificações aprovadas.
- v154: jogos iniciados pelo RetroArch pausam quando perdem o foco; F12 ou **Pausar · Menu** volta à Biblioteca mantendo a sessão e a ação **Jogo pausado · Retomar** restaura a janela; fechar o host continua encerrando após confirmação; suíte completa: 340 verificações aprovadas.
- v153: ao selecionar o executável do RetroArch, detecta mGBA/VBA-M/VBA Next e melonDS DS/melonDS/DeSmuME na pasta `cores`, preenche só caminhos vazios ou inválidos e mantém a ativação manual por geração; suíte completa: 338 verificações aprovadas.
- v152: Configura RetroArch e cores Libretro por geração; inicia GBA/DS com cheevos e popup habilitados, sem guardar credenciais; salva SRAM `.srm` no perfil ativo e aceita importação/validação em GBA e DS; suíte completa: 336 verificações aprovadas com executável/core simulados. A execução com RetroArch real não foi validada, pois não está instalado neste ambiente.

- v151: Biblioteca, lista de jogos e detalhe em Meus saves compartilham um carregador que clona capas e fecha o arquivo de origem; testes confirmam acesso exclusivo ao arquivo em todos os três contextos e fallback para capa corrompida; suíte completa: 327 verificações aprovadas.
- v150: slots ocupados de saves GBA/DS carregam sprites em cache ao lado do nome e nível; slots vazios mantêm o estado atual; sprite e informações permanecem acessíveis; suíte completa: 323 verificações aprovadas, incluindo pintura do sprite no cartão.
- v149: Banco global mostra uma prévia ampliada, acessível e sincronizada com a seleção do sprite; a grade libera corretamente as imagens anteriores ao atualizar filtros; suíte completa: 321 verificações aprovadas.
- v148: busca e filtro de sistema da Biblioteca anunciam contexto e instruções de uso aos leitores de tela; o nome do filtro continua contextual quando muda a seleção; suíte completa: 319 verificações aprovadas.
- v147: botão contextual **Limpar histórico** no modo Mais jogados; confirmação informa que datas e tempos serão apagados; ação acessível e oculta fora das listas Recentes/Mais jogados; verificado pela suíte.
- v146: Biblioteca ganhou ordenação **Mais jogados** por tempo acumulado, combinável com busca, sistema e favoritos; chips se ajustam à largura e os cartões expõem o total; 316 verificações passaram.
- v145: histórico preserva e soma o tempo das sessões; GameHost registra tempo ao encerrar o emulador; cartão Recentes exibe duração legível e acessível e atualiza ao retornar; histórico legado permanece válido; 307 verificações passaram.
- v144: cartão sem capa oferece um botão dimensionado para abrir/criar a pasta esperada; suítes cobrem arquivos corrompidos, nomes ausentes, subpastas e rejeição de caminhos fora da instalação; prévia verificada e 298 verificações aprovadas.
- v143: filtro “Só repetidas” mostra orientação específica e opção de limpar quando não há espécies repetidas; limpar recupera os Pokémon únicos; suíte completa passou com 296 verificações.
- v142: identidade 3DS usa o caminho relativo completo ao resolver colisões, separando nomes normalizados e formatos diferentes no mesmo diretório; capas planas seguem funcionando; suíte completa passou com 294 verificações.
- v141: Banco global ganhou filtro para encontrar espécies que se repetem nos resultados atuais ou manter só um Pokémon por espécie; combina com outros filtros e limpa/reset corretamente; suíte completa passou com 293 verificações.
- v140: nomes repetidos de ROMs 3DS em subpastas geram títulos e pastas de save distintos; capas podem seguir a hierarquia das ROMs e mantêm fallback plano; três verificações específicas, suíte completa passou com 289 verificações.
- v139: resumo do Banco global diferencia Pokémon válidos de espécies distintas, sem contar arquivos inválidos no progresso; verificado com duplicatas entre formatos e uma coleção só inválida; suíte completa passou com 286 verificações.
- v138: consultas de processo/janela no GameHost tratam processos encerrados no meio da leitura e garantem a liberação dos handles não selecionados; suíte completa passou com 284 verificações.
- v137: erro de inicialização do executável do emulador fica visível no GameHost, não cria entrada em Recentes e oferece retorno ao menu direto; suíte completa passou com 283 verificações.
- v136: o botão de favorito anuncia o nome do jogo tanto antes quanto depois da interação; suíte completa passou com 281 verificações.
- v135: a navegação da Biblioteca considera todos os cartões visíveis em todas as seções de geração; as setas continuam cruzando entre seções; suíte completa passou com 280 verificações.
- v134: cartões da Biblioteca usam as setas para navegar pelo cartão vizinho mais próximo, com foco rolado para mantê-lo visível; suíte completa passou com 279 verificações.
- v133: a Biblioteca direciona `Enter` e `Espaço` do cartão focado ao fluxo existente de lançamento; confirmação e cancelamento foram testados em diálogo modal; suíte completa passou com 277 verificações.
- v132: a Biblioteca não recria os cartões ao alterar favoritos fora do filtro; no filtro, preserva a navegação movendo o foco ao próximo jogo ou à ação do estado vazio; suíte completa passou com 276 verificações.
- v131: cartões do Banco global usam `PageUp`/`PageDown` para atravessar páginas, focam o primeiro Pokémon de destino, reconhecem as teclas no WinForms e respeitam os dois limites; a suíte também confirma sprites em cache nos cartões, incluindo variantes shiny e fêmea; 273 verificações passaram.
- v130: `Esc` limpa consultas da Biblioteca e do Banco sem descartar outros filtros; `DownloadAsync` evita deadlock do contexto WinForms ao fazer I/O; 266 verificações passaram.
- v129: `Ctrl+F` posiciona o foco na busca do Banco global; cobertura com janela WinForms ativa.
- v128: a busca do Banco global agora ignora acentos em nomes de espécie, apelidos e nomes de arquivo, seguindo a regra de busca da Biblioteca.
- v127: cartões 3DS sem arte apontam para o caminho de PNG esperado; a busca de capas aceita JPG/JPEG e prioriza PNG quando há arquivos correspondentes duplicados.
- v126: filtros por geração da Biblioteca quebram em linhas responsivas para manter os nomes completos em janelas compactas; testado em 762 px, junto ao botão Atualizar e ao atalho F5.
- v125: ação “MOSTRAR TODOS” reconstrói a Biblioteca imediatamente após limpar filtros, inclusive quando busca e filtro de sistema já estavam no padrão; cobertura incluída na suíte completa, que passou.
- v124: Biblioteca ganhou botão Atualizar e atalho F5 para redescobrir jogos 3DS e recarregar capas sem reiniciar; busca e filtros são preservados, e o cabeçalho mostra hora e quantidade atualizadas.
- v123: cartões do Banco global aceitam navegação pelas setas na direção do cartão vizinho, atualizam os detalhes e rolam para manter o destino visível; Tab, Enter e Espaço permanecem disponíveis. Verificado com uma janela WinForms ativa e capturas em 1000×720 e 1280×820.
- v122: cartões do Banco global exibem etiquetas compactas dos tipos primário e secundário, com cores e rótulos compartilhados pelo filtro por tipo; nomes completos também entram no nome acessível do cartão.
- v121: cartões do Banco global passam a carregar sprites frontais com cache limitado e reserva offline. Capas inválidas agora usam fallback seguro, liberam o arquivo de origem e informam o caminho esperado. A instalação local contém e decodifica as 10 capas configuradas; não há ROMs 3DS no momento, e a pasta opcional de capas 3DS está ausente.
- v120: `Ctrl+F` transfere o foco da biblioteca para a busca, com teste de teclado em janela ativa.
- v119: busca da biblioteca trata acentos como equivalentes para títulos e descrições, mantendo a grafia original no resultado.
- v118: cartões Recentes mostram data e hora local da última abertura; a descrição acessível acompanha a informação, enquanto outros modos mantêm os cartões sem data de atividade.
- v117: biblioteca ganhou uma visão alfabética que reúne gerações e mantém os filtros combináveis.
- v116: filtro Recentes ganhou ação confirmada para apagar o histórico local, com estado vazio após a limpeza; a barra do catálogo se reorganiza para caber na área mínima com navegação lateral.
- v115: a biblioteca filtra jogos abertos recentemente com ordenação local, combinação com busca/sistema/favoritos e estado vazio orientativo; o histórico mantém até 50 títulos sem enviar dados à nuvem.
- Publicação: o conector encontrou `Juanblack1/pokemonplay`, privado e com apenas o README no branch `main`. O atualizador requer um repositório público, e a fonte ainda contém configuração Firebase de cliente; nenhuma publicação foi feita.
- v108: envio para nuvem compara a revisão que foi mostrada na confirmação com a revisão lida imediatamente antes do commit, para o Banco global e saves por jogo; alterações concorrentes cancelam o envio. O commit também conserva a precondição de revisão do Firestore para detectar mudanças durante o envio do ZIP.
- v109: restauração por jogo mostra os metadados da cópia Google confirmada e recusa uma revisão diferente no início ou no fim do download, antes de tocar no save local.
- v110: o Banco global permite buscar e identificar Pokémon por nome de espécie via PKHeX, além de número, apelido e nome do arquivo.

- v63: arquivos inválidos não contam na listagem e ganham estado vazio orientativo.
- v64: erros comuns de decodificação são normalizados como arquivo inválido.
- v65: restauração limpa filtros e seleção antigos e recarrega a lista.
- v66: leitor compartilhado entre a interface e a validação de backups.
- v67: prévia de tamanho e bloqueio do limite de 4 MB antes de enviar à nuvem; arquivo confirmado é o mesmo que foi validado.
- v68: confirmação da restauração informa a data e o tamanho da cópia remota.
- v69: rollback da troca do banco testado com uma falha simulada.
- v70: rollback da restauração de save por perfil coberto com falha simulada.
- v71: jogo e perfil ativo exibidos durante a sessão.



7. **Mostrar o atalho F12 no jogo — concluído em v72.** A tecla de retorno já existia; agora a barra informa seu efeito e que há confirmação antes de fechar.
- v72: atalho F12 para voltar ao menu explicado na barra da sessão de jogo.

8. **Persistir a sessão Firebase com troca atômica — concluído em v73.** O teste injeta falha na troca e confirma que a sessão anterior e o arquivo original ficam intactos.
- v73: sessão Firebase protegida é gravada por substituição atômica e falha não corrompe a sessão anterior.

9. **Mostrar resumo do progresso no seletor — concluído em v74.** O seletor indica saves compatíveis e sua data mais recente antes de iniciar o jogo.
- v74: seletor de jogo mostra contagem de saves compatíveis, save mais recente ou estado vazio.

10. **Distinguir perfil sem save de arquivos não reconhecidos — concluído em v75.** O seletor explica se o perfil ainda não tem arquivos ou se há conteúdo que não foi reconhecido como save.
- v75: diagnóstico de perfil vazio versus arquivos incompatíveis no seletor de jogo.

11. **Abrir a pasta do perfil pelo seletor — concluído em v76.** Um botão abre o diretório do perfil atual e acompanha a seleção; fica indisponível quando não há pasta para abrir.
- v76: atalho no seletor para conferir e organizar diretamente os arquivos do perfil.

12. **Explicar onde e como o banco global é copiado — concluído em v77.** A tela explicita a cópia local, a associação à conta Google, o armazenamento no Firebase do app e o fato de que envio/restauração são manuais e não usam o Drive.
- v77: explicação da cópia Google sempre visível no Banco Pokémon.

13. **Detalhar a substituição de backup Google — concluído em v78.** A confirmação de envio mostra os dados da coleção local e da cópia remota que será substituída, incluindo quantidade, data e tamanho, com fallback para metadados ausentes.
- v78: contexto completo antes de sobrescrever a cópia remota do banco global.

14. **Fixar a versão da cópia confirmada ao restaurar — concluído em v79.** A restauração verifica o identificador da revisão remota depois da confirmação; se mudou, cancela antes de substituir o banco local. Versões sem identificador usam os metadados disponíveis.
- v79: restauração não aplica silenciosamente uma cópia diferente da que foi confirmada.

15. **Identificar backups locais pelo nome — concluído em v80.** A lista de restauração mostra o nome do arquivo além da data, tamanho e tipo manual/automático; nomes longos preservam começo, sufixo único e extensão sem esconder os metadados.
- v80: seleção de backup mais fácil quando várias cópias foram criadas no mesmo período.

16. **Abrir a pasta da coleção global — concluído em v81.** A tela do banco oferece acesso direto ao diretório local compartilhado pelos perfis.
- v81: botão para abrir a pasta do Banco Pokémon no Explorador.

17. **Repetir os detalhes do backup antes da restauração — concluído em v82.** A confirmação final informa o perfil e os dados do arquivo selecionado junto com o aviso de substituição e preservação do save atual.
- v82: contexto do arquivo selecionado na última etapa antes de restaurar.

18. **Mostrar a conta Google nos saves por jogo — concluído em v83.** A tela e as confirmações de envio/restauração identificam o e-mail da conta conectada.
- v83: confirmação visual da conta de destino/origem dos backups de save por jogo.

19. **Exibir progresso do perfil em Meus saves — concluído em v84.** A tela mostra a contagem de saves compatíveis e a data do mais recente e atualiza ao trocar de perfil.
- v84: resumo do progresso também disponível durante o gerenciamento de saves.

20. **Identificar jogo e perfil ao sincronizar saves — concluído em v85.** As confirmações de envio e restauração mostram o jogo, o perfil ativo e o e-mail Google associado.
- v85: contexto completo antes de enviar ou restaurar um save por jogo.

21. **Explicar o isolamento por perfil na nuvem — concluído em v86.** O estado conectado mostra jogo, perfil, conta e que cada perfil mantém uma cópia própria no Firebase.
- v86: clareza sobre qual save está selecionado e como os perfis ficam separados na nuvem.

22. **Tratar falhas ao abrir a pasta do banco — concluído em v87.** O erro de acesso ou inicialização do Explorador agora aparece na tela do banco sem interromper o aplicativo.
- v87: abertura da pasta local com retorno claro em caso de falha.

23. **Identificar o ZIP antes de importar o banco global — concluído em v88.** A confirmação exibe o nome do arquivo, a quantidade de Pokémon e a cópia de segurança local criada antes da troca.
- v88: confirmação de importação deixa explícito qual backup será aplicado.

24. **Importar vários arquivos Pokémon de uma vez — concluído em v89.** O importador em lote valida todos os arquivos antes de gravar e mantém os formatos de Gen 3, 4 e 5.
- v89: importação em lote PK3/PK4/PK5 com validação prévia do conjunto.

25. **Selecionar várias gerações no importador em lote — concluído em v90.** Um filtro conjunto mostra PK3, PK4 e PK5 ao mesmo tempo, com opções individuais mantidas.
- v90: seleção mista de arquivos compatíveis na importação do Banco global.


## Suporte PK6–PK9 v91

- O Banco global agora importa, valida, exporta, conta e inclui em ZIP arquivos PK6, PK7, PK8 e PK9, além de PK3–PK5.
- O filtro de importação em lote apresenta os sete formatos, e o filtro de exportação acompanha a geração selecionada.
- A suíte passou com 149 verificações, incluindo round-trip e inclusão em backup das quatro novas gerações.
- 99 arquivos de dados monitorados foram preservados por hash durante a instalação. Firebase não autenticado nesta sessão; operação remota ao vivo não verificada.


## Filtro por geração v92

- A navegação do Banco global filtra Gen 3 a Gen 9 e combina o filtro com busca e ordenação.
- Estado vazio identifica a geração selecionada sem resultados e oferece o retorno a todas.
- 149 verificações aprovadas; 99 arquivos de dados monitorados idênticos por hash; prévia instalada com 17 telas conferida visualmente. Firebase não autenticado nesta sessão; sincronização remota ao vivo não verificada.


## Consistência de extensão PK3–PK9 v93

- A validação do Banco global compara a geração decodificada com a extensão PK3–PK9 e rejeita arquivo rotulado incorretamente antes de alterar a coleção.
- Novo teste usa bytes PK9 com extensão PK6 e confirma rejeição tanto na leitura quanto na importação sem criar a pasta de destino.
- Suíte com 149 verificações aprovadas.


## Retorno da importação por geração v94

- Importação em lote apresenta contagem total e resumo por geração; importação individual identifica formato e arquivo.
- Mensagens de falha foram alinhadas ao suporte PK3–PK9 e à validação extensão/conteúdo.
- 149 verificações aprovadas, incluindo mistura PK6–PK9.


## Compatibilidade de transferência visível v95

- O botão de transferência do Banco fica desativado quando a geração difere do save aberto.
- Os detalhes identificam as duas gerações e explicam que o original permanece no Banco global.
- Teste confirma bloqueio e explicação ao selecionar PK3 com save Gen 4 aberto.


## Ação Banco/save nomeada com clareza v96

- O botão do Banco global informa Copiar para save e mantém o Pokémon original na coleção.
- Em slots do save, o botão continua Mover Pokémon.
- Nomes acessíveis e verificações cobrem os dois contextos.


## Remoção recuperável do Banco v97

- A interface oferece Remover do banco após selecionar um Pokémon e pede confirmação com o destino do backup.
- Arquivo Pokémon e sidecar de origem são movidos juntos para Backups/Automaticos/Pokemon-Banco-Removidos; falha na movimentação tenta restaurar os originais.
- Testes cobrem formato PK9 recuperável e habilitação da ação apenas com seleção.


## Prévia do conteúdo em ZIP v98

- A confirmação de importação do Banco global mostra a contagem por geração além do nome, total e backup local.
- Restore ZIP PK6–PK9 exercitado até validação da coleção extraída; quatro arquivos mantêm seus formatos.
- Testes agrupam e exibem resumo para ZIP Gen 3 e coleção mista Gen 6–9.


## Análise PKHeX somente leitura v99

- Botão Analisar com PKHeX disponível para Pokémon selecionado no Banco e nos slots do save.
- Janela exibe relatório técnico e quantidade de apontamentos, sem gravar ou alterar os bytes.
- Testes validam apontamentos em PK9, preservação do arquivo, janela e habilitação contextual.

- A prévia v99 ajustou as ações laterais: mostra somente controles do contexto atual para não cobrir o filtro do Banco.


## Recuperação de Pokémon removidos v100

- O Banco oferece Restaurar removido e abre a pasta de cópias recuperáveis.
- Restaura arquivo e origem juntos; nome original ocupado gera nome novo, sem sobrescrever o Pokémon existente.
- Teste round-trip cobre recuperação PK9, sidecar e colisão de nomes.

- O repacotador agora reconhece marcadores de versão com quantidade variável de dígitos; reconstrução e inspeção do pacote v100 passaram.


## Contagem de itens recuperáveis v101

- A ação de restauração informa quantos Pokémon removidos estão disponíveis e fica desativada quando não há nenhum.
- Nome acessível inclui a contagem para leitores de tela.
- Testes cobrem pasta vazia e presença de um PK9 recuperável.
- Ajuste final do layout: filtros e paginação deixam de ser sobrepostos; ações laterais compactadas ficam visíveis no tamanho padrão da janela.


## Publicação atômica de backups ZIP v102

- Backups verificados de saves e do Banco Pokémon são montados em temporário na mesma pasta do destino.
- O ZIP só é publicado depois das validações de conteúdo e da checagem de que os arquivos de origem não mudaram durante a compactação.
- Em falha, o temporário é limpo e um arquivo existente no destino permanece intacto.
- Testes simulam mudanças concorrentes nos saves e no Banco global; ambos preservam o arquivo anterior e não deixam temporários.


## Retorno claro do backup manual v103

- Ao concluir, o painel de saves informa quantidade de arquivos, tamanho do ZIP e nome criado.
- Em caso de falha, o erro fica visível na tela e em uma mensagem; o backup incompleto não é publicado e os saves originais ficam intactos.
- Testes cobrem resumo singular/plural e tamanhos em KB/MB.


## Contagem validada de itens removidos v104

- A ação de recuperação conta apenas arquivos que o leitor PKHeX consegue ler, com checksum e formato correspondentes.
- Arquivos inválidos permanecem intocados na pasta recuperável e não aumentam a contagem acessível.
- O teste combina um PK9 válido e um arquivo corrompido e confirma contagem correta e bytes preservados.


## Validação de jogo antes do restore Firebase v105

- A restauração de save exige o jogo selecionado e valida o ZIP extraído com PKHeX antes de criar backup ou tocar no destino ativo.
- A confirmação informa que o save será validado para aquele jogo e que o progresso atual será preservado como backup.
- Teste usa um save Gen 5 válido ao tentar restaurar em FireRed e confirma rejeição, bytes locais intactos e ausência de staging/backup.


## Validação de jogo antes do upload Firebase v106

- A confirmação de envio identifica jogo e perfil e informa que o save será validado antes de substituir a cópia remota.
- O ZIP enviado é extraído em pasta temporária e validado por geração, checksums e jogo selecionado antes de gravar qualquer documento no Firebase.
- Testes aceitam Gen 5 em Pokémon Black, rejeitam o mesmo ZIP para FireRed e confirmam limpeza da pasta de validação.


## Contagem do ZIP da nuvem conferida antes do restore v107

- O restore do Banco global compara a contagem confirmada com a quantidade real de arquivos Pokémon válidos no ZIP baixado.
- Divergência interrompe a troca local; manifestos antigos sem contagem explícita continuam compatíveis.
- Testes cobrem correspondência, divergência e compatibilidade legada.


## Ordenação do Banco pela data de captura v114

- A opção **Captura · recentes** ordena Pokémon com data válida do mais novo ao mais antigo e mantém no final os arquivos sem data.
- A data de captura aparece nos detalhes do Pokémon selecionado, sem modificar o arquivo da coleção.
- Testes exercitam o rótulo do seletor, a ordem das datas, a posição de itens sem data e a exibição `dd/MM/yyyy`; compilação e 210 verificações aprovadas.
## Sessão Nintendo DS integrada v162

- Jogos melonDS são hospedados na área principal do launcher, mantendo navegação e barra superior disponíveis para voltar à Biblioteca sem encerrar a emulação.
- `PauseLostFocus` é temporariamente ativado; ao encerrar, o valor original e demais alterações feitas pelo emulador são preservados.
- Teste direcionado verifica a pausa de foco, restauração de valor existente, remoção de chave temporária e preservação de outras mudanças do TOML.

## Sessão GBA integrada v163

- Jogos VBA-M passam a usar a área de jogo gerenciada pelo launcher; a navegação e os controles globais permanecem disponíveis.
- Pausa ao perder foco é ativada nas configurações do usuário e portátil somente durante a sessão; o valor anterior é restaurado sem descartar alterações paralelas do emulador.
- Testes cobrem as duas configurações INI e o caso sem seção `[preferences]`.

## Recuperação de sessão interrompida v164

- Ao iniciar, o launcher examina os diretórios temporários de sessão do melonDS, VBA-M e RetroArch.
- Marcadores e configurações GBA/DS só são restaurados quando o processo do emulador não está em execução.
- Arquivos `.cfg` do RetroArch são removidos somente quando não há RetroArch aberto.
- Teste cria sessões GBA e DS abandonadas e confirma restauração e remoção dos marcadores.
