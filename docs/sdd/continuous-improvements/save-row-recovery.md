# Ciclo 11 — arquivo removido durante a exibição de saves

Problema observado no código: SaveFileRow consulta FileInfo.Length durante OnPaint sem tratar falhas. Um arquivo removido entre a descoberta e a primeira pintura pode provocar FileNotFoundException na renderização da tela.

Critérios: R1 a pintura de uma linha cujo arquivo desapareceu não lança exceção; R2 a linha indica arquivo indisponível com descrição acessível e não apresenta tamanho/data inventados; R3 o mesmo controle recupera os metadados após o arquivo ser restaurado externamente; R4 uma linha normal mantém nome, tamanho e data; R5 nenhuma pintura recria, grava ou remove o arquivo; R6 suíte completa, inspeção visual e atualização publicada depois da validação.

Plano: reproduzir por desenho real em bitmap após remover um arquivo sintético. Atualizar os metadados em cada pintura com tratamento somente das falhas esperadas de leitura. Não alterar os algoritmos de backup ou fazer afirmações sobre controles físicos/ROMs comerciais.
