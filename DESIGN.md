# Sistema visual — Pokemons Play v26 · Pixel

## Biblioteca — botões e rolagem

A coleção usa fundo opaco na paleta existente e painéis com buffer de desenho. A área rolável acompanha a altura real do conteúdo; cada linha reserva espaço para seu cartão mais alto, incluindo hacks e histórico. Textos respeitam o recorte durante a repintura. O favorito tem alvo de 36 px e estrela desenhada, independente dos glifos da fonte pixel, preservando os nomes acessíveis e a navegação por teclado.

## Configurações — largura disponível

O canvas acompanha a área visível. Em áreas estreitas, as ações de controle ocupam uma segunda linha e os cartões de telas DS e áudio ficam empilhados. O cabeçalho, os textos auxiliares e os caminhos respeitam a largura de seus cartões. As atribuições mantêm sua própria rolagem vertical para os 12 botões do DS; os botões de salvar e restaurar ficam no rodapé. Capturar uma atribuição reserva espaço para cancelar sem cobrir o diagnóstico. Mantém paleta, tipografia e desenho Pixel existentes.

## Biblioteca — ROMs locais e hacks Pokémon

### Recuperar ROM movida

**Adicionar jogos → Gerenciar jogos importados** apresenta inclusive entradas com arquivo não encontrado. A janela mantém lista, caminho selecionável e ação **Localizar ROM**; a confirmação vincula o novo caminho ao mesmo jogo. O seletor e o retorno explicam que o nome do arquivo deve ser o mesmo para manter a associação com o save. Resultados completos permanecem disponíveis para acessibilidade e em tooltip. A janela usa a paleta e a tipografia existentes e adapta a lista à altura disponível; nenhum arquivo é movido pelo app.

### Primeiro uso

Quando não há ROM importada disponível nem executável local de jogo, a visão geral da Biblioteca oferece **Comece adicionando um jogo** e abre as opções de arquivo ou pasta. A orientação explica emulador e login opcional; desaparece durante filtros e para instalações já configuradas. Mantém os cartões de referência do catálogo e a identidade Pixel existente. No importador, o jogo-base fica limitado ao console do arquivo e o nome sugerido vem do arquivo para respeitar hacks com cabeçalhos herdados. Ao editar, a ação é **Salvar**.

**Adicionar jogos** abre opções para selecionar ROMs GBA, Nintendo DS e Nintendo 3DS ou examinar uma pasta, sem copiar os arquivos. Cada arquivo passa por uma etapa curta de classificação. Cabeçalhos GBA/DS com Pokémon ajudam a preencher título e jogo-base; títulos 3DS e arquivos sem identificação exigem confirmação explícita de conteúdo Pokémon. A pessoa pode marcar a opção de hack e escolhe o nome e o jogo-base. Cartões de hack exibem **HACK ROM** e “Hack de [jogo-base] · [sistema]”, com a capa do jogo-base como referência. O catálogo guarda apenas caminho e metadados em `Settings/ImportedPokemonGames.json` e mantém os saves em pasta própria por entrada. Os controles seguem a paleta Pixel, com texto auxiliar e estados de foco acessíveis.

## Barra superior v161 — nomes longos sem colisão

A ação de retomar mantém largura visual máxima de 180 px e preserva o nome completo do jogo e perfil para leitores de tela. O título da página usa reticências quando necessário e some quando sobra menos de 80 px, deixando o espaço para os controles principais. Durante o jogo, o nome completo do título continua visível quando as ações de sessão estão ocultas.

## Jogo v160 — RetroArch integrado ao Pokemon Play

Jogos GBA/DS iniciados pelo RetroArch ocupam o painel central do launcher; a barra superior, a navegação e o contexto de jogo/perfil permanecem visíveis. Pausar retorna à Biblioteca com a sessão em pausa e uma ação de retomada na barra principal. Navegar para outra tela durante a sessão também pausa e preserva o botão para retomar. A janela do RetroArch abre em modo janela sem bordas para caber no painel. A identidade pixel e a distribuição atual das barras permanecem intactas.

