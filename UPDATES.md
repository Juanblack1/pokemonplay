# Atualizações pelo GitHub

## v171.3.0 — Primeiro jogo com menos erros

A visão geral da Biblioteca orienta a adicionar ROMs quando não há jogos locais disponíveis, oferece arquivo ou pasta e explica que o emulador precisa ser configurado e o login é opcional. O importador mostra somente jogos-base do console selecionado e sugere o nome do arquivo, preservando nomes de hacks com cabeçalhos herdados. A edição mantém o título escolhido e oferece **Salvar**.

ROMs importadas com nomes iguais aos títulos legados agora usam o nome do seu próprio arquivo para localizar saves `.sav` e `.srm`. O PR #12 passou pela suíte completa Windows, empacotamento e 79 verificações de isolamento da nuvem (run `37043170957`); 19 novas verificações cobrem o primeiro uso. As fixtures da suíte foram removidas automaticamente ao final no runner.

## v171.2.0 — Adicionar ROMs locais e verificar isolamento da nuvem

A Biblioteca pode importar ROMs GBA, Nintendo DS e Nintendo 3DS ou examinar uma pasta, mantendo os arquivos no local escolhido. Cabeçalhos GBA/DS ajudam a preencher título e jogo-base; arquivos sem identificação, incluindo títulos 3DS, exigem confirmação do conteúdo Pokémon. Os cartões de hack mostram o selo **HACK ROM**, o nome escolhido e o jogo-base. Saves locais ganham uma pasta isolada por jogo importado.

As regras ativas do Firestore foram verificadas em 79 testes locais de acesso, com dois UIDs e visitantes anônimos, incluindo os saves e o banco global Pokémon. O CI passa a repetir essa suíte. Os pacotes públicos continuam sem ROMs, BIOS ou emuladores de terceiros, e a assinatura Windows permanece opcional nesta etapa.

## v171.1 — Abrir o app direto da pasta extraída

A release inclui um pacote portátil com `Pokemons Play.exe` na raiz. Extraia o ZIP e abra esse iniciador para começar; o runtime autocontido continua dentro da pasta do app e o pacote de atualização permanece separado.

## v171 — Simplificar a publicação segura

O login Google usa o fluxo nativo com PKCE sem enviar um segredo OAuth embutido no aplicativo. O workflow de release executa a suíte ProfilesCheck antes de compilar e publicar o pacote Windows autocontido.

## v170 — Evitar sobreposição do resumo da ordenação

Na Biblioteca, o texto que explica a ordenação só aparece quando há espaço depois dos filtros e ações. Ele se reposiciona quando a ordenação muda ou quando o botão de limpar o histórico aparece, mantendo a barra legível em larguras intermediárias.

## v169 — Consultar os cores compatíveis com conquistas

Configurações agora abre a lista oficial atual de sistemas e cores compatíveis com RetroAchievements. A seleção automática prioriza mGBA para GBA e melonDS DS para Nintendo DS, sem alterar uma escolha válida já salva.

## v168 — Abrir o RetroArch para configurar a conta

Configurações ganhou o atalho **Abrir RetroArch · configurar conta**. Ele abre o RetroArch sem iniciar um jogo e orienta a ativar a conta em **Configurações > Conquistas**; o Pokemon Play não guarda a senha.

## v167 — Restaurar o atalho personalizado do Azahar

Retomar pode usar F4 durante uma sessão 3DS mesmo quando o usuário personalizou o atalho Continuar/Pausar no Azahar. A configuração temporária restaura o atalho original e mantém as demais preferências.

## v166 — Limpar a configuração temporária do Azahar após falhas

Se a preparação da sessão 3DS falhar ao gravar as preferências temporárias, o Pokemon Play também remove o marcador e a pasta vazia da sessão.

## v165 — Jogar títulos 3DS dentro do launcher

As sessões do Azahar usam a área de jogo integrada, mantendo a barra e a navegação do Pokemon Play visíveis. Voltar ao menu pausa a sessão; **Retomar** usa F4 depois que o launcher recupera o foco. As preferências originais do emulador são restauradas ao encerrar ou recuperar uma sessão interrompida.

## v164 — Recuperar sessões interrompidas

Se o Pokemon Play ou o computador fechar durante uma sessão, o próximo início restaura as configurações temporárias do melonDS e VBA-M e limpa arquivos de sessão abandonados do RetroArch. A recuperação só altera marcadores quando o emulador correspondente não está rodando.

