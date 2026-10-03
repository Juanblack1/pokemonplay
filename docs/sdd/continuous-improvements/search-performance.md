# Ciclo 5 — pesquisa sem reconstruções por tecla

Problema observado no código: cada TextChanged de LibraryView chama Rebuild, descarta os cartões e relê capas/perfis/favoritos. Objetivo: agrupar digitação rápida em uma reconstrução, preservando resultado, foco e ações explícitas.

Critérios:
- P1: digitação consecutiva em janela criada não descarta os cartões antes de um intervalo curto sem nova tecla (150ms); depois aplica a consulta final.
- P2: Enter aplica a busca pendente imediatamente sem iniciar um jogo; Esc ou campo vazio restaura resultados imediatamente.
- P3: alterar sistema/favoritos/ordenação ou atualizar catálogo aplica o texto atual e cancela a reconstrução pendente.
- P4: após ocultar e mostrar a biblioteca, o texto atual é aplicado; fechar a tela libera timer e impede callback sobre controles descartados.
- P5: preservar busca combinada, acentos, atalhos, foco e avisos de ROM indisponível; suíte completa passa.

Plano: Timer WinForms privado de uso pontual; eventos de digitação agendam reiniciando prazo; Rebuild cancela qualquer agendamento. A consulta antes de criar handles pode permanecer síncrona para construção da tela. O evento da janela Enter/KeyDown deve consumir Enter somente quando o campo de busca contém foco. Não introduzir cache mutável de saves nem mudar emuladores.

Verificação: fixture LibraryView dentro de Form com handles, sequência de três mudanças de busca e observação do descarte dos cartões; esperar resultado com deadline de 2 segundos; ações explícitas sem espera; fechamento de janela durante agendamento. Testes não medem desempenho de ROMs.

Evidência: teste antes da mudança falhou ao observar descarte imediato dos cartões durante três alterações rápidas. Depois passaram 13 verificações focadas, incluindo Enter, Esc, sistema, ocultar/mostrar e descarte com timer pendente. A primeira suíte completa mostrou duas expectativas antigas de atualização síncrona: preservar sincronismo para mudanças sem foco resolveu uma delas; a verificação de digitação focada agora aguarda o resultado por até 2s, mantendo a assert de filtro/foco. Suíte completa local final: 523 PASS, exit 0, ALL CHECKS PASSED (`output/search-performance-full-local.log`). Não há medição de FPS ou tempo de ROM nesta alteração.
