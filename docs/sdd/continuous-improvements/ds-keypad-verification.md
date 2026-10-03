# Próximo ciclo — prova dos 12 botões DS

Reprodução: CI37099265795 no commit d64737b reprovou a ROM antiga em a-held: expected_mask1, observed_mask4095. Ela pintava a tela inteira ao pressionar A e não distingue as outras regiões. A falha comprova insuficiência da prova anterior, não defeito dos 12 botões do produto. O FULL red37099265792 foi cancelado após essa prova nativa para evitar recompilação inútil.

Implementação em andamento: ARM9 desenha a matriz e espera ready; ARM7 publica EXTKEYIN em RAM compartilhada. Quatro testes locais do oráculo/bounds passaram (incluem12alvos, botão extra indevido, dois frames/estabilidade, callback duplicado, formato/stride e segmentos determinísticos/limitados). Ainda depende da execução nativa e do CI completo; não afirmar X/Y validados antes disso.

Objetivo: ampliar a verificação no núcleo real para A, B, Select, Start, Right, Left, Up, Down, R, L, X e Y, com programa ARM9/ARM7 próprio sem conteúdo comercial. Não alterar bindings de produto sem defeito reproduzido.

R1: um boot mostra 12 regiões independentes. Cada botão pressionado acende somente sua região vermelha; as outras ficam azuis. Soltar deixa todas azuis.
R2: X/Y são lidos por um loop ARM7 original em EXTKEYIN e publicados em meia palavra na RAM principal compartilhada0x02003000; ARM9 combina estes bits com os dez de KEYINPUT. Marcador ready separado impede tratar RAM zerada como X/Y pressionados. Sem IRQ/BIOS/SDK comercial.
R3: 25 fases (neutro + pressionar/soltar12) com30frames cada, dentro de750frames. Cada fase exige callbacks reais novos e pelo menos dois frames finais consecutivos concordando com a expectativa independente. Não aceitar callback duplicado/último buffer da fase anterior.
R4: negativos independentes de input suprimido, mapper A/B trocado, botão preso ao soltar e supressão somente de X/Y devem falhar no oráculo específico esperado, preservando evidência das regiões. No negativo de X/Y, os dez botões inferiores precisam passar antes da falha em X. Falha de DLL/config/boot não conta como negativo válido.
R5: ROM determinística128KiB sem logo/banner/filesystem comercial; segmentos ARM9/ARM7 disjuntos e payload limitado0x200 com erro explícito se exceder; CRC/header válido. Hashes gerador/ROM/core e tempos reais no relatório. Nomear PNG por fase/botão.
R6: manter opções DS builtin/direct/software, dados isolados e rede desativada; GBA/serialize atual preservado. Timeout externo3min, interno120s. Não diminuir asserções para fazer o CI passar.
R7: executar núcleo distribuído verificado, controle negativo e build novo, suíte da aplicação/pacote/instalador; publicar uma atualização após passar. Publicar releases anteriores em ordem antes desta.

Evidência primária em output/ds-keypad-research.md. Mesmo se todos os critérios passarem, a prova cobre somente callbacks ao núcleo e efeito visual nessa ROM original; não valida hardware físico, touch, áudio audível, integração do launcher ou compatibilidade de ROMs comerciais.

Desenho candidato: grade4x3 de quadrados16x16 no framebuffer superior, destinos em tabela. ARM7 loop ldrh EXTKEYIN/strh sharedcell + ready; ARM9 lê snapshot, combina bits e pinta. ROM atual cabe em512bytes de ARM9; verificar novo tamanho explicitamente. RAM principal é compartilhada, directboot do core fixado deixa caches/PU desligados; snapshots low/ext não são globalmente atômicos, portanto usar entradas estáveis e dois frames reais coerentes. Ainda sem implementação ou execução dessa ampliação.

Notas técnicas para implementação posterior (hipóteses de instruções a validar por execução, não prova):
- Regiões na ordem física DSbits0..11: A/B/Select/Start/Right/Left/Up/Down/R/L/X/Y. Destino VRAM0x06800000+2*(x+256*y), x=16+56*(i%4), y=16+56*(i//4). Amostrar grade3x3 interna por região, além de formato/pitch/256x384; não depender só de hash.
- ARM7: literais r0=EXTKEYIN, r1=sharedcell, r3=ready0x5044; loop ldrh r2,[r0] / strh r2,[r1] / strh r3,[r1,#2] / branch. ARM9 aguarda ready antes de pintar. ARM9 não escreve o valor neutro para simular sucesso da ponte.
- ARM9 snapshot: KEYINPUT em r4 &0x3FF, sharedcell em r5 &3, ORR r4,r4,r5,LSL10. TST r4,mask => zero significa held; LDR condicional EQ red0x001F / NE blue0x7C00. Máscara começa1 e desloca1 por região. Tabela de12destinos resolve endereço após código/literais; exigir pool PC relativo0..4095.
- Raster16x16: STRH pixel/postinc2, contador16colunas, adicionar480bytes ao fim da linha (stride512 menos32pixelsbytes), contador16linhas. Recarregar destino da tabela em cada região. Payload ARM9 e ARM7 devem caber separadamente0x200; gerar erro, nunca deixar ljust/assignment estender ROM e deslocar segmento ARM7.
- Framebuffer: serial novo apenas quando callback contém pixels reais, manter últimos dois snapshots novos dentro de cada fase; callback NULL/duplicado não vale. Esperado da fase independente do mapper mutante. Registrar expected/observed masks e falhas por região, nomes únicos de PNG. GBA mantém comportamento atual.
- Negativos: swapA/B modifica somente callback (IDs8/0), não expectativa; sticky mantémA no primeiro release; suppressXY mantém dez baixos funcionando e falha no holdX (22a fase, após21passantes); global suppress falha no primeiroAheld. Verificar erro/phase/regiões específicos no wrapperCI e limpar exitcode esperado apenas depois das asserções.
