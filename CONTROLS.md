# Controles interativos · v27

## Proteção automática de saves · v30

- Antes de abrir um jogo, o launcher cria um ZIP na pasta `Saves/Backups/Automaticos` quando já existem saves e o emulador correspondente está fechado.
- Uma impressão SHA-256 do conteúdo evita repetir cópias idênticas; mudanças no nome, conteúdo ou conjunto de arquivos geram outro snapshot.
- O ZIP é criado como temporário, o conteúdo é conferido novamente e só então ele é publicado com nome final. Se o snapshot falhar ou mudar no meio da cópia, a abertura do jogo pede confirmação e começa com “Não” selecionado.
- Se o emulador já estiver aberto, o app avisa que não conseguiu criar o snapshot antes de oferecer a abertura. Backups manuais e restauração da nuvem continuam disponíveis.
- Compilação e pacote autocontido v30 concluídos. O aplicativo instalado abriu e respondeu com o título v30; o backup automático ainda não foi exercitado dentro de uma sessão de jogo.

## Revisão de textos e backups manuais · v31

- As instruções para DS/3DS agora separam pressionar botões do gesto na tela inferior.
- O backup manual usa milissegundos no nome para evitar colisão quando criado mais de uma vez no mesmo segundo.
- O subtítulo da tela “Meus saves” avisa que o backup automático é feito ao iniciar jogos.
- Publicação autocontida instalada e aberta como `Pokemons Play · v31 · Pixel`. Pacote com 373 entradas; hashes das capas, saves, configurações e cloud idênticos antes/depois da instalação.
- Prévias desktop (1280×820) e compacta (1000×720) das telas do app em `output/v31-review-desktop` e `output/v31-review-compact`.
- A abertura real de uma ROM, os gestos de toque e a criação/restauração de backups ainda precisam de verificação interativa.

## Visual do GBA · v32

- O destaque visual dos botões L/R no GBA fica alinhado aos ombros do sprite; as áreas de clique continuam amplas para uso confortável.
- Publicação autocontida, pacote de 373 entradas e instalação concluídos. A prévia compacta do teste GBA foi renderizada e o app abriu respondendo com título v32.

## Encerramento seguro do emulador · v33

- O botão de voltar e F12 pedem primeiro o fechamento normal do emulador e aguardam a gravação/saída por até 15 segundos.
- Se o emulador não sair, o encerramento forçado requer uma segunda confirmação, com “Não” como opção padrão; recusar mantém o jogo aberto.
- O F12 agora pertence à janela de jogo. A rota antiga no launcher encerrava o processo diretamente.
- Fechamento com uma ROM real ainda precisa de confirmação interativa; esta rodada cobre implementação, não uma sessão de jogo.
- Compilação autocontida v33 e pacote de 373 entradas verificados; capas e dados do usuário permaneceram iguais durante a instalação.

## Restauração de backup local · v34

- A tela “Meus saves” permite escolher um backup ZIP local para restaurar.
- A restauração é bloqueada quando o emulador desse jogo está aberto, extrai para uma pasta temporária e só troca a pasta atual depois de conferir que o ZIP contém arquivos.
- Antes da troca, o estado local existente é salvo em `Saves/Backups/Automaticos`; falha na troca tenta repor a pasta anterior.
- Compilação e prévia visual necessárias; o fluxo de restauração ainda não foi exercitado com um arquivo de save real.

## Revisão de textos · v35

- A barra do jogo e as mensagens de saves usam acentuação correta e instruções mais claras, inclusive o prazo para concluir o login no navegador.
- O título do aplicativo identifica o build como v35. A publicação autocontida foi instalada e aberta pelo launcher; a janela respondeu como `Pokemons Play · v35 · Pixel`.
- Os hashes dos 10 arquivos de capa e dos dados de saves, configurações e nuvem conferem antes e depois da instalação (11 arquivos no total).
- A restauração interativa de saves e a abertura de uma ROM não foram exercitadas nesta rodada.

## Integridade dos backups · v36

