# Ciclo: sprites com rede instável

## Problema e contrato

Uma exceção de rede numa variante interrompe o download antes de tentar as fontes seguintes. A validação de tamanho ocorre depois de `ReadAsByteArrayAsync` ou `ReadAllBytesAsync`, permitindo leitura excessiva quando a resposta não informa tamanho ou o cache está corrompido.

- Falha rápida de uma fonte deve permitir a próxima variante/padrão da ordem existente.
- Um orçamento total de seis segundos vale para a busca de fontes depois de obter uma vaga de download; não criar tentativas infinitas.
- Ler no máximo 1 MB mais um byte sentinela antes de rejeitar um payload excessivo, tanto na rede como no cache. Preservar o limite de dimensão e a validação PNG existentes.
- Manter no máximo quatro downloads concorrentes e o fallback visual atual. Não modificar arquivos Pokémon ou saves.
- Testar transporte simulado com primeira fonte falhando, resposta excessiva sem tamanho declarado, prazo/cancelamento e cache inválido. Executar também a suíte Windows e empacotamento antes da publicação.

## Estado

O [run 37047750380](https://github.com/Juanblack1/pokemonplay/actions/runs/37047750380) aprovou `9aa4bf65d48a10144546a53a674a7905a65be2ec`, incluindo cinco regressões determinísticas, suíte completa, empacotamento e Firestore. A primeira fonte simulada falhou e a segunda retornou PNG válido. Uma resposta sem Content-Length consumiu exatamente 1 MB mais um byte antes da rejeição; a próxima fonte permaneceu disponível. O cache excessivo foi rejeitado e removido após liberar o arquivo. A fonte parada foi cancelada pelo orçamento total, sem novas requisições. Não houve teste de rede instável real. As fixtures do runner foram removidas ao terminar.