## v163 — Sessões GBA integradas ao launcher

Jogos GBA no VBA-M agora usam a mesma área de jogo integrada e os controles **Pausar · Menu** e **Retomar**. A pausa automática ao perder foco é ativada temporariamente nas configurações do VBA-M e os valores originais são restaurados ao encerrar.

## v162 — Sessões Nintendo DS dentro do launcher

Jogos de Nintendo DS no melonDS agora abrem dentro da área principal do Pokemon Play. A navegação e a barra superior permanecem disponíveis: **Pausar · Menu** volta à Biblioteca e **Retomar** traz a sessão de volta. A configuração de pausa ao perder foco é ativada somente durante a sessão e seu valor anterior é restaurado ao encerrá-la.

## v161 — Evitar cortes nos controles da sessão de jogo

Na largura mínima, um nome de jogo longo podia empurrar as ações da barra superior para fora da janela. **Retomar** agora mantém largura controlada, o nome completo continua disponível para leitores de tela e o título do topo cede espaço quando necessário.

## v159 — Anunciar qual jogo está pausado

Ao retornar do RetroArch, a ação da Biblioteca mostra o título do jogo e recebe o foco de teclado. O nome acessível das ações de retomar e encerrar também informa o jogo e o perfil ativos.

## v158 — Fechar o Pokemon Play com uma sessão pausada

Ao sair do aplicativo enquanto uma sessão do RetroArch está pausada, o Pokemon Play conclui o fluxo de confirmação do emulador e fecha a própria janela principal quando a sessão termina. Se o fechamento do jogo for cancelado, o aplicativo continua aberto.

## v157 — Atualizar o histórico ao pausar a sessão

Ao voltar à Biblioteca, o Pokemon Play grava o tempo ativo acumulado até a pausa e atualiza imediatamente os cartões de Recentes e Mais jogados. O restante da sessão é somado ao retomar e encerrar.

## v156 — Contar apenas o tempo ativo no RetroArch

O tempo de jogo para de avançar quando a janela do RetroArch perde o foco, inclusive ao voltar à Biblioteca, e recomeça ao retomar a sessão. As estatísticas de Recentes e Mais jogados deixam de incluir o tempo em que o jogo está pausado.

## v155 — Encerrar uma sessão pausada pela Biblioteca

Quando um jogo do RetroArch fica pausado na Biblioteca, há ações separadas para retomar ou encerrar a sessão. Encerrar usa a confirmação de fechamento existente; cancelar preserva o jogo e deixa as ações disponíveis.

## v154 — Pausar e retomar a sessão do RetroArch pelo menu

Durante um jogo aberto no RetroArch, F12 ou o botão **Pausar · Menu** retorna à biblioteca sem encerrar a sessão. O RetroArch pausa ao perder o foco; a barra superior do Pokemon Play mostra **Jogo pausado · Retomar** para voltar à partida. Fechar a janela do jogo ainda encerra a sessão após confirmação.

## v153 — Detectar cores do RetroArch automaticamente

Ao escolher o executável do RetroArch, o Pokemon Play procura cores compatíveis para GBA e Nintendo DS na pasta `cores` ao lado dele. As sugestões priorizam mGBA e melonDS DS, preenchem apenas seleções vazias ou inválidas e não ativam os sistemas automaticamente.

## v152 — Configurar RetroAchievements em GBA e DS

Nas Configurações, escolha o RetroArch e os cores Libretro para GBA e Nintendo DS. Os jogos selecionados abrem pelo core configurado; o Pokemon Play ativa as conquistas e notificações de desbloqueio no RetroArch, sem guardar credenciais ou alterar a preferência de modo hardcore da conta. Saves SRAM `.srm` e importações continuam ligados ao perfil ativo. Os emuladores atuais seguem como padrão quando a opção fica desativada.

## v151 — Liberar as capas ao sair das telas de saves

As capas carregadas na lista e no detalhe de **Meus saves** agora são cópias em memória; a imagem continua visível sem manter o PNG/JPEG de origem bloqueado. Biblioteca, lista e detalhe compartilham o mesmo carregador, que mantém o fallback para arquivos ausentes ou inválidos.

## v150 — Mostrar sprites nos slots do save

Slots ocupados de equipes e caixas GBA/DS agora carregam o sprite do Pokémon ao lado do nome, nível e identificador do slot. O cartão mantém uma Pokébola enquanto o sprite baixa, usa cache local e anuncia o Pokémon por acessibilidade. Os slots vazios preservam o layout atual.