## Biblioteca v159 — Retorno acessível à sessão

Ao pausar uma sessão, a ação de retomada informa jogo e perfil e recebe foco para teclado e leitores de tela; encerrar anuncia o mesmo contexto.

## Sessão v158 — Saída coordenada

Fechar o Pokemon Play com uma sessão pausada aguarda a decisão e o encerramento do jogo; a saída original continua automaticamente após o fechamento confirmado.

## Biblioteca v157 — Histórico atualizado ao pausar

Os cartões de atividade são reconstruídos quando uma sessão retorna à Biblioteca, incluindo o tempo de jogo recém-pausado.

## Biblioteca v156 — Tempo ativo de jogo

O total registrado para a sessão do RetroArch acompanha o estado de foco: pausas na Biblioteca ou em outra janela deixam de inflar o tempo mostrado em Recentes e Mais jogados.

## Biblioteca v155 — Ações da sessão pausada

Com o jogo pausado no RetroArch, a barra principal oferece **Jogo pausado · Retomar** e **Encerrar jogo**. As ações desaparecem ao retomar ou fechar a sessão.

## Sessão de jogo v154 — Retomar RetroArch

O host mantém o jogo incorporado, oculta a sessão ao retornar à biblioteca e mostra uma ação acessível na barra principal para retomá-la. Para outras opções de emulador, o botão mantém a confirmação de encerramento atual.

## Configurações v153 — Detecção de cores do RetroArch

O cartão de RetroAchievements explica que localizar o executável também procura cores compatíveis na pasta `cores`; a seleção manual continua disponível e a detecção não ativa o RetroArch por geração.

## Configurações v152 — RetroAchievements

Um cartão opcional permite ativar RetroArch separadamente para GBA e DS, localizar o executável e escolher um core Libretro por sistema. O texto avisa que login e notificações pertencem ao RetroArch; os arquivos `.srm` continuam no perfil de save ativo. Com as opções desativadas, a tela mantém o fluxo de emuladores existente.

## Meus saves v151 — Capas sem bloqueio de arquivo

Os cartões da lista e o detalhe mantêm a capa visível a partir de uma cópia independente em memória. Arquivos ausentes ou inválidos seguem o fallback atual; trocar ou remover uma capa não exige fechar a tela.

## Saves GBA/DS v150 — Sprites nos slots

Os slots ocupados mostram uma arte de 42 px à esquerda do nome e dos fatos do Pokémon; a Pokébola existente funciona como placeholder durante a carga assíncrona. O cache compartilhado com o Banco global evita baixar novamente a mesma variante. Slots vazios continuam com o texto de espaço disponível.

## Banco global v149 — Prévia ampliada do Pokémon selecionado

A lateral do Banco global mostra o sprite selecionado em uma área de 112 px, preservando a transparência do PNG e exibindo uma Pokébola enquanto a imagem carrega. A descrição acessível acompanha o cartão selecionado; alterar filtros limpa a prévia anterior antes de liberar os cartões da grade.

## Biblioteca v148 — Contexto acessível nos filtros

O campo de busca anuncia que filtra título/plataforma e ignora acentos; o nome é aplicado também à caixa de texto interna que recebe foco. O seletor combina o contexto **Filtrar por sistema** com a opção atual e mantém a descrição de filtros combináveis.

## Biblioteca v147 — Limpar tempos jogados

O modo **Mais jogados** oferece **Limpar histórico** quando há entradas salvas. A confirmação deixa claro que a ação também apaga datas recentes e tempos acumulados; leitores de tela recebem o mesmo contexto. Fora das listas **Recentes** e **Mais jogados**, o controle fica oculto.

## Biblioteca v146 — Mais jogados

O chip **Mais jogados** ordena somente títulos com duração contabilizada, do total maior ao menor; empates seguem a última abertura. Busca, sistema e favoritos continuam combináveis. Os cartões mostram o total usado na ordenação. Chips medem o texto real e quebram em linhas pelo espaço disponível, sem cortar os rótulos nem invadir **Atualizar**.