- Backups do Banco Pokémon e backups ZIP manuais recebem sufixos únicos, para duas gravações próximas não tentarem reutilizar o mesmo caminho.
- Mensagens restantes da sincronização de saves e da captura de teclas receberam acentuação corrigida.
- Publicação autocontida instalada: 373 entradas; launcher e DLL instalada conferem com o pacote. Os hashes dos 10 arquivos de capa e dos dados de saves, configurações e nuvem permaneceram iguais (11 arquivos no total).
- O runtime v36 respondeu ao abrir. A interface foi fechada depois que o usuário explicou que janelas abertas interrompem seu trabalho; não abrir novas janelas durante os próximos ciclos.
- Não havia saves locais para exercitar a gravação; ROM e sincronização/restauração de nuvem não foram executadas.

## Restauração local limitada · v37

- A restauração local valida caminhos e limites do ZIP (512 itens, 32 MB descompactados e tamanho real de cada arquivo) antes de trocar o save.
- O launcher verifica novamente se o emulador abriu durante a seleção/extração; nesse caso, cancela a troca e remove o staging.
- Publicação autocontida instalada; as 373 entradas foram verificadas e os hashes de launcher, DLL instalada e 11 arquivos de dados preservados conferem.
- Não havia saves locais para executar uma restauração real. A interface não foi aberta, conforme preferência do usuário.

## Limpeza da restauração na nuvem · v38

- A restauração local e a restauração da nuvem agora usam o mesmo extrator limitado, com validação de caminhos e do tamanho efetivamente extraído.
- Falhas na restauração da nuvem removem a pasta temporária; a checagem dos processos do emulador também libera todos os handles obtidos.

## Correção e imagens v29

- Relato do usuário: ObjectDisposedException de System.Windows.Forms.ContextMenuStrip ao usar um seletor. ThemeSelect descartava o menu no evento Closed. A v29 mantém um menu por seletor, descarta no fim de vida do controle e agenda a alteração de seleção após o clique, evitando reentrância no fechamento do ToolStrip.
- Não foi executado um teste interativo de regressão desse erro. A alteração foi compilada; ausência de exceção em prévias estáticas não comprova a correção do clique.
- GBA, DS e 3DS usam imagens transparentes geradas com Imagegen e embutidas como recursos. Botões, pressão, telas e coordenadas são controles do aplicativo. Arquivos e prompts em recovered-source/main-app/Assets/Consoles/README.md.
- Publicação Release e prévias desktop/compact concluídas com código 0 em output/v29-confirmed. Estados pressionados continuam ilustrativos.
- Pacote v29: 373 entradas, 897.316.825 bytes; DLL de pacote/publicação/instalação com SHA-256 DD0D7B53B55AC7DB1A89FA9A7677151303467F7AF583DCAC369DEC19CA76AFCE. As dez capas foram preservadas por hash. Aplicativo instalado aberto e respondendo com título Pokemons Play · v29 · Pixel.

## Ampliação v28: teste por console

- O usuário pediu desenhos distintos para GBA, Nintendo DS e Nintendo 3DS. Seletor de console persistido com os demais controles.
- GBA mostra tela, direcional, A/B, L/R, Start e Select; X/Y ficam ocultos na lista desse teste.
- DS mostra duas telas e A/B/X/Y. 3DS mostra tela superior larga, tela inferior e Circle Pad animado.
- Iniciar teste abre uma área própria para visualizar o console inteiro. DS/3DS recebem clique, arraste e WM_TOUCH na tela inferior; mostram contato, marca e coordenadas nativas (256×192 DS / 320×240 3DS). Toque é diagnóstico nessa tela, sem alterar os jogos.
- Teste inicia ao abrir, pode ser pausado/retomado e encerra com Esc ou Concluir. Perda de foco limpa entradas e pausa. Botões virtuais mantêm múltiplos contatos; Circle Pad virtual aceita direção por clique/toque.
- O jogo escolhe o desenho dos botões virtuais conforme seu emulador. Capas e a identidade pixel permanecem.
- Plano: implementar desenhos e hitboxes compartilhadas, janela de teste, integração/persistência, compilar, renderizar os três modelos em duas larguras, atualizar pacote e instalação.
- Evidência v28: build Release e publicação sem erros; prévias de GBA/DS/3DS em output/v28-confirmed/desktop e compact, com processos de renderização encerrados em código 0. Estados pressionados e toque nessas imagens são ilustrativos. O teste físico de gamepad e multitouch continua pendente.
- Pacote v28: 373 entradas, 893.900.607 bytes. DLL do pacote/publicação/instalação com SHA-256 `DC6EE08C4C233ED9347EA4A964461BBE926B31848E884D2C81128F495CBF9D38`. Dez capas preservadas por hash. Aplicativo instalado aberto e respondendo com título `Pokemons Play · v28 · Pixel`. Evidência em output/v28-package-evidence.json.

