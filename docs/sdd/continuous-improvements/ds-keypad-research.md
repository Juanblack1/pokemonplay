# Ampliar o diagnóstico DS de keypad

Pesquisa em 2026-10-03. Somente fontes primárias e leitura de contexto local; nenhuma ROM/core foi executada, nenhum código do repositório foi alterado e nenhum commit foi criado.

## Mapeamento confirmado

O core consultado é JesseTG/melonds-ds `f394adbacb5722ee97c1b37c8064da9a25818310`. `JoypadState::Update` inicia máscara DS em `0xFFF`, **limpa** o bit DS quando o botão libretro está pressionado e o mantém em 1 quando solto. `Apply` chama `nds.SetKeyMask`. Os IDs libretro pertencem ao RetroPad, não ao keypad DS. [joypad.cpp, linhas 46–70 e 111–112](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/src/libretro/input/joypad.cpp#L46-L70), [libretro.h fixado, constantes](https://github.com/libretro/libretro-common/blob/fa8a1b5/include/libretro.h).

| Botão DS | ID libretro JOYPAD | Bit KEYINPUT | Máscara DS | KEYINPUT & 0x03FF se apenas ele pressionado |
| --- | ---: | ---: | --- | --- |
| A | 8 | 0 | 0x001 | 0x3FE |
| B | 0 | 1 | 0x002 | 0x3FD |
| Select | 2 | 2 | 0x004 | 0x3FB |
| Start | 3 | 3 | 0x008 | 0x3F7 |
| Right | 7 | 4 | 0x010 | 0x3EF |
| Left | 6 | 5 | 0x020 | 0x3DF |
| Up | 4 | 6 | 0x040 | 0x3BF |
| Down | 5 | 7 | 0x080 | 0x37F |
| R | 11 | 8 | 0x100 | 0x2FF |
| L | 10 | 9 | 0x200 | 0x1FF |

KEYINPUT é leitura ARM9 de **16 bits em 0x04000130**. Estado neutro dos dez bits é `0x03FF`; estado pressionado positivo para o oráculo é `(~KEYINPUT) & 0x03FF`. Uma consulta individual libretro usa port=0, device=JOYPAD=1, index=0, id da tabela e retorna 1/0; com bitmask usa id=MASK=256 e retorna bits `1 << id_libretro`. Não retornar a máscara DS diretamente ao frontend. O probe já suporta consultas individuais e MASK; se anunciar bitmask, ambos precisam representar o mesmo estado. [API libretro](https://github.com/libretro/libretro-common/blob/fa8a1b5/include/libretro.h), [NDS.cpp fixado: SetKeyMask e ARM9IORead16](https://github.com/JesseTG/melonDS/blob/16f127dbb587a73371827ce79689c5d237c4b59b/src/NDS.cpp), [calico keypad: KEY_MASK e inversão active-low](https://github.com/devkitPro/calico/blob/81b75e314d57ed1784545e28554e567f26f572f1/include/calico/gba/keypad.h).

### Por que X/Y exigem uma ponte ARM7

O wrapper mapeia libretro X=9 para bit DS10 e Y=1 para bit DS11. `SetKeyMask` separa `mask & 0x3FF` dos dois bits altos e coloca estes no estado interno bits16/17. O ARM7 lê o estado estendido em **0x04000136**, conhecido como EXTKEYIN/RCNT_EXT: X=bit0 e Y=bit1, também active-low. O ARM9 não possui a mesma rota de leitura nesse endereço. Ler KEYINPUT ARM9 e testar bits10/11 não prova X/Y. [Wrapper](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/src/libretro/input/joypad.cpp#L69-L70), [NDS.cpp, ARM7IORead16](https://github.com/JesseTG/melonDS/blob/16f127dbb587a73371827ce79689c5d237c4b59b/src/NDS.cpp), [gpio ARM7 calico](https://github.com/devkitPro/calico/blob/81b75e314d57ed1784545e28554e567f26f572f1/include/calico/nds/arm7/gpio.h).

libnds/calico tem serviço ARM7 para compartilhar estado estendido com ARM9. O payload original tem ARM7 em loop sem esse serviço; uma prova de X/Y exige ARM7 próprio amostrando EXTKEYIN e comunicação por memória/IPC, com sincronização e novo oráculo. A extensão ao fim deste relatório confirma suporte nas fontes para ponte mínima por RAM principal e propõe incluí-los na matriz. Y combinado com L2 também pode fechar a tampa no wrapper; R3 troca layout. Evitar esses auxiliares/combinations nesta prova isolada. [API keypad e servidor estendido](https://github.com/devkitPro/calico/blob/81b75e314d57ed1784545e28554e567f26f572f1/include/calico/gba/keypad.h), [ações do wrapper](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/src/libretro/input/joypad.cpp#L74-L125).

## Contexto local lido

`original_ds_test.py` declara CC0-1.0, gera header/payloads próprios sem logo/banner/filesystem/dumps, configura VRAM display no engine A superior e lê apenas bit0 de KEYINPUT. Seu oráculo é tela azul solto, vermelha A pressionado, azul após soltar. `probe-libretro.py` mantém builtin/direct/software, layout top-bottom 256×384, gap0, escala100 e cursor desativado; amostra nove pontos da tela superior, aceita canais dominantes >=240 e outros <=16, e salva PNGs. O negativo `--suppress-input` retém todo input, mas uma expansão precisa registrar resultado **por botão e por fase**, não somente um contador global positivo. Esta pesquisa não avaliou resultado de execução.

## Menor desenho e recomendação

**Menor alteração de gerador:** parametrizar a máscara DS testada no `TST` original e gerar dez variantes. Cada variante mantém exatamente o oráculo de cores existente e ARM7 em loop. Para cada variante executar neutro → botão-alvo → neutro, mais botão diferente → neutro; a cor esperada do botão diferente é azul. Essa opção é simples de revisar, porém repete dez boots/cargas e para controlar todos os falsos mapeamentos precisa ampliar negativos, elevando tempo e fixtures.

**Recomendação para CI de três minutos:** uma única ROM própria com **dez regiões fixas** na tela superior, uma por bit DS0..9, todas azuis soltas e cada uma vermelha somente para seu bit pressionado. O payload lê KEYINPUT uma vez por ciclo e atualiza as regiões a partir desse mesmo snapshot. Uma tabela de dez destinos/máscaras com loop pequeno evita dez implementações independentes. Manter uma área constante de marcador de boot e a inferior fora do oráculo. Regiões pequenas (por exemplo, dez quadrados 16×16, separados) reduzem muito as escritas frente ao preenchimento atual de 49.152 pixels; dimensão/posições exatas são decisão de implementação. Não colocar labels/assets/fontes. Validar todos os quadrados em cada fase detecta simultaneamente botão correto, mapeamento trocado, botão extra indevido e release preso. O código fica um pouco maior, mas **um boot e 21 fases** dão melhor cobertura por custo.

Sequência proposta: um neutro inicial; para cada ID da tabela, pressionar apenas ele, exigir exatamente sua região vermelha e as nove outras azuis, soltar todos e exigir dez azuis. Cada fase deve receber **novo callback de vídeo**, amostrar vários pontos internos por região e exigir pelo menos dois frames finais consecutivos coerentes dentro de limite de frames. Não aceitar o último framebuffer de fase anterior como evidência. Start é apenas bit do diagnóstico, não comando de saída.

Negativos independentes:

- Reexecutar a mesma sequência com input suprimido: deve falhar no primeiro hold, mesmo que boot e frames estejam corretos. Registrar que falha esperada ocorreu no oráculo, não em DLL/boot/configuração.
- Injetar ID errado para um alvo e exigir que o oráculo detecte a **região errada**; o teste não deve trocar a expectativa junto com o erro injetado. Uma mutação direcionada A/B ou Left/Right demonstra sensibilidade de mapeamento. A matriz das dez fases positivas já observa nove negativos por fase.
- Opcional posterior: simular botão preso após release e provar que fase release falha. Combinações de múltiplos botões/opostas ficam fora do teste mínimo e não justificam reduzir isolamento por botão.

O teste positivo com nove regiões inativas já é mais forte que somente “input altera hash”; contadores de callback/consultas ficam como diagnóstico, não critério suficiente. Oráculo de pixels mede efeito visível no guest dessa ROM própria e da DLL identificada, sem inferir controles físicos ou integração de launcher.

## Orçamento de CI e riscos

O job do projeto usa explicitamente `--frames=30`, portanto o default CLI `1800` não descreve o gate atual. Medição comunicada pelo responsável: **90 frames DS nativos levaram 0,5 s, além da inicialização**. A matriz de dez botões teria 21 × 30 = 630 frames; com X/Y, 25 × 30 = 750 frames. Extrapolação linear somente da medida sugere aproximadamente 4,17 s para 750 frames, mas isso **não é medição da expansão**, nem inclui inicialização, PNGs e runner CI. Executar CI real antes de declarar prazo cumprido. O timeout externo deve cobrir bloqueio nativo dentro de retro_run, pois o timeout Python só verifica entre chamadas.

Dentro do limite total de 180 s, reservar aproximadamente 100 s ao processo positivo, 35 s ao negativo com supressão e 45 s para setup/relatório/cleanup. Não executar dez negativos com boot independente nesse gate. CI deve registrar tempos reais e falhar por timeout sem esconder resultados parciais. Se o runner não atingir orçamento, reduzir frames só após demonstrar estabilidade; não enfraquecer asserções de release/regiões inativas para fazer passar.

Riscos de implementação a revisar: literal pool ARM fora de alcance após crescer payload; ARM9 padded atualmente em 0x200 bytes e ARM7 em offset fixo 0x1200 (não sobrepor segmentos se ARM9 crescer); cálculo das instruções TST para máscaras 0x100/0x200; escrita halfword e stride dos quadrados; pipeline de amostragem/renderização gerar frames mistos; cópia/frame novo no callback; retorno MASK consistente; não confundir bit DS com ID RetroPad; nomes por botão evitarem sobrescrever PNG; expectativa independente do mapper do callback. Manter header válido/segmentos dentro da ROM, BIOS builtin, firmware username fixo, rede/microfone reais desativados, e hash do core/gerador/ROM no relatório. Os testes oficiais de input upstream dirigem menu e exigem firmware nativo, portanto são referência de método e não fixture reutilizável neste escopo. [test_input.py](https://github.com/JesseTG/melonds-ds/blob/f394adbacb5722ee97c1b37c8064da9a25818310/test/python/test_input.py).

Nenhuma expansão foi implementada ou executada nesta pesquisa. **O script do projeto** `recovered-source/tools/build-emulator-cores.sh:15` sobrescreve a dependência com `-DMELONDS_REPOSITORY_TAG=16f127dbb587a73371827ce79689c5d237c4b59b`; portanto essa revisão fixa é o alvo do build local. Apenas a receita **upstream padrão** referencia branch mutável. Registrar hash da DLL continua necessário para identificar o binário executado. Esta proposta não usa ROM/BIOS comerciais e não afirma validação de hardware físico.

## Extensão: X/Y por célula em RAM principal

**Conclusão da inspeção:** o caminho proposto é suportado pelas rotas de memória e direct boot do core fixado, permitindo planejar 12 regiões e 25 fases. Ainda não foi executado; suporte no código não é resultado de teste.

`0x02003000` é **main RAM**, acessível às duas CPUs; não é a área de Shared WRAM `0x03000000`, cuja atribuição muda por WRAMCNT. `NDS::ARM7Write16` alinha a endereço par e escreve diretamente em `MainRAM[addr & MainRAMMask]` para região 0x02. `NDS::ARM9Read16` lê a mesma matriz; no modo DS MainRAMMask=`0x3FFFFF`. Não há controle de posse EXMEMCNT nesse caminho de main RAM; as verificações de posse encontradas pertencem ao slot GBA. A célula 0x02003000 é alinhada e fica fora do payload ARM9 atual em 0x02000000..0x020001FF. Reservá-la explicitamente contra crescimento do payload, literais e futuras tabelas. [NDS.cpp: ARM7Write16/ARM9Read16/Reset](https://github.com/JesseTG/melonDS/blob/16f127dbb587a73371827ce79689c5d237c4b59b/src/NDS.cpp).

### Cache, TCM e permissões

Direct boot escreve CP15 control com `0x00052078`: bits0 (PU), 2 (D-cache) e12 (I-cache) estão zerados. `CP15Write(0x100)` conserva/aplica esses bits; `UpdatePURegions` com PU desligado concede mapa de permissões, e `ARMv5::DataRead16` checa permissão, TCM e então lê o barramento. Embora direct boot também configure atributos regionais cacheable em `0x42`, estes não ativam caches desligados globalmente. Neste estado não se identifica necessidade de flush/invalidate para a célula compartilhada. Não generalizar para payload que habilite cache/PU posteriormente ou para hardware real. [CP15.cpp: CP15Write, UpdatePURegions, DataRead16](https://github.com/JesseTG/melonDS/blob/16f127dbb587a73371827ce79689c5d237c4b59b/src/CP15.cpp), [SetupDirectBoot](https://github.com/JesseTG/melonDS/blob/16f127dbb587a73371827ce79689c5d237c4b59b/src/NDS.cpp).

Os TCM estão habilitados: direct boot DTCMSetting=`0x0300000A` dá base0x03000000/tamanho16KiB; ITCMSetting=`0x20` dá janela de32MiB de0 até endereço **menor que**0x02000000, segundo a fórmula implementada. Assim **0x02003000 não intercepta ITCM nem DTCM** e chega à main RAM. ARM7 não usa esse CP15; sua leitura/escrita halfword alinha o endereço e chama o barramento. [Cálculos TCM em CP15.cpp](https://github.com/JesseTG/melonDS/blob/16f127dbb587a73371827ce79689c5d237c4b59b/src/CP15.cpp), [ARMv4::DataRead16/DataWrite16](https://github.com/JesseTG/melonDS/blob/16f127dbb587a73371827ce79689c5d237c4b59b/src/ARM.cpp).

### ARM7 original e composição do snapshot

Direct boot copia ARM7 do ROM para ARM7RAMAddress, configura SP=0x0380FD80 e entradas de IRQ/SVC, e salta para ARM7EntryAddress. Região0x03800000 usa ARM7WRAM, inclusive fetch/leitura/escrita; reset limpa RAM, desativa IRQ por IME/IE e inicia CPSR em0xD3. O header atual aponta ARM7 load/entry para0x03800000; substituir o branch vazio por loop original de instruções ARM alinhadas é compatível com essa inicialização, sem runtime ou BIOS comercial. [NDS.cpp: SetupDirectBoot e mapa ARM7](https://github.com/JesseTG/melonDS/blob/16f127dbb587a73371827ce79689c5d237c4b59b/src/NDS.cpp), [ARM.cpp: Reset e JumpTo](https://github.com/JesseTG/melonDS/blob/16f127dbb587a73371827ce79689c5d237c4b59b/src/ARM.cpp).

Desenho proposto, somente pseudocódigo:

```text
ARM7 continuamente:
    ext = halfword_read(0x04000136)
    halfword_write(0x02003000, ext)   // apenas um escritor
ARM9 em cada atualização visual:
    low = halfword_read(0x04000130) & 0x03FF
    ext = halfword_read(0x02003000) & 0x0003
    released12 = low | (ext << 10)
    pressed12 = (~released12) & 0x0FFF
```

X ocupa região11/bit10 e usa libretro ID9; Y ocupa região12/bit11 e usa ID1. Para X sozinho, released12=`0xBFF`; Y sozinho=`0x7FF`; neutro=`0xFFF`. O ARM7 lê ambos os bits estendidos juntos e grava uma halfword, sem read-modify-write entre CPUs. Na fonte, o register EXTKEYIN é lido a partir do estado interno alto mantido por SetKeyMask; não exige a biblioteca ARM7 ou calibração de touch para X/Y. [SetKeyMask/ARM7IORead16](https://github.com/JesseTG/melonDS/blob/16f127dbb587a73371827ce79689c5d237c4b59b/src/NDS.cpp).

### Sincronização e riscos que a execução deve resolver

- Reset deixa main RAM zero; antes da primeira gravação ARM7, ext=0 pareceria X/Y pressionados. Usar warmup e estado inicial neutro validado antes dos holds. Uma célula de ready ou bit de marcador na mesma halfword pode distinguir “ARM7 nunca escreveu” de valor válido; não basta confiar em azul inicial obtido por inicialização ARM9 da célula. Se ARM9 escreve o valor inicial, fazê-lo uma única vez antes de consumir a ponte; depois ARM7 é o único escritor.
- KEYINPUT e célula estendida são leituras separadas: não há snapshot global atômico das 12 teclas. Hold por30frames e comparação de últimos frames estáveis toleram latência de amostragem; não afirmar captura simultânea perfeita. Uma halfword alinhada de único escritor reduz risco de torn value, mas não constitui garantia geral de sincronização em hardware.
- ARM7 roda continuamente e ARM9 lê repetidamente: evitar manter a célula em registrador entre frames. Assembly próprio garante carga explícita; implementação em C necessitaria semântica adequada de acesso volatile, além de decisões de cache se mudar CP15. Não adicionar cache/runtime nesta prova mínima.
- Ponte travada após boot pode produzir neutro válido sem X/Y: as fases X/Y positivas e releases detectam isso. Acrescentar negativo de supressão só X/Y (mantendo dez baixos) para demonstrar que a falha acontece nos quadrados X/Y, e eventual teste ponte congelada. O negativo global já comprova sensibilidade geral, não especificamente a ponte.
- Não acionar L2 juntamente com Y: o wrapper fecha tampa nessa combinação, confundindo oráculo. R3 e extras permanecem soltos. Não ler bits de touch/lid/debug da halfword sem mascarar `&3`.
- Revisar tamanho dos dois payloads, literal pool, sobreposição no ROM e reserva RAM0x02003000. Esse desenho altera ARM7 de loop inerte para loop de I/O/main RAM, portanto medir novamente tempo e estabilidade. JIT/fast-memory precisam ser comprovados na DLL real pelo mesmo oráculo, mesmo que a rota de leitura do interpretador esteja clara.

Cobertura planejada: **12 regiões, um boot, neutro inicial +12×(hold/release)=25 fases, 30frames/fase=750frames**. Cada hold exige alvo vermelho e onze regiões azuis; cada release exige todas azuis. A evidência ainda depende de implementação, execução local e CI real. A medida atual de90frames/0,5s é contexto de performance anterior, não resultado desta ampliação.