## Biblioteca v124 — atualização do catálogo

Um botão Atualizar fica junto aos filtros por geração, com atalho F5 em qualquer largura. No modo compacto o botão recolhe sem sobrepor os filtros; F5 permanece disponível. Após a leitura, o cabeçalho mostra hora local e quantidade de jogos. Busca e filtros atuais são preservados.

## Banco global v122 — tipos nos cartões

Os cartões mostram etiquetas pixel com cores por tipo, primário e secundário, em uma linha própria abaixo do nome. O filtro por tipo usa os mesmos rótulos localizados. Detalhes acessíveis anunciam os nomes completos; Pokémon de tipo único não exibem uma segunda etiqueta.

## Banco global v121 — sprites

Cada cartão de Pokémon mostra o sprite frontal da espécie em uma área fixa à esquerda, ao lado do nome e dos metadados. Variantes shiny e fêmea são priorizadas quando disponíveis e usam fallback para o sprite padrão. A busca é assíncrona; o cache fica em LocalAppData e é limitado a 512 imagens. Sem conexão, o Poké Ball desenhado continua visível. O sprite não substitui a identificação textual nem altera o arquivo do Pokémon. Cartões de jogos informam o caminho esperado da capa ausente; imagens inválidas usam o mesmo fallback sem travar a biblioteca e não mantêm os arquivos de origem bloqueados.

## Atualizações v113

O topo mantém o botão Atualizações com ícone de download desenhado na paleta pixel. Quando há uma release estável mais nova, muda para Atualização disponível em ciano/azul. A janela exibe versão atual, origem GitHub, versão disponível, novidades e ações Ver release, Atualizar e reabrir e Agora não. Download tem progresso e cancelamento. Repositório ausente ou falha de rede oferece configuração/verificação manual; não impede usar o aplicativo. Contrato de distribuição e preservação dos dados em UPDATES.md.

## Filtros e Pokédex v112

Geração da espécie usa a introdução da espécie (Gen 1–9), independentemente do formato PK3–PK9. Pokédex regional usa listas por jogo: Kanto FR/LG, Hoenn RSE/ORAS, Johto HGSS, Sinnoh DP/Platinum, Unova BW/B2W2, Kalos XY e Alola SM/USUM. As listas têm espécies antigas e novas conforme a Pokédex daquele jogo; não indicam disponibilidade de captura nem compatibilidade de transferência. A opção Nacional do save aberto usa o limite de espécies do save compatível. Os filtros se combinam; resultados vazios permitem limpar todas as opções.

Busca, geração da espécie, Pokédex regional e ordenação têm rótulos acima dos campos. Mais filtros troca a coluna de detalhes por filtros com rolagem independente: formato, tipo (incluindo o secundário, conforme o arquivo), shiny, sexo, ovos e intervalo de nível. Ao fechar o painel, o botão mostra a quantidade de filtros adicionais ativos. Ordenação inclui nome, número crescente/decrescente, nível crescente/decrescente, geração da espécie, formato e mais recentes. Cartões e detalhes distinguem geração da espécie e formato.

## Banco global v111

A coleção ocupa a área principal. A barra reúne o seletor de coleção, Importar Pokémon, Abrir save e Ferramentas do banco. Perfis e nuvem aparecem sob demanda; ZIP, pasta e restauração ficam no menu de ferramentas. Busca, geração e ordenação quebram linha conforme a largura. A paginação fica abaixo da coleção com página e intervalo de resultados. Os cartões distinguem número da espécie, geração, nome e nível. A seleção preserva a rolagem e o foco; detalhes e ações compartilham uma coluna com rolagem em janelas menores. Mantém a identidade pixel e a coleção local independente dos saves.

Componentes nativos WinForms com a organização e medidas corrigidas na v25, mantendo a identidade pixel art do design gerado com base no PKForge, conforme correção do usuário. Capas existentes preservadas. A estética zinc e os botões arredondados da v25 foram substituídos pela direção original.