## Requisitos e decisões

- R1: preservar pixel art PKForge e capas originais.
- R2: visualizar direcional, botões, ombros, gatilhos e analógicos com feedback enquanto o teste está ativo. Sem foco, limpar entradas e pausar leitura.
- R3: selecionar teclado, teclado + mouse, controle XInput (slots 1–4), ou touchpad. O usuário confirmou que touchpad significa botões virtuais na tela, com clique e multitouch.
- R4: personalizar teclado e controle, cancelar captura com Esc, impedir atribuições duplicadas e reservar F12 para voltar ao menu.
- R5: salvar o modo e atribuições em Settings/input-device.json sem quebrar input-presets.txt. Aplicar a seleção durante a execução dos jogos; ponte de controle envia as teclas configuradas somente ao jogo em foco e libera as teclas ao pausar, desconectar ou fechar.
- R6: mostrar conexão real do controle e instrução de recuperação; não apresentar dispositivo inexistente como conectado.

## Plano

1. Modelo persistente, leitura XInput e teclado (R3–R6).
2. Visualização pixel e painel de teste/captura (R1–R4, R6).
3. Integração com configurações e execução (R3–R5).
4. Compilação, prévias em duas larguras, empacotamento e atualização da instalação (R1–R6).

## Evidências

- Compilação Release win-x64 concluída com 0 erros.
- Prévia de controles reais em output/v27-confirmed/desktop (1280×820) e compact (1000×720), incluindo modos controle/virtual e feedback ilustrativo. Ambos os processos terminaram com código 0.
- Leitura XInput real: os quatro slots estavam desconectados. Gamepad físico, multitouch físico e integração dos jogos não foram exercitados nesta sessão.
- A animação acompanha entradas enquanto o teste está ativo. A captura e teste interrompem a leitura ao sair da janela; F12 é reservado e Esc cancela.
- A janela do jogo recebe os botões virtuais quando esse modo está salvo. X/Y estão disponíveis para DS/3DS; VBA-M ignora esses dois botões.
- O launcher aplica os perfis antes de iniciar os emuladores diretamente, evitando que os antigos executáveis individuais substituam as atribuições.
- Pacote v27 refeito: 373 entradas, 893.894.842 bytes. DLL do pacote, publicação e instalação com SHA-256 `6FAC2ED0A7869CDCC29D89C66CEE768429834A16356C2C82D0272CB244FBB0CC`. As dez capas têm hashes idênticos antes/depois e no pacote.
- Aplicativo instalado aberto com título `Pokemons Play · v27 · Pixel`, processo respondendo. Evidência de integridade em output/v27-package-evidence.json.
- Evidência v38: publish Release e stub concluídos; pacote v38 contém 373 entradas (897.317.563 bytes), SHA-256 `210E2D39F7597013863CD07D4712AD68114979A5BB7A7D9A210855BCDFE9FF64`. Runtime instalado com 273 arquivos e DLL SHA-256 `B0E4AD4A6D6E653C204F44F07DA8958BE7B0E60A6053DD6BDC656D424549A46D`; launcher instalado confere com o pacote. Os 13 arquivos de dados nas pastas Capas, Saves, Settings e Cloud mantiveram os mesmos hashes; dez capas verificadas. A interface ficou fechada e não havia processo do app durante a instalação.
- Limites v38: compilação concluída sem erros, com avisos CA1416 de compatibilidade Windows; não havia cenário de save/nuvem/emulador executado com dados reais nesta rodada.

