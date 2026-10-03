# Ciclo de confiabilidade de jogos e controles

Objetivo: melhorar o caminho entre configurar comandos e jogar, com evidências que distingam configuração, comandos recebidos e execução real de conteúdo.

Escopo deste ciclo: corrigir nomes de teclas no RetroArch (incluindo teclado numérico), eliminar entradas físicas duplicadas quando o launcher gerencia o controle, proteger combinações de teclas e facilitar a escolha de um controle conectado. Preservar a identidade Pixel, configurações persistentes, saves, emuladores e o atualizador.

Critérios de aceitação:

- C1: a configuração de sessão usa nomes reconhecidos pelo RetroArch para teclas capturadas no Windows, em emuladores incluídos e externos.
- C2: nos modos Controle/Touchpad, remapeamentos do launcher não competem com bindings físicos automáticos do RetroArch; teclas de jogo não acionam hotkeys sem um modificador livre.
- C3: duas ações associadas à mesma tecla não produzem soltura enquanto outra ação continua pressionada. Perda de foco, desconexão e encerramento liberam as entradas do bridge. Tentativas de envio malsucedidas são repetidas.
- C4: a pessoa pode selecionar explicitamente um slot XInput conectado; reconectar ou conectar outro controle não troca seu slot automaticamente durante o jogo.
- C5: a tela de teste informa que é uma visualização de comandos, não a execução de uma ROM. A ação e os avisos cabem nas larguras existentes e têm nomes acessíveis.
- C6: executar um teste nativo de emulação com conteúdo original de diagnóstico, sem alterar saves ou configurações reais. ROMs pessoais e controle físico só são classificados como testados quando houver execução e evidência correspondentes.

Loop: estabelecer reprodução → corrigir uma causa → verificar → integrar → repetir para o próximo critério. Encerrar este ciclo com os critérios de código verificados e limitações de hardware/conteúdo registradas. Não criar agendamento contínuo nem publicar uma nova release neste escopo.

Estado inicial: v171.9.0, commit 588708761201dd1b5f23c5f1fd2ed6199b244069. O projeto original contém alterações preexistentes; usar o checkout integrado separado. Não foram encontradas ROMs reais de pelo menos 64 KiB na árvore do projeto; os arquivos encontrados são fixtures. A pasta de ROMs foi solicitada ao usuário.

Pesquisa primária: [bindings e hotkeys do RetroArch](https://github.com/libretro/RetroArch/blob/master/retroarch.cfg), [nomes de teclas aceitos](https://github.com/libretro/RetroArch/blob/master/input/input_keymaps.c), [XInputGetState](https://learn.microsoft.com/en-us/windows/win32/api/xinput/nf-xinput-xinputgetstate).