## v149 — Ampliar o sprite selecionado no Banco Pokémon

Ao selecionar um Pokémon no Banco global, a lateral mostra uma prévia maior do mesmo sprite usado no cartão, com estado visual enquanto carrega e descrição acessível. A prévia acompanha a seleção e os filtros e libera a imagem anterior ao atualizar a grade.

## v148 — Tornar busca e filtro mais claros para leitores de tela

O campo **Buscar jogos** anuncia sua finalidade tanto no controle visual quanto na caixa de texto que recebe o foco. O seletor informa que filtra por sistema e mantém esse contexto ao trocar entre GBA, Nintendo DS e Nintendo 3DS; a descrição explica que os filtros podem ser combinados.

## v147 — Limpar o histórico na lista Mais jogados

Na Biblioteca, **Mais jogados** agora oferece uma ação para limpar o histórico de jogo e os tempos acumulados. A confirmação explica que as datas de acesso e as durações também serão apagadas; o nome acessível do botão acompanha o modo atual. A ação fica disponível enquanto houver histórico, inclusive quando os filtros deixam a lista vazia.

## v146 — Ordenar pela duração total jogada

A Biblioteca ganhou o filtro **Mais jogados**, que mostra os jogos com tempo registrado do maior total para o menor. Busca, sistema e favoritos continuam funcionando junto com a ordenação; cada cartão mostra o total considerado. Jogos antigos sem duração medida não aparecem até uma sessão ser contabilizada. O chip se ajusta ao espaço disponível sem cortar o rótulo.

## v145 — Mostrar tempo total jogado

O histórico **Recentes** agora soma o tempo de cada sessão e mostra o total no cartão, junto à data da última abertura. A duração é registrada quando o emulador encerra; históricos existentes continuam válidos e indicam quando ainda não há tempo medido. Ao voltar do jogo, a lista de Recentes é atualizada automaticamente. A ação **Limpar recentes** remove datas e tempos armazenados localmente.

## v144 — Abrir a pasta para adicionar capas ausentes

Cartões de jogos sem capa agora oferecem **Abrir pasta da capa**. A ação abre a pasta que corresponde ao caminho sugerido e cria a pasta automaticamente se ela ainda não existir; cartões com capa válida não exibem esse botão.

## v143 — Explicar quando não há espécies repetidas

Quando os filtros não encontram espécies repetidas, o Banco global agora explica esse resultado e mantém a ação de limpar filtros disponível. A descrição considera busca, geração e os demais filtros ativos.

## v142 — Evitar colisões entre ROMs 3DS com nomes normalizados iguais

ROMs que resultam no mesmo nome depois da normalização agora usam o caminho relativo completo para diferenciar o título e a pasta de save. Isso também separa arquivos da mesma pasta em formatos diferentes, sem mudar a busca de capas planas ou espelhadas.

## v141 — Encontrar espécies repetidas no Banco

O filtro de espécies repetidas mostra todos os Pokémon de espécies presentes mais de uma vez entre os resultados atuais, ou reduz os resultados a um Pokémon por espécie. Ele combina com busca, geração, formato e os outros filtros.

## v140 — Separar ROMs 3DS com nomes iguais

Quando ROMs 3DS com o mesmo nome ficam em subpastas diferentes, a Biblioteca agora diferencia os títulos e as pastas de save pela localização. A busca de capas acompanha essa estrutura e continua aceitando capas antigas salvas na raiz.

## v139 — Mostrar espécies distintas no Banco global

O resumo da coleção agora separa o total de arquivos Pokémon válidos da quantidade de espécies diferentes. Repetidos contam como Pokémon, sem aumentar o total de espécies.

## v138 — Tolerar o encerramento durante a integração do emulador

As leituras de processo e janela agora tratam processos que encerram ou perdem a janela entre as consultas. A busca também libera todos os handles que não foram escolhidos, mesmo quando um candidato falha durante a leitura.

## v137 — Recuperar falhas ao iniciar o emulador

Se o Windows não conseguir abrir um emulador existente, o launcher mostra instruções na própria janela. O erro não registra o jogo em Recentes; voltar ao menu fecha a tela sem pedir confirmação de encerramento.

## v136 — Anunciar o jogo no botão de favorito

O botão de estrela informa o nome do jogo associado desde que o cartão aparece, antes de qualquer interação. Assim, cada favorito continua identificável por leitores de tela ao percorrer a Biblioteca.