## Janela de corrida na restauração local · v39

- Depois de criar o backup automático, a restauração verifica novamente se o emulador iniciou; se sim, cancela antes de mover a pasta do save e limpa a extração temporária.
- Publicação Release e stub concluídos. Pacote com 373 entradas, 897.317.573 bytes, SHA-256 `476802415C857BDC19ACE430CDE49E303A46810D39462F8048410A0D0C2CFD63`; launcher e runtime instalados conferidos. A versão v38 foi retida para rollback.
- Os 13 arquivos de dados, incluindo as dez capas, mantiveram seus hashes. Nenhuma janela do aplicativo foi aberta.
- Limite: ainda existe uma janela mínima entre a última checagem e a troca das pastas; outra aplicação poderia iniciar o emulador nesse intervalo. A restauração real não foi exercitada com um save nesta rodada.

## Backup local visível antes da restauração da nuvem · v40

- Antes de substituir os saves locais, a restauração da nuvem cria um ZIP automático em `Saves/Backups/Automaticos`. Se não conseguir guardar o estado local, cancela a substituição. Reconfere se o emulador iniciou durante a criação do backup e apresenta na tela o local correto da cópia.
- Publicação Release e stub concluídos. Pacote com 373 entradas, 897.317.680 bytes, SHA-256 `D85F191B8B10FB57E15D4ADA813FFA8A0763B2CE3BDB2EF83CBA7CCEFFC85BBB`; launcher/runtime instalados conferidos. A versão v39 foi retida para rollback.
- Os hashes dos dados preservados, incluindo dez capas, coincidem antes/depois. A interface não foi aberta.
- Limite: não executei uma restauração com conta/saves reais nem um teste interativo nesta rodada.

## Consulta segura do processo antes de abrir jogos · v41

- A busca do processo do emulador agora descarta todos os objetos `Process`, evitando acumular handles a cada clique em “Jogar”.
- Depois do backup automático, o launcher consulta novamente o processo para detectar um emulador que iniciou durante a cópia. A confirmação explica que abrir outro jogo pode causar conflito e que o save mais recente pode não estar incluído no backup.
- Publicação Release e stub concluídos. Pacote com 373 entradas, 897.317.809 bytes, SHA-256 `4BB1EA302F15E5E7E5CAFD3CD790D8EF8F5F731E7FFB9E0F2D70CBB6862E853E`; launcher/runtime instalados conferidos. A versão v40 foi retida para rollback.
- Os hashes dos dados preservados, incluindo dez capas, coincidem antes/depois. Interface fechada durante todo o ciclo.
- Limite: não exercitei o lançamento de jogo ou a corrida com um emulador real nesta rodada.

## Ajuste da zona morta XInput · v42

- O modo Controle agora permite ajustar a zona morta de 10% a 60%; a configuração é salva no perfil e usada na leitura do analógico e no indicador visual. A tela mostra a porcentagem ativa e o texto explica para que serve o ajuste.
- Publicação Release e stub concluídos. Pacote com 373 entradas, 897.318.222 bytes, SHA-256 `B6C8E51B6F15AB6B60A02B38FB5768FAE14ADEC27C84B798E933D3B5E3EB2AEC`; launcher e runtime instalados conferidos. A versão v41 foi retida para rollback.
- Os hashes de 13 arquivos de dados, incluindo dez capas, coincidem antes/depois. A interface não foi aberta.
- Limite: não validei o controle físico nem a animação interativamente nesta rodada.

## Upload de save consistente para a nuvem · v43

- Antes de enviar, o app revalida se há emulador aberto após atualizar a sessão e de novo antes da compactação. O ZIP é conferido por impressão digital antes/depois; se os arquivos mudarem durante a cópia, o envio é cancelado. O snapshot também respeita os limites de restauração: até 512 itens e 32 MB descompactados.
- O app consulta o emulador novamente após compactar para cancelar se ele abriu durante essa etapa.
- Publicação Release e stub concluídos. Pacote com 373 entradas, 897.318.524 bytes, SHA-256 `9E77D346322A096AFA09E759F0A3C5E2EC0D6B483B62606167A01DEA658950BB`; launcher/runtime instalados conferidos. A versão v42 foi retida para rollback.
- Hashes de 13 arquivos de dados, incluindo dez capas, idênticos antes/depois. Nenhuma janela do aplicativo foi aberta.
- Limite: não executei o envio com conta na nuvem nem com um emulador real.

