# Banco e editor Pokémon

Preservar a integração de emuladores feita nesta sessão e a identidade pixel do launcher. O editor usa a organização familiar ao PKHeX: Pokémon, encontro, atributos, golpes, treinador e banco de encontros, com campos legíveis e ações sempre visíveis.

1. Um save válido de LeafGreen em `.srm` dentro do perfil é reconhecido e aberto como LeafGreen. FireRed/LeafGreen compartilham formato; usar o contexto do perfil para resolver a variante. Manter isolamento de perfis e backups/hash antes de gravar. Leitura nunca grava no save.
2. Criar Pokémon aparece como ação principal, inclusive sem save aberto no Banco global. Editar também abre por duplo clique. Cancelar o editor preserva o registro original. Aplicar atualiza uma cópia; no save a alteração fica pendente até Salvar com backup.
3. Editor permite espécie, forma, apelido, nível, natureza, sexo, habilidade, item, treinador/IDs, PID, encontro/ball, IVs/EVs e quatro golpes/PP. Limites seguem o formato. Preservar campos não editados. Análise considera os campos atuais, informa inconsistências e não garante proveniência real.
4. Banco de encontros consulta dados reais do PKHeX filtrados por espécie e versão, mostra nível/local/tipo e permite carregar um modelo compatível no rascunho. Verificar legalidade depois da geração. Não prometer que um registro gerado foi capturado em um jogo.
5. Banco oferece seleção de jogo/perfil visível, atualizar saves, criar, encontro e análise sem menus escondidos. Gravar mantém a defesa contra mudanças externas e backup.

Verificação: regressão com cópia/leituras do `.srm` relatado; fixtures de perfis separados; clone/cancel/aplicar do editor; encontros reais Gen3 e análise; limites de EVs; renderização compacta e ampla. Pacote local acumulativo, sem publicação externa.