## v135 — Navegar entre gerações com as setas

Na Biblioteca, as setas continuam movendo o foco entre cartões mesmo quando o destino está em outra seção de geração. A escolha considera a posição visual dos cartões na tela.

## v134 — Navegar entre jogos pelas setas

Com um cartão de jogo focado na Biblioteca, as setas movem o foco para o cartão vizinho mais próximo na direção pressionada. `Enter` e `Espaço` continuam iniciando o jogo, e o foco rola para manter o destino visível.

## v133 — Iniciar jogos pelo teclado

Cartões de jogo focados agora recebem `Enter` e `Espaço` como comandos de ação no WinForms. O lançamento continua usando o fluxo de confirmação de perfil e respeita o cancelamento.

## v132 — Manter o foco ao alterar favoritos

Marcar ou remover um favorito sem filtro mantém o cartão e o foco no lugar. Na visualização filtrada, o foco segue para o próximo jogo disponível; ao remover o último favorito, vai para **MOSTRAR TODOS**.

## v131 — Navegar entre páginas do Banco pelo teclado

Com o foco em um cartão do Banco global, `PageDown` abre a próxima página e `PageUp` volta à anterior. O primeiro Pokémon da página passa a ficar selecionado e focado; nos limites da coleção, o atalho não altera a seleção.

## v130 — Limpar buscas com Esc

Na Biblioteca e no Banco global, `Esc` apaga a consulta ativa quando o campo de busca está focado, mantendo os outros filtros e o foco no campo. O download do atualizador também deixa de depender do loop de interface durante leitura, gravação e extração.

## v129 — Atalho de busca no Banco Pokémon

`Ctrl+F` coloca o cursor diretamente na busca do Banco global, seguindo o atalho já disponível na Biblioteca.

## v128 — Busca sem acentos no Banco Pokémon

A busca por espécie, apelido e nome de arquivo agora ignora diferenças de acentuação, seguindo o mesmo comportamento da Biblioteca. Por exemplo, `flabebe` encontra o apelido `Flabébé`.

## v127 — Caminho claro para capas 3DS ausentes

Quando um jogo 3DS ainda não tem capa, a Biblioteca informa o nome de arquivo PNG sugerido dentro de **Pokemon 3DS - Capas**. Capas correspondentes em PNG têm prioridade estável; JPG e JPEG também continuam aceitos.

## v126 — Filtros legíveis em janelas compactas

Os filtros por geração da Biblioteca agora ocupam mais de uma linha quando o espaço horizontal é curto. Os rótulos continuam legíveis ao lado do botão Atualizar, e a ação pelo atalho F5 permanece disponível.

## v125 — Recuperar a Biblioteca após estado vazio

A ação **MOSTRAR TODOS** agora reconstrói a lista imediatamente depois de limpar busca, filtros e modo de visualização, mesmo quando alguns desses controles já estavam no padrão.

## v124 — Atualizar jogos sem reiniciar

A Biblioteca agora tem o botão **Atualizar** e o atalho **F5** para reler jogos 3DS adicionados ou removidos e recarregar as capas. A busca e os filtros continuam aplicados; o cabeçalho informa a hora da atualização e a quantidade atual de jogos.

## v122 — Tipos visíveis nos cartões do Banco

Os cartões do Banco global exibem etiquetas coloridas dos tipos primário e secundário. A mesma lista localizada alimenta as etiquetas e o filtro por tipo; cartões acessíveis anunciam os tipos completos.

## v121 — Sprites no Banco Pokémon

O Banco global mostra o sprite da espécie nos cartões. Os PNGs vêm do repositório de sprites do PokeAPI; variantes shiny e fêmea são priorizadas quando existem, com fallback para o sprite padrão. As imagens são carregadas sem bloquear a interface e ficam em cache local limitado a 512 combinações. Sem internet ou quando um sprite não está disponível, o cartão mantém um Poké Ball de reserva e todas as informações do Pokémon. Capas inválidas não derrubam a biblioteca, os arquivos carregados ficam desbloqueados e o cartão informa o caminho esperado quando a arte falta.

## v120 — Atalho para buscar jogos

`Ctrl+F` leva o foco para a busca da biblioteca, inclusive quando o foco está em um filtro ou cartão.

## v119 — Busca por títulos com ou sem acentos

A busca da biblioteca ignora diferenças de acentuação: digitar `pokemon x` encontra uma ROM chamada **Pokémon X**, sem mudar o título exibido. A busca com acentos continua funcionando.

