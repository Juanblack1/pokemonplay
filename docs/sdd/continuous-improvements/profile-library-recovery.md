# Ciclo 6 — perfis inválidos não derrubam a biblioteca

Problema: GameCard lê perfis no construtor, inclusive para 3DS, cuja apresentação não usa essa seleção. Um JSON de perfis malformado pode impedir a biblioteca inteira de abrir ou causar exceção no timer de busca.

Critérios: R1 um arquivo de perfis inválido/inacessível não impede os demais cartões; R2 cartão afetado indica Verificar perfis e descreve recuperação, sem assumir silenciosamente perfil Principal; R3 iniciar revalida e bloqueia antes de preparar emulador se os perfis continuam inválidos; R4 arquivo original e saves permanecem byte a byte; R5 apresentação 3DS não lê metadata de perfis do app que não usa; GBA/DS importados continuam validando essa metadata porque seu backend atual a utiliza; R6 perfis válidos mantêm nome/semântica e histórico acessível; suíte passa.

Plano: apresentação de perfil somente de leitura, usada na criação e antes de iniciar o jogo; diagnosticar apenas exceções esperadas de leitura/validação, sem esconder erros de programação. Não reparar, resetar ou selecionar perfil automaticamente. Testes sintéticos e inspeção de aviso no cartão, sem executar emuladores/ROMs.


Verificação: antes da correção, a fixture reproduziu JsonException no construtor de GameCard e impediu LibraryView de abrir. Depois, a suíte local completa passou com 532 verificações; nove cobrem perfis inválidos, jogo vizinho válido, revalidação após correção explícita na fixture, preservação de bytes, perfil bloqueado por outro processo, GBA importado e 3DS. O cartão renderizado exibe Verificar perfis com texto vermelho sobre superfície escura e orientação acessível. A suíte não executou uma ROM comercial nem um controle físico. A limpeza dos quatro modos focados valida o diretório temporário antes de excluir fixtures.
