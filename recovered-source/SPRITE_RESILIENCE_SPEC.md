# Ciclo: sprites com rede instável

## Problema e contrato

Uma exceção de rede numa variante interrompe o download antes de tentar as fontes seguintes. A validação de tamanho ocorre depois de `ReadAsByteArrayAsync` ou `ReadAllBytesAsync`, permitindo leitura excessiva quando a resposta não informa tamanho ou o cache está corrompido.

- Falha rápida de uma fonte deve permitir a próxima variante/padrão da ordem existente.
- Um orçamento total de seis segundos vale para a busca de fontes depois de obter uma vaga de download; não criar tentativas infinitas.
- Ler no máximo 1 MB mais um byte sentinela antes de rejeitar um payload excessivo, tanto na rede como no cache. Preservar o limite de dimensão e a validação PNG existentes.
- Manter no máximo quatro downloads concorrentes e o fallback visual atual. Não modificar arquivos Pokémon ou saves.
- Testar transporte simulado com primeira fonte falhando, resposta excessiva sem tamanho declarado, prazo/cancelamento e cache inválido. Executar também a suíte Windows e empacotamento antes da publicação.

## Estado

Investigação concluída a partir de PokemonSpriteService.cs. Implementação e regressões em andamento; nenhuma afirmação de correção ou publicação deste ciclo até os checks passarem.
