# Prova DS sem ROM comercial

Pesquisa: 2026-10-03. Branch observada: `codex/save-actions-recovery`. Documento de pesquisa e plano; nenhum core, ROM ou aplicativo foi executado, nenhum binário foi baixado e nenhum código foi alterado.

## Conclusão e identidade do core

É viável planejar uma prova de boot, renderização e entrada em **modo DS**, usando programa próprio, BIOS substitutos e firmware gerado pelo melonDS DS. Isso não comprova compatibilidade com jogos comerciais, DSi ou controle físico.

O script local `recovered-source/tools/prepare-emulators.ps1` aponta para **JesseTG/melonds-ds**, commit `f394adbacb5722ee97c1b37c8064da9a25818310`, anunciado no bundle como melonDS DS 1.4.0. Não confundir com o antigo `libretro/melonDS`, cuja política de BIOS difere. A identidade efetiva da DLL deve ser registrada via `retro_get_system_info` e SHA-256 na execução futura. [Documentação libretro distingue os cores](https://docs.libretro.com/library/melonds_ds/).

O commit do wrapper referencia uma **branch mutável** de JesseTG/melonDS, `jtg/fix-uninitialized-opengl`, como dependência. Na pesquisa ela resolve para `16f127dbb587a73371827ce79689c5d237c4b59b`. A configuração padrão upstream usa essa branch, mas o build local sobrescreve explicitamente MELONDS_REPOSITORY_TAG com o SHA 16f127dbb587a73371827ce79689c5d237c4b59b. A dependência do build do projeto está portanto fixada; a DLL efetiva ainda deve ser identificada por hash na prova. [Script do build do projeto](https://github.com/Juanblack1/pokemonplay/blob/8495a422814a611af650bf073b1f08a4f18db0b4/recovered-source/tools/build-emulator-cores.sh). [FetchDependencies fixado](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/cmake/FetchDependencies.cmake).

## Arquivos e opções

No modo DS, o core oferece FreeBIOS e constrói `Firmware(ConsoleType::DS)` quando utiliza substitutos. Built-in força boot direto e exige conteúdo: não é substituto do menu inicial Nintendo. Não são necessários `bios7.bin`, `bios9.bin` ou firmware externo para essa prova; fornecer diretórios de sistema/save temporários vazios. DSi exige arquivos nativos de BIOS/firmware/NAND e fica fora do escopo. [Construção da console](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/src/libretro/config/console.cpp), [opções de sistema](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/src/libretro/config/definitions/system.hpp).

Configuração explícita proposta, confirmada nas definições desse wrapper:

| Variável | Valor | Finalidade |
| --- | --- | --- |
| `melonds_console_mode` | `ds` | Evitar seleção DSi automática |
| `melonds_sysfile_mode` | `builtin` | Não procurar dumps nativos |
| `melonds_boot_mode` | `direct` | Entrar no programa próprio |
| `melonds_render_mode` | `software` | Capturar pixels sem contexto OpenGL |
| `melonds_touch_mode` | `touch` | Pointer, para teste de toque separado |
| `melonds_network_mode` | `disabled` | Sem rede |
| `melonds_firmware_username` | `melonDS DS` | Evitar inferir nome do usuário real |

Os nomes e valores vêm de [constants.hpp](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/src/libretro/config/constants.hpp), e as semânticas de [vídeo](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/src/libretro/config/definitions/video.hpp), [tela](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/src/libretro/config/definitions/screen.hpp), [rede](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/src/libretro/config/definitions/network.hpp) e [firmware](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/src/libretro/config/definitions/firmware.hpp). O padrão Username é Guess e pode consultar variáveis de ambiente. Na prova, registrar opções declaradas pela própria DLL, verificar que aceitam esses valores e não inventar sucesso para opções ausentes.

## Conteúdo recomendado e licenças

Preferir um **gerador determinístico original**, pequeno: header NDS, payload ARM9 próprio que inicializa um framebuffer, lê keypad e alterna uma região entre cores conhecidas; payload ARM7 próprio em loop estável. Não incluir logo Nintendo, banner, gráficos, fontes, som, filesystem, dumps ou blobs externos. Usar apenas constantes e instruções escritos para a prova, e declarar licença do projeto para o gerador. A inicialização real dos registradores/renderização ainda precisa ser implementada e verificada; header aceito não significa programa funcional.

A fonte da dependência consultada exige ROM com pelo menos `0x1000` bytes, offsets ARM9 e ARM7 >= `0x200` e segmentos dentro do arquivo. Homebrew é reconhecido por ARM9 offset < `0x4000` **ou** gamecode `####`. Uma proposta inicial é header reservado de 4 KiB e payloads após ele, ARM9 abaixo de `0x4000`, unitcode DS=0, endereços e entradas válidos; preencher os campos ARM9/ARM7 de offset, endereço de carga, entrada e tamanho. Essas são condições do parser observado, não uma especificação completa de ROM válida em hardware. [NDSCart.cpp](https://github.com/JesseTG/melonDS/blob/16f127dbb587a73371827ce79689c5d237c4b59b/src/NDSCart.cpp), [NDS_Header.h](https://github.com/JesseTG/melonDS/blob/16f127dbb587a73371827ce79689c5d237c4b59b/src/NDS_Header.h), [layout do header no ndstool](https://github.com/devkitPro/ndstool/blob/master/source/header.h).

Alternativa maior: escrever aplicação própria com devkitARM/libnds, `scanKeys()` e `keysHeld()`, preservando avisos das bibliotecas e auditando runtime/ARM7 e todas as dependências linkadas. libnds tem licença permissiva com condições de atribuição de origem e preservação do aviso. **Não inferir** que exemplos têm a mesma licença: não foi encontrado LICENSE global em `devkitPro/nds-examples`, e hello_world traz autor, mas não permissão explícita no arquivo consultado. Usar como referência de API, não redistribuir o binário como fixture licenciada. [Licença libnds](https://github.com/devkitPro/libnds/blob/master/libnds_license.txt), [API/exemplos referenciados](https://github.com/devkitPro/libnds/blob/master/include/nds.h), [hello_world](https://github.com/devkitPro/nds-examples/blob/master/hello_world/source/main.cpp). O ndstool contém dados `nintendo_logo`; a construção deve auditar o header produzido, sem presumir que toolchain elimina todos os assets externos. [ndscreate.cpp](https://github.com/devkitPro/ndstool/blob/master/source/ndscreate.cpp).

## Entrada e evidência

O core chama `retro::input_poll()`, consulta joypad no port 0 e encaminha a máscara para `nds.SetKeyMask`. DS A corresponde ao ID libretro A (8), B a B (0), Start a Start (3); a máscara de keypad DS não tem a mesma numeração da API libretro. Aceitar consultas individuais e só anunciar suporte a bitmask quando retornar a máscara corretamente. Pointer usa device POINTER, index 0, X/Y/PRESSED e coordenadas normalizadas da API; a transformação depende de layout, orientação e tela inferior. [InputState](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/src/libretro/input/input.cpp), [JoypadState](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/src/libretro/input/joypad.cpp), [contrato libretro](https://github.com/libretro/libretro-common/blob/master/include/libretro.h).

Plano de prova futuro:

1. Registrar hash DLL, versão, opções efetivas, hash do ROM próprio e hash do gerador. Validar tamanho/segmentos antes de carregar. Usar processo dedicado e timeout, isolando system/save; sem arquivos pessoais, microfone real, rede ou username inferido.
2. Instalar callbacks environment/input/video/audio conforme ABI, chamar init/load/run e comprovar `retro_load_game=true`, frames com geometria/pixel format/pitch corretos e marcador visível do homebrew. Copiar pixels dentro do callback; contar consultas não basta.
3. Executar sequência bounded **solto → A pressionado → solto**. A região definida pelo programa deve produzir exatamente as cores esperadas e retornar à cor inicial. Não usar mera diferença de hash do frame inteiro: relógio, cursor e animação podem mudar sem entrada.
4. Repetir com B/Start/direção apenas se o programa definir sinais distintos. Não usar Start para encerrar antes de colher evidência. Para touch, adicionar posteriormente aplicação própria que reporte coordenadas e pressione um marcador; não prometer toque com o payload simples só de keypad.
5. Salvar frame de cada fase, contadores, logs e relatório de falhas. Reset/serialize são verificações separadas, não pré-requisitos para o teste mínimo. Liberar unload/deinit e finalizar processo.

Há testes oficiais pytest/libretro.py/CTest, incluindo boot built-in e entrada. Entretanto os testes de botão e pointer consultados dirigem o menu DS e **exigem firmware nativo bootável**; não servem diretamente ao escopo sem dumps. Os `.nds` de teste upstream não foram baixados nem tiveram licença de conteúdo individual comprovada. [README da suíte](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/test/README.md), [test_input.py](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/test/python/test_input.py).

O probe local atual gera apenas GBA quando `--rom` é omitido; fornece callbacks joypad e não pointer. Assim, sua prova GBA existente não evidencia DS. Implementação e execução ainda pendentes. Resultado possível deve ser descrito estritamente como execução do homebrew e entrada por callbacks no core/DLL testado; não comprova gameplay comercial, BIOS Nintendo, integração visual completa do launcher ou entrega de input por hardware físico.

## Complemento: contrato proposto para payload ARM9 original

As fontes devkitPro foram conferidas em libnds `84e6082ce27c87ed218fb369a9944644aa2243a6` e calico `81b75e314d57ed1784545e28554e567f26f572f1`. libnds atual reexporta vários registradores e constantes de calico. Não copiar executáveis, logo ou runtime de jogo comercial.

| Item ARM9 | Endereço, tamanho e valor proposto | Evidência |
| --- | --- | --- |
| DISPCNT, engine A | `0x04000000`, escrita 32 bits, `0x00020000` (`MODE_FB0`) | libnds define modo de display direto de VRAM_A em LCD mode |
| VRAMCNT_A | `0x04000240`, escrita **8 bits**, `0x80` | Banco habilitado, MST=0=LCDC; `vramSetBankA(VRAM_A_LCD)` usa VRAM_ENABLE OR 0 |
| Framebuffer VRAM_A LCDC | `0x06800000`, pixels de 16 bits | MM_VRAM_A = MM_VRAM + 0x800000; banco de 128 KiB |
| POWCNT9 | `0x04000304`, proposta escrita 16 bits `0x8003` | bit0 LCD, bit1 engine A, bit15 engine A na tela superior |
| KEYINPUT | `0x04000130`, leitura 16 bits | A é bit0; zero=pressionado, um=solto |
| Pixel | `r | (g << 5) | (b << 10)`, canais 0..31 | RGB15; vermelho `0x001F`, azul `0x7C00` |
| Área visível | 256 × 192, 49.152 pixels, 98.304 bytes | SCREEN_WIDTH/HEIGHT → LCD_WIDTH/HEIGHT |

Fontes primárias para a tabela: [video.h libnds fixado](https://github.com/devkitPro/libnds/blob/84e6082ce27c87ed218fb369a9944644aa2243a6/include/nds/arm9/video.h), [io.h calico](https://github.com/devkitPro/calico/blob/81b75e314d57ed1784545e28554e567f26f572f1/include/calico/nds/io.h), [mm.h calico](https://github.com/devkitPro/calico/blob/81b75e314d57ed1784545e28554e567f26f572f1/include/calico/nds/mm.h), [pm.h calico](https://github.com/devkitPro/calico/blob/81b75e314d57ed1784545e28554e567f26f572f1/include/calico/nds/pm.h), [keypad.h calico](https://github.com/devkitPro/calico/blob/81b75e314d57ed1784545e28554e567f26f572f1/include/calico/gba/keypad.h), [lcd.h calico](https://github.com/devkitPro/calico/blob/81b75e314d57ed1784545e28554e567f26f572f1/include/calico/gba/lcd.h).

Não confundir `POWER_LCD`/`POWER_2D_A` de libnds com valores crus: as APIs novas incluem marcadores de roteamento de power management. Para payload que escreve registradores diretamente, os bits acima vêm de POWCNT_* de calico. Seu helper de layout coloca bit15 quando engine A deve ficar em cima. Embora o alias calico use registro u32, a implementação melonDS aceita escrita ARM9 de 16 bits em `0x04000304` e atualiza GPU; é o tamanho proposto para o payload.

A implementação melonDS confirma que swap=1 associa saída engine A ao framebuffer top, e que display mode 2 lê banco selecionado por DISPCNT bits18..19, apenas se mapeado LCDC. Ela lê pixels consecutivos em `line * 256 + x`, usando bits0..14 de cor e ignorando bit15 nesse modo. Não é bitmap BG com transparência: não é necessário setar bit15 do pixel para VRAM display. [GPU.cpp](https://github.com/JesseTG/melonDS/blob/16f127dbb587a73371827ce79689c5d237c4b59b/src/GPU.cpp), [GPU_Soft.cpp](https://github.com/JesseTG/melonDS/blob/16f127dbb587a73371827ce79689c5d237c4b59b/src/GPU_Soft.cpp), [NDS.cpp, acesso aos registradores](https://github.com/JesseTG/melonDS/blob/16f127dbb587a73371827ce79689c5d237c4b59b/src/NDS.cpp).

Contrato de comportamento proposto: configurar power/banco/display, preencher a área visível com azul, ler KEYINPUT repetidamente e escrever vermelho em uma região fixa quando `(KEYINPUT & 1)==0`, azul quando diferente de zero. Para primeira prova, preencher toda a tela com a cor do estado simplifica o oráculo; aguardar vários frames estáveis em cada fase evita colher o frame parcial de uma mudança. Não usar SWI de VBlank ou libnds sem seus serviços; polling simples não precisa de BIOS comercial, IPC, DMA, IRQ, filesystem ou pilha C. A implementação de instruções, endereçamento, loops e tempo de preenchimento precisa de revisão e execução posterior.

### O que direct boot fornece

Na revisão melonDS consultada, Reset zera IME/IE/IF das duas CPUs e VRAM pelo reset da GPU. `SetupDirectBoot()` mapeia shared WRAM para ARM7, copia ambos os payloads nos endereços do header, configura dados de firmware gerado e CP15 do ARM9. A variante com nome de ROM configura stacks ARM9 e ARM7, salta para as entradas especificadas, seta PostFlag de ambas, POWCNT9=`0x820F`, POWCNT7=`1` e bias de áudio. Logo, a proposta `0x8003` torna explícito apenas LCD/engine A/top, enquanto preservar `0x820F` também mantém engine B/3D ligados; ambos incluem os bits necessários. **Não** usar `0x0003` se o oráculo procura engine A na tela superior. [SetupDirectBoot/Reset em NDS.cpp](https://github.com/JesseTG/melonDS/blob/16f127dbb587a73371827ce79689c5d237c4b59b/src/NDS.cpp).

Um payload ARM7 original com branch para si próprio é uma **hipótese sustentada pelas rotas de código** para esta prova mínima: KEYINPUT é registro compartilhado mantido pelo core, leitura ARM9 não depende de FIFO/serviço ARM7, e scanlines/renderização são eventos do emulador. Colocar o loop em RAM ARM7 exclusiva (`0x03800000`, entrada alinhada ARM) e ARM9 em main RAM (`0x02000000`) evita depender de shared WRAM para código. Isso não demonstra que loop ARM7 basta para libnds completo, touch calibrado, X/Y por serviços, áudio, sleep ou execução em hardware. Ainda é necessário confirmar que a DLL distribuída segue as rotas observadas, que ambas as CPUs permanecem estáveis e que o payload não dispara exceções. [Memória calico](https://github.com/devkitPro/calico/blob/81b75e314d57ed1784545e28554e567f26f572f1/include/calico/nds/mm.h), [keypad/core](https://github.com/JesseTG/melonDS/blob/16f127dbb587a73371827ce79689c5d237c4b59b/src/NDS.cpp).

### Localizar a tela superior no video callback

No wrapper fixado, defaults são layout #1 `top-bottom`, gap `0`, secondary scale `100`, número de layouts `2`. Definir explicitamente `melonds_screen_layout1=top-bottom`, `melonds_screen_gap=0`, `melonds_secondary_screen_scale=100`, `melonds_number_of_screen_layouts=1` e `melonds_show_cursor=disabled` para a prova. Não pressionar R3, que troca layout. [Definições de tela](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/src/libretro/config/definitions/screen.hpp), [nomes/valores](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/src/libretro/config/constants.hpp).

Com software e essa configuração, a composição esperada é **256 × 384**, tela superior em x=0..255/y=0..191, inferior em y=192..383. A alternativa `melonds_screen_layout1=top` elimina a inferior e deve produzir 256 × 192 para prova apenas de keypad/video. O wrapper obtém top/bottom do core, coloca top no canto superior esquerdo para TopBottom e chama video_refresh com width/height/**stride** efetivos. Validar geometria anunciada e usar `pitch` para cada linha, não presumir buffer contíguo. [screenlayout.cpp](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/src/libretro/screenlayout.cpp), [software.cpp](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/src/libretro/render/software.cpp).

A cor RGB555 na VRAM não é o formato entregue pelo callback: software do wrapper usa XRGB8888; a GPU primeiro expande/transforma canais e aplica master brightness. O oráculo deve interpretar o pixel format negociado, conferir canais vermelho/azul e ausência dos outros, sem tratar uint16 da VRAM como bytes finais nem exigir alpha. Só fixar valores exatos de canal após confirmar a conversão final dessa DLL; branco ou preto podem indicar power/layout/configuração errada. Não houve execução para provar imagem, estabilidade ou input do payload proposto.