## Componentes

- Fundo azul-marinho com grade sutil, superfícies índigo, bordas azuis, foco ciano e títulos de geração em amarelo.
- Pixelify Sans embutida para títulos, botões, navegação e rótulos. Segoe UI no corpo e nas descrições para preservar legibilidade. Licença OFL distribuída com o aplicativo.
- Botões: altura de 40 px, padding horizontal de 16 px, cantos em degraus de 4 px e estados de hover, pressionado, foco e desabilitado.
- Largura preferida calculada pelo texto, sem reduzir a fonte para encaixar o rótulo.
- Cards com padding de 24 px e cabeçalhos em controles Label com área própria.
- Seletores desenhados pelo aplicativo, com menus de contexto e suporte a teclado.
- Busca mantém a edição nativa dentro de um campo com borda e espaçamento próprios.
- Navegação inferior em quatro blocos, retomando a composição do mockup PKForge. Salvar e restaurar configurações no rodapé fixo.
- Formulários empilhados em janelas estreitas; barras de ações quebram linha.
- Os quatro mockups em Assets/Designs são a referência visual. Cabeçalhos com grade e ícones em pixel, sem o cenário cartoon da v24.
- Diálogos com rótulos acima dos campos e ações separadas da edição.

## Evidência visual

## Sessão Nintendo DS integrada v162

O melonDS ocupa a mesma área de jogo usada pelo RetroArch; a barra superior e a navegação da Biblioteca continuam no launcher. Ao sair para o menu, o emulador perde foco e pausa; retomar restaura a sessão e permite o retorno ao jogo. O valor original de `PauseLostFocus` volta ao TOML ao fechar a sessão.

## Sessão GBA integrada v163

O VBA-M usa a mesma área de jogo do launcher. **Pausar · Menu** leva à Biblioteca sem encerrar o progresso e **Retomar** devolve a sessão ao painel. `pauseWhenInactive` só fica ligado durante o jogo e é restaurado nas configurações INI usadas pela instalação.

## Recuperação de sessão v164

Marcadores temporários deixados por uma queda do app ou do sistema são processados na próxima abertura. O Pokemon Play preserva a configuração original, limpa os marcadores recuperados e deixa sessões de emuladores que ainda estão abertos em paz.

## Controles interativos v27

Modo Operate, preservando a identidade v26. Configurações coloca a visualização de controle ao lado das atribuições, com rolagem própria da lista para manter o desenho inteiro visível. Testar fica acessível no topo. Os botões virtuais usam o mesmo desenho na janela do jogo; pressão desloca o botão e muda a cor, analógicos seguem os eixos e gatilhos mostram intensidade. Os textos de instrução usam Segoe UI. Contratos e limitações em CONTROLS.md; prévias em output/v27-confirmed.

## Teste por console v28

Seletor de GBA/DS/3DS em Configurações e janela própria de teste com área ampla. Desenhos em pixel com proporções de tela de cada portátil: GBA horizontal violeta, DS dobrável índigo, 3DS com tela superior larga e acabamento azul petróleo. Pressão muda a cor e posição do botão. Circle Pad do 3DS acompanha as direções. Telas inferiores de DS/3DS têm grade para localizar o toque e cruz de contato. As prévias com botões e toque ativos são identificadas como ilustrativas. Não usar o antigo gamepad genérico como visual desses testes.

## Imagens v29

Carcaças substituídas por sprites transparentes gerados com Imagegen, com contornos de hardware, dobradiças, relevos e brilho em pixel. Permanecem a paleta violeta/índigo/azul petróleo e as regiões interativas desenhadas pelo aplicativo. Sprites embutidos e prompts em Assets/Consoles. Imagens finais integradas em output/v29-confirmed.

Prévia dos controles reais em `output/v26-pixel/desktop` e `output/v26-confirmed`: telas, posições com rolagem e diálogos. São renderizações DrawToBitmap, não testes de integração de emuladores ou login de nuvem.
