# Biblioteca: mostrar a contagem dos resultados

## Contexto

O banner da Biblioteca informa a quantidade total de jogos prontos para jogar. Quando uma consulta ou filtro reduz a lista, esse total permanece igual, então a pessoa não consegue distinguir uma lista curta de uma busca que encontrou poucos jogos. A pesquisa na biblioteca já filtra a coleção existente e limpar o texto restaura os itens. Exibir quantos resultados restam é uma decisão de UX derivada dessa orientação, não uma exigência da Microsoft. Referência: [Microsoft Windows Search Boxes](https://learn.microsoft.com/en-us/windows/win32/uxguide/ctrl-search-boxes).

## Requisitos e aceitação

- P1: enquanto qualquer consulta ou filtro estiver ativo, o banner mostra e expõe para acessibilidade a contagem de jogos encontrados em relação ao total da biblioteca (por exemplo, “1 de 10 jogos encontrados”).
- P2: uma busca sem resultados informa zero; o empty state existente e as ações de limpar continuam intactos.
- P3: sem consulta ou filtro, preservar o texto atual do banner; depois de atualizar o catálogo, preservar também o horário de atualização.
- P4: contar jogos correspondentes, sem incluir avisos, títulos de seção ou painéis de empty state.
- P5: cobrir estado inicial, resultado único, resultado zero, limpeza e atualização; executar ProfilesCheck em Windows CI.

## Limites

Não alterar pesquisa, ordenação, catálogo, ROMs, saves, histórico, dados persistidos ou comportamento de lançamento. Usar apenas os títulos já presentes no catálogo padrão como fixture. Sem conteúdo pessoal de ROM no teste.

## Verificação

`FirstUseCheck` instancia a LibraryView em pasta temporária, lê o `AccessibleDescription` do banner, consulta “Emerald”, consulta termo inexistente, limpa a busca e valida os textos correspondentes. O check existente compila e executa no Windows CI.

## Evidência TDD

O teste foi enviado antes da implementação no commit `a933f86`; o Windows CI [37157955799](https://github.com/Juanblack1/pokemonplay/actions/runs/37157955799) falhou na primeira nova asserção porque o banner não expunha o resumo completo do catálogo. Isso confirma a lacuna de acessibilidade antes da mudança de produto.
