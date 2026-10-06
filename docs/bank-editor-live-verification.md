# Banco e editor v172.0.5 — verificação

Preserva a identidade Pixel. O editor Conjunto reúne espécie, nível, natureza, habilidade, item, golpes e controles EV/IV com barras, inspirado na organização do teambuilder do Showdown. Identidade, encontro, PP/reaprendizado, treinador, base e relatório mantêm suas funções em seções com botões do app. Campos aceitam busca por nome/número; texto sem correspondência bloqueia aplicar.

EVs mostram total e orçamento restante durante a edição. Acima de 510 ou do máximo individual do formato, campos e mensagens ficam vermelhos e aplicar é desativado. O limite é obtido de PKM.MaxEV; valores inválidos importados não são truncados silenciosamente. O usuário pode corrigir o rascunho sem alterar a origem. Legalidade é calculada pelo PKHeX depois de 350 ms; alterações invalidam resultados antigos. O relatório distingue regras conhecidas de prova de captura real.

O banco conserva a grade 6×5, mostra nomes onde couberem e tooltip completo, restringe os botões nas extremidades, oferece análise automática no inspector e sinaliza alterações pendentes. Ctrl+S salva com backup; F2 abre a edição selecionada. Transferências e gravação explícita com detecção de conflitos continuam existentes.

Verificação local em 06/10/2026:

- Build Release: zero erros; aviso CS0108 preexistente em InputWorkbench.
- ProfilesCheck --bank-editor: 81 verificações aprovadas; PK3–PK9, limites, correção, valor importado inválido, campos sem correspondência, atualização real de legalidade e descarte de resultado antigo.
- ProfilesCheck --profile-bank: 156 verificações aprovadas; importação/exportação, perfis, transferências e backups.
- SRAM LeafGreen real utilizado apenas em cópia/read-only: reconhecimento e conteúdo original preservados.
- Grade 30 espaços sem rolagem em 1000×560, 1000×700, 1280×800 e 1920×1000. Editor em 1000×700 e 1180×780; a menor janela usa rolagem para manter todos os campos acessíveis.
- Capturas DrawToBitmap de controles reais, não simulação de jogabilidade: output/bank-live-ux em Z:/PokemonPlay.

Referências: https://www.smogon.com/player/issue5/the-teambuilder e código oficial PKHeX EffortValueVerifier. CI do PR confere a suíte completa e os pacotes antes da publicação.