## Extração concorrente e recuperação do inicializador · v44

- O inicializador agora serializa extrações com mutex por usuário. Uma segunda cópia aguarda, relê o marcador e só extrai se o runtime continuar ausente. O pacote também repara a instalação quando o marcador existe, mas o executável do runtime sumiu.
- A pasta temporária exclusiva é removida após falha; o leitor rejeita contagem, tamanho e caminhos inválidos antes de trocar o runtime.
- Publicação Release e stub concluídos. Pacote com 373 entradas, 897.319.551 bytes, SHA-256 `29948DF62702DF348A4C87EE3896A7FB21D3044CC6F9C9621830E0C7439D91CD`; launcher/runtime instalados conferidos. A versão v43 foi retida para rollback.
- Hashes dos 13 arquivos de dados, incluindo dez capas, idênticos antes/depois. Interface fechada durante o ciclo.
- Limite: não simulei duas inicializações concorrentes nem forcei corrupção do runtime; verifiquei compilação, código e integridade do pacote instalado.

## Limites do autoextrator · v45

- O stub agora rejeita bundles com mais de 4.096 itens, caminhos com mais de 32 KiB, caminhos de saída duplicados, tamanhos negativos e payload total descompactado acima de 5 GiB, antes de trocar o runtime.
- Publicação Release e stub concluídos. Pacote com 373 entradas, 897.320.059 bytes, SHA-256 `051A7A9B13ED7CC0888166B6416CF22B9D1CCA51A8F52D603A6303A326C262E8`; launcher/runtime instalados conferidos. A versão v44 foi retida para rollback.
- Os hashes dos 13 arquivos de dados, incluindo dez capas, conferem antes/depois. Nenhuma interface foi aberta.
- A remoção do antigo staging `C:\Users\veron\AppData\Local\Pokemons Play.extracting-36b80426ca9742c389f3e2e24538415c` (2,49 GB) foi bloqueada pela revisão automática. A pasta foi deixada intacta; os 99 arquivos nas árvores de dados/capas comparadas estavam idênticos aos ativos.
- Limite: não testei um bundle malformado nem iniciei extrações simultâneas reais.

## Limites dos backups ZIP · v46

- A criação de backups locais automáticos agora aplica os mesmos limites da restauração: no máximo 512 arquivos e 32 MB descompactados. Os ZIPs locais e de nuvem também são verificados após a compactação quanto à quantidade de entradas e ao tamanho antes de serem publicados ou enviados.
- A conferência de impressão digital dos saves após a compactação continua ativa, para não considerar válido um snapshot se os arquivos mudarem durante a operação.
- Publicação self-contained e build do stub concluídos. Pacote v46: 373 entradas, 897.320.127 bytes, SHA-256 `DB2A297E0682EA9EA1C3FF505D6C7A31EC27397BF9446F7AF70E1FAE0C4B8F09`. Launcher instalado confere com o pacote; runtime com 273 arquivos e DLL SHA-256 `FC3A76A175D1781A1AEBAB175FFEE6A2B5500D9F74B2D8C3A9166B1500D03FA8`. A versão v45 foi retida para rollback.
- Os 13 arquivos de dados preservados, incluindo dez capas, mantiveram os mesmos hashes antes e depois. A interface permaneceu fechada e não havia instância do aplicativo.
- Evidência detalhada: `output/v46-package-evidence.json`; snapshots de dados em `output/v46-preserved-data-before.json` e `output/v46-preserved-data-after.json`.
- Limite: não executei testes automatizados, nem backup/restauração com arquivos grandes reais ou alteração concorrente de saves. A pasta de extração antiga de 2,49 GB mencionada na v45 permanece, pois a revisão automática rejeitou sua remoção recursiva.