## v118 — Horário da última sessão nos jogos recentes

Os cartões de **Recentes** mostram a data e hora local da última abertura, mantendo o histórico armazenado em UTC. A informação também fica disponível como descrição acessível do cartão.

## v117 — Encontrar jogos em ordem alfabética

A biblioteca ganhou a opção **A–Z**, que reúne títulos de todas as gerações em uma única lista alfabética. Busca, sistema e favoritos continuam funcionando juntos, e a barra se reorganiza para caber em telas menores.

## v116 — Controle do histórico de jogos recentes

O filtro **Recentes** agora oferece **Limpar recentes**. Após confirmação, os registros locais são removidos e a biblioteca mostra o estado vazio; nenhum dado é enviado para a nuvem. A barra reorganiza busca, sistema e favoritos para caber quando a janela está no tamanho mínimo.

## v115 — Acesso rápido aos jogos recentes

A biblioteca ganhou o filtro **Recentes**, que lista primeiro os jogos abertos por último e pode ser combinado com busca, sistema e favoritos. O histórico fica apenas no arquivo local `Settings/GameLaunchHistory.json`, guarda até 50 jogos e só é atualizado quando o emulador inicia. A tela também explica quando ainda não há jogos recentes.

## v114 — Ordenar o Banco pela data de captura

O Banco Pokémon ganhou a opção **Captura · recentes**. Pokémon com uma data válida aparecem do mais novo para o mais antigo; arquivos de gerações ou espécies sem data ficam depois dos itens datados. A data também aparece nos detalhes do Pokémon selecionado. O recurso usa os dados do próprio Pokémon e não altera os arquivos da coleção.

A partir da v113, o usuário recebe um botão com ícone e **Atualização disponível** quando há uma release estável mais nova. A verificação ocorre ao abrir o aplicativo e a cada seis horas enquanto ele está aberto. O botão **Atualizações** permite verificar manualmente, ver as novidades e escolher **Agora não** ou **Atualizar e reabrir**. O download pode ser cancelado.

O repositório precisa ser público. Configure seu endereço na janela Atualizações, ou distribua `PokemonPlayRuntime/UpdateSource.json` contendo `{"repository":"usuario/repositorio"}`. A configuração escolhida pelo usuário fica em `Settings/Updates.json`. Nenhum endereço de repositório foi inventado para esta instalação.

## Publicar uma versão nova

Versione o código-fonte e o workflow `.github/workflows/release-update.yml` no repositório escolhido. Crie uma tag numérica estável, por exemplo `v122` ou `v122.1.0`, e envie essa tag ao GitHub. O workflow compila o aplicativo, inclui o helper de atualização e publica `pokemon-play-win-x64-update.zip` na release. A versão compilada é derivada da tag. As próximas atualizações contêm só o runtime, sem ROMs, emuladores, saves, banco ou configurações pessoais.

Para gerar o pacote manualmente em uma máquina de desenvolvimento com o SDK .NET 10:

```powershell
./recovered-source/tools/publish-update.ps1 -Tag v122 -Repository usuario/repositorio -OutputDirectory C:/Releases/v122
```

Envie o ZIP e seu `.sha256` à release correspondente. O GitHub precisa fornecer o campo `digest` SHA-256 do asset para a atualização automática; releases sem esse campo ou sem o ZIP correto são recusadas. Releases draft, prerelease, mais antigas ou com versão inválida não são instaladas.

## Aplicação da atualização

O app confere a origem, tamanho e SHA-256 do download, valida os caminhos e o manifesto do ZIP e extrai em uma pasta temporária na própria instalação. Após o usuário aceitar, verifica se há jogos abertos e permite salvar ou cancelar alterações pendentes no banco. Um helper separado aguarda o processo fechar, troca apenas `PokemonPlayRuntime` e reabre o aplicativo. A versão anterior é restaurada se a troca falhar ou se a versão nova não confirmar sua abertura. Após abertura confirmada, o app limpa o pacote temporário e a cópia anterior do runtime.

A v113 precisa entrar uma vez na instalação atual para acrescentar esse mecanismo. Depois, o usuário final não compila nem reinstala a coleção a cada release. O aplicativo continua utilizável quando a verificação falha ou o computador está offline. Um repositório privado requer um serviço público de distribuição ou outra estratégia de autenticação; nenhum token GitHub é embutido no aplicativo.
