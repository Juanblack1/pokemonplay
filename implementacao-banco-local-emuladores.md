# Proposta: banco local de Pokémon para saves de emuladores

**Pesquisa e proposta para o projeto Pokemons Play — 29/09/2026**

## Escopo

Esta proposta trata apenas de um banco local que trabalha com arquivos de saves usados por emuladores. A experiência desejada é escolher/organizar Pokémon em caixas e movê-los entre jogos através dos saves. Não depende de conta, servidor, aplicativo ou armazenamento de terceiros.

## O que temos no workspace

O código C# foi recuperado por decompilação e versionado em `recovered-source/`; o bundle original está rastreado via Git LFS. É uma **reconstrução decompilada**, não o fonte original entregue pelo desenvolvedor. A metadata do projeto reconstruído indica WinForms e alvo .NET Framework 4.0.

O app já tem uma base aproveitável:

- `LauncherForm` tem a navegação principal e cria `SaveManagerView`.
- `GameCatalog.SaveGames(root)` inclui os jogos Gen 3–5 do catálogo. Embora o launcher descubra títulos 3DS, essa função atualmente os filtra e não os apresenta no gerenciador de saves.
- `SaveManagerView` lista arquivos dentro de `Saves/<nome-do-jogo>`, abre essa pasta, cria backup ZIP e oferece ações atuais de nuvem. Hoje ele organiza arquivos; não interpreta o save como equipe/caixas nem edita Pokémon.
- `LauncherSettings.ApplyVba` aponta o diretório de bateria do VBA-M para `Saves/<jogo>`; `ApplyMelon` configura `SaveFilePath` do melonDS para a pasta do jogo. Isso já oferece um ponto de descoberta para saves GBA/DS sem escrever adapters de pasta novos.

Há um descompasso técnico a resolver antes de integrar o motor: o projeto decompilado declara `net40`, enquanto o `PKHeX.Core` atual declara `net10.0`. Para integração direta, a opção mais simples a avaliar é atualizar o app WinForms para `net10.0-windows` e referenciar Core. Uma ponte por processo seria alternativa apenas se a migração não for viável; não espalhar parsing binário na UI.

## O que os projetos abertos ensinam

- [PKHeX](https://github.com/kwsch/PKHeX) fornece uma interface de desktop para abrir saves, ver/editar caixas e converter dados entre gerações. Seu README lista vários contêineres de save e arquivos individuais de Pokémon.
- [PKHeX.Core](https://github.com/kwsch/PKHeX/tree/master/PKHeX.Core) concentra parsing, dados dos jogos, leitura/gravação dos Pokémon e análise. O desenho importante é separar o motor de saves da interface gráfica.
- [OpenHome](https://github.com/andrewbenington/OpenHome) é o exemplo mais direto de banco local multiplataforma: armazena Pokémon e os move entre saves, incluindo movimentos entre gerações. O próprio README documenta conversões com perdas/ajustes e exclui certos formatos; ele também guarda dados de origem para ajudar a preservar informações ao retornar a jogos mais novos.
- [PKForge](https://github.com/sofianeelhor/PKForge) mostra uma abordagem Android com UX de banco, leitura de saves ligados a emuladores, editor e PKHeX.Core. Seu README lista suporte a emuladores GB/GBC, GBA e DS, mas essa cobertura e os formatos devem ser verificados por versão.
- [PKSM](https://github.com/FlagBrew/PKSM) demonstra um banco offline e uma lista de saves adicionais. A FAQ descreve compatibilidade de alguns saves de emulador e avisa que formatos e tamanhos variam; a própria ferramenta mantém o banco local separado de uma conexão com servidores.
- [PkManager](https://github.com/fmangela/pkmanager) demonstra uma arquitetura web + backend com PKHeX.Core e banco de dados, incluindo integração com emuladores GBA/NDS. O README descreve persistência de saves e Pokémon no servidor local, então é uma referência de arquitetura, não uma dependência necessária.

Esses projetos confirmam que o trabalho central não é desenhar caixas: é identificar corretamente cada save, parsear suas estruturas, adaptar cada Pokémon ao jogo de destino e evitar corromper arquivos. A conversão não pode ser tratada como cópia binária universal.

## Tela de save: equipe, caixas e inspeção

O usuário deve poder abrir um save e ver **a organização que existe naquele jogo**, não uma grade fixa inventada pelo aplicativo. O PKHeX Core já expõe `BoxCount`, `BoxSlotCount`, `GetBoxData`, `GetBoxSlotAtIndex` e `SetBoxSlotAtIndex`; o número de caixas vem do save específico e o número de espaços por caixa tem um padrão de 30, com exceções tratadas pelos formatos. O modelo do Pokemons Play deve consumir esses valores do motor, sem codificar “todas as versões têm N caixas”. [SaveFile.cs](https://github.com/kwsch/PKHeX/blob/master/PKHeX.Core/Saves/SaveFile.cs)

### Layout de tela sugerido

- **Faixa superior:** nome/versão do jogo, geração, arquivo aberto, status “em sincronia/alterado”, botões salvar, backup e recarregar.
- **Navegação lateral:** Equipe, Caixas do jogo, Banco local e, quando o formato tiver suporte, abas de Day Care/Nursery ou outras áreas específicas.
- **Grade da caixa:** quantidade de colunas adequada à tela, slots vazios distinguíveis, nome da caixa, paginação/navegação entre caixas. Não assumir que todo jogo tem as mesmas caixas, nomes, papel de parede ou slots protegidos.
- **Painel do Pokémon selecionado:** sprite, espécie/forma, apelido, nível, sexo, tipo, habilidade, item e resumo de golpes. Clique abre editor detalhado.
- **Busca/filtros:** espécie, forma, apelido, nível, shiny, tipo, habilidade, item, origem e atributos suportados pela geração.
- **Interação:** arrastar para outro slot da mesma caixa reorganiza; arrastar entre equipe/caixa aplica regras próprias; enviar para banco abre operação de arquivar/mover. Toda operação pode ser desfeita antes de gravar.

Importante: caixas, equipe e banco local são coleções diferentes. Equipe tem até seis posições e dados adicionais de batalha; os slots de PC e as posições protegidas variam. A camada de UI recebe slots já interpretados pelo motor, e não manipula offsets/bytes.

### Estado e salvamento da tela

Ao abrir, o aplicativo cria uma sessão de edição em memória com o snapshot/hash do arquivo. Cada alteração atualiza um estado pendente e marca a caixa ou o save como modificado; não regrava o arquivo a cada clique. “Salvar” escreve para temporário, valida com o motor e substitui o save após backup. “Descartar” restaura a sessão do último snapshot. Se o hash no disco mudar enquanto a tela estiver aberta (por exemplo, o emulador ainda está rodando), interromper a gravação e pedir para recarregar/fechar o jogo para evitar perder alterações recentes.

## Criar e editar Pokémon

Vale separar três ações que visualmente podem parecer iguais:

1. **Editar um Pokémon existente:** abrir painel com campos suportados naquele jogo e mostrar imediatamente campos inválidos/incompatíveis. Campos não existentes na geração não aparecem como se pudessem ser gravados.
2. **Criar a partir de encontro do jogo:** usuário escolhe um modelo/encounter que exista no jogo/save selecionado (selvagem, presente, inicial, ovo etc., quando suportado); o motor preenche os dados de origem e aplica as regras do encontro. Depois, usuário ajusta somente atributos compatíveis. Esse deve ser o caminho padrão de “Novo Pokémon”.
3. **Criar livre para sandbox:** permitir mexer em valores arbitrários só num modo claramente separado, com avisos de que pode produzir dados que o jogo não aceita ou que não correspondem a um encontro possível. Não habilitar essa opção por padrão em lotes.

Para gerar, o módulo deve usar as informações do treinador e do jogo do save selecionado, montar a entidade no formato dessa geração, escolher um encontro válido ou explicitamente um modelo sandbox, executar as análises disponíveis, então mostrar o resultado antes de ocupar um slot. O código do PKHeX tem templates para preencher campos-base e geradores/dados de encontro por geração; isso é uma boa referência para não inventarmos como inicializar PID, met location, ball, movimentos ou datas. [EntityTemplates.cs](https://github.com/kwsch/PKHeX/blob/master/PKHeX.Core/Editing/PKM/EntityTemplates.cs) · [EncounterGenerator.cs](https://github.com/kwsch/PKHeX/blob/master/PKHeX.Core/Legality/Encounters/Generator/EncounterGenerator.cs)

### Fluxo “Novo Pokémon”

```text
Save ativo → Novo → selecionar espécie/forma/nível
           → escolher encontro compatível do jogo ou sandbox
           → preencher atributos disponíveis nessa geração
           → checagem + prévia → escolher slot vazio
           → gravar na sessão → salvar/validar save
```

A lista de encontros deve ser filtrada pelo jogo do save, espécie, forma, região, nível e outras condições aplicáveis. Se não existir encontro possível nos dados locais, explicar isso; não “consertar” silenciosamente criando valores arbitrários. Para o MVP, pode-se limitar o criador a modelos de encontro que o motor já suporta, em vez de construir uma geração procedural própria.

### Validação e informação para o usuário

Mostrar três estados separados evita confusão:

- **Formato:** o registro pode ser lido e escrito neste save.
- **Compatibilidade com o jogo:** espécie, forma, golpes, habilidade, itens e campos cabem nas regras/capacidades do destino.
- **Análise de encontro:** os atributos combinam com um modelo de encontro conhecido nos dados do motor.

O verificador é uma análise do que está modelado, não uma prova matemática de que toda combinação é válida; dados antigos, eventos não documentados e ROM hacks podem não ter toda a informação. Permitir salvar um Pokémon com erro de análise deve exigir modo sandbox e confirmação, em vez de mostrar um selo verde enganoso.

### Regras de interação para as caixas

- **Reorganizar na caixa:** alterar somente os slots e manter o Pokémon intacto.
- **Mover caixa↔equipe:** usar funções distintas, pois equipe exige recalcular/limpar estatísticas de batalha conforme o jogo.
- **Colocar no banco local:** prévia de cópia ou movimento; a opção padrão de MVP deve copiar e preservar origem, com “mover” explícito.
- **Inserir em outro save:** converter para o formato do jogo alvo, verificar espaço/slot bloqueado e informar perda/transformação antes de aplicar.
- **Slot cheio:** não sobrescrever. Pedir destino ou permitir troca explícita dos dois Pokémon.
- **Desfazer:** manter histórico de operações na sessão (adicionar, editar, mover, trocar, excluir) até o save ser gravado; depois, restaurar por backup.

## Implementação para Pokemons Play

Adicionar uma seção **Pokémon** ao launcher, ligada ao jogo selecionado, em vez de transformar o atual browser de arquivos em uma tela que também conhece formatos binários. A nova tela usa `Saves/<jogo>` como localização inicial, carrega o save normal do emulador e apresenta equipe, caixas e slots reais daquele save. `SaveManagerView` pode continuar responsável por backup/arquivo/cloud; o editor de Pokémon é uma tela separada.

O primeiro corte deve cobrir os jogos que `GameCatalog.SaveGames` já lista (GBA e DS: gerações 3–5). Gen 6+ deve ficar desabilitado até o fluxo de save do Azahar ser explicitamente suportado: atualmente o catálogo encontra o jogo na biblioteca, mas o gerenciador não o inclui.

Se a migração para .NET 10 for aprovada na implementação, o motor sugerido é PKHeX.Core. Caso contrário, a escolha de uma versão antiga do motor ou processo externo precisa ser avaliada com protótipo pequeno antes de escrever a UI. PKHeX.Core atual está em .NET 10 e GPL-3.0-or-later, então a compatibilidade do alvo e a licença precisam ser resolvidas antes de distribuir o launcher atualizado. [Projeto PKHeX.Core](https://github.com/kwsch/PKHeX/blob/master/PKHeX.Core/PKHeX.Core.csproj)

### 1. Começar com importação manual de saves

Primeiro, permitir que a pessoa selecione um arquivo de save normal do jogo no emulador. Mostrar jogo/geração identificados, tamanho, caminho e data de modificação. Guardar um backup antes de qualquer gravação.

Não começaria varrendo diretórios de todos os emuladores. Caminhos e formatos diferem entre sistemas operacionais e versões; um seletor manual cria o fluxo principal sem amarrar o produto a uma lista extensa de emuladores. Depois, podemos adicionar adapters para localizar automaticamente saves conhecidos.

### 2. Criar um motor de saves atrás de uma interface pequena

Se a stack do projeto permitir .NET, usar **PKHeX.Core** como primeiro motor, em vez de reimplementar formatos binários e regras de cada geração. Criar uma interface interna curta, aproximadamente:

```text
identify(save) → jogo, geração, capacidade, avisos
read(save) → caixas e slots de Pokémon
previewTransfer(pokemon, targetSave) → conversão + avisos + compatibilidade
write(targetSave, changes) → novo save validado
```

O módulo esconde os detalhes de criptografia, checksums, slots e campos específicos da geração. A UI só apresenta os resultados e pede confirmação quando a conversão reportar perdas. Se Pokemons Play não for .NET, comparar custo de hospedar o Core numa biblioteca/worker local com o custo de usar OpenHome ou outro motor; não duplicar parsing por telas.

Um save desconhecido ou hack não reconhecido deve abrir em modo somente leitura ou ser recusado com motivo claro. Haverá exceções por ROM hack; elas precisam de perfil de jogo explícito, pois mesmo jogos que derivam do mesmo save base podem alterar dados e regras.

### 3. Guardar um banco próprio, independente dos saves

Para a primeira versão, um banco SQLite local é adequado para índices, caixas, ordenação e histórico. Guardar cada Pokémon como bytes exportáveis no formato reconhecido pelo motor, mais metadados de origem. Não guardar somente campos reconstruídos pela interface.

Modelo conceitual mínimo:

- **SaveSource:** caminho, hash, formato/jogo reconhecido, última leitura e backup associado.
- **Box / Slot:** organização do banco local, posição e nome.
- **StoredPokemon:** formato atual, bytes do Pokémon, save/jogo de origem, geração de origem, data de entrada e histórico de conversões.
- **TransferRecord:** origem, destino, resultado do preview, campos alterados/perdidos e estado da operação.

O arquivo individual deve ser a fonte dos dados do Pokémon; campos para busca (espécie, apelido, nível, shiny, tipos) podem ser um índice reconstruível. Uma cópia dos bytes de origem ajuda a oferecer restauração quando uma conversão entre gerações altera atributos que não existem no destino.

### 4. Fazer transferências com preview e recuperação

Toda transferência deve ocorrer nesta ordem:

1. Ler os saves de origem/destino e calcular hashes; confirmar que o emulador não está alterando o arquivo durante a operação.
2. Criar uma cópia de segurança verificável.
3. Calcular a conversão e apresentar o resumo: o que será preservado, adaptado, removido ou incompatível.
4. Gravar primeiro um arquivo temporário, reabri-lo pelo motor, validar o formato/checksum e comparar o resultado esperado.
5. Substituir o save somente depois da validação, com substituição atômica quando o sistema suportar. Registrar a operação no histórico e permitir restaurar o backup.

Se “mover” entre dois saves envolver apagar do save de origem e inserir no destino, tratar as duas gravações como uma operação recuperável: criar snapshots dos dois, registrar uma operação pendente e só marcá-la como concluída depois de ambos os saves serem reabertos e validados. Para o primeiro MVP, é mais seguro implementar **importar para o banco e inserir em destino**, com opção explícita de mover/apagar a origem; não apagar silenciosamente Pokémon durante a importação.

### 5. Separar adapters de emulador do motor

Um adapter de emulador só precisa localizar/abrir o save e, quando suportado, sincronizar o arquivo ao sair do jogo. O parser do save deve continuar sendo o mesmo para esse arquivo, independentemente de qual emulador o criou.

Ordem de suporte sugerida:

1. Arquivo selecionado manualmente, sem integração com um emulador específico.
2. Identificação e abertura direta dos caminhos mais usados pelos usuários do Pokemons Play.
3. Detecção de alteração/hash e sincronização segura ao fechar/reabrir o emulador.

Não usar **save states** como arquivo de entrada padrão: são snapshots da memória do emulador e não equivalem ao save normal do jogo. DeSmuME é um exemplo de particularidade de contêiner: `.dsv` pode incluir dados adicionais; a FAQ do PKSM cita um caso específico de 122 bytes extra. Não aplicar correção de tamanho genérica a todos os emuladores.

## Roadmap por etapas

### MVP

- Abrir save manualmente e identificar jogo/formato.
- Mostrar equipe e caixas reais daquele save, com slot vazio/ocupado e detalhes ao selecionar.
- Reorganizar Pokémon dentro do save e mover entre equipe e PC com as regras do jogo.
- Editar atributos suportados e criar Pokémon usando encontros/modelos compatíveis com o jogo selecionado.
- Importar Pokémon para caixas locais e exportar um Pokémon individual.
- Fazer preview das alterações; gravar de volta no mesmo jogo/formato somente após validação e backup restaurável.
- Incluir log de diagnóstico local e mensagem clara para formato não suportado.

### Transferência entre jogos

- Preview de conversão e regras de compatibilidade por jogo/geração.
- Escrita em save de destino e atualização segura das caixas.
- Preservação de metadados de origem e registro de mudanças.
- Operações em lote depois de validar o fluxo individual.

### Conveniência

- Adapters para diretórios de emuladores escolhidos pelos usuários.
- Filtros, caixas favoritas, etiquetas, ordenação, busca e dex.
- Perfis explícitos de ROM hacks populares, somente depois de testes com saves reais e autorizados pelo usuário.

## Decisões e riscos de engenharia

- **Licença:** PKHeX/Core, PKSM, OpenHome e PKForge têm licenças GPL no repositório. Se Pokemons Play for distribuído sob outra licença, revisar compatibilidade e obrigações antes de incorporar ou redistribuir código/binários; não presumir que chamar a biblioteca elimina a questão.
- **Perda de dados:** formatos de gerações antigas não têm todos os campos das novas. Exibir preview e armazenar dados de origem em vez de prometer transferência “perfeita”.
- **Corrupção:** gravar sobre o único save é risco alto. Manter backup com restauração testável, validar checksum/formato e detectar alteração concorrente pelo emulador.
- **ROM hacks:** identificação pelo título/assinatura é necessária; reconhecer só “parece jogo base” pode resultar em escrita destrutiva.
- **Segurança e privacidade:** saves contêm dados pessoais da campanha e treinador. Armazenar localmente por padrão; se houver modo web/hospedado, declarar claramente o que é enviado, por quanto tempo e como apagar.
- **Escopo inicial:** não implementar editor livre de IV/EV/itens como parte da primeira entrega. A prioridade é movimentar e preservar dados entre saves sem corrompê-los.

## Próximo passo para implementar no nosso projeto

O primeiro trabalho de engenharia era tornar o projeto decompilado reproduzível, migrar para `net10.0-windows` e ligar a tela de Pokémon ao launcher. O estado atual e o escopo entregue estão registrados logo abaixo. O código recuperado está sob Git local, commit inicial `78288bc`; nenhum remoto foi configurado.

## Implementação entregue em 29/09/2026

Foi feito um primeiro fluxo funcional no código recuperado:

- O projeto foi migrado para `net10.0-windows` e usa `PKHeX.Core` **26.8.26** via NuGet. A licença do pacote é GPL-3.0-or-later; revisar obrigações antes de redistribuir.
- O executável raiz foi reconstruído como **v22**. O stub agora instala o app autocontido em `PokemonPlayRuntime/`, usa `.complete-v22` para refazer uma instalação v21 existente e preserva o payload original. `recovered-source/tools/repack_bundle.py` repacota os dados em streaming e verifica o hash do app interno.
- A navegação agora tem **Banco Pokémon**. O usuário escolhe manualmente um arquivo normal de save; o leitor limita gravação às gerações 3–5 reconhecidas pelo motor e exibe as caixas reais, a equipe e detalhes básicos dos slots.
- Dá para criar um Pokémon no slot selecionado ou primeiro vazio, editar apelido/nível, mover entre PC e equipe, copiar para o banco local e inserir cópia do banco em um save quando o formato é da mesma geração.
- O banco local usa `Pokemon Bank/`, mantém arquivos PK3/PK4/PK5 criptografados no formato nativo e um `.origin.txt` com os metadados da origem. Ele fica separado dos saves do jogo.
- Ao gravar um save: compara SHA-256 com a versão aberta, monta e reabre a saída, verifica geração/checksum, cria backup `.bak-AAAAmmdd-HHmmss` e então substitui o original.
- O serviço de nuvem recuperado foi adaptado de `JavaScriptSerializer` para `System.Text.Json` para permitir a compilação na nova stack.

Limites intencionais deste primeiro corte: a criação é **sandbox livre**, sem seleção/validação de encontro; a edição expõe apenas apelido e nível; o banco mostra registros de formatos reconhecidos e mantém procedência, mas ainda não oferece busca/etiquetas; a inserção entre gerações está bloqueada para não converter com perda silenciosa; saves de gerações 6+ e ROM hacks continuam fora do suporte. A localização do save é manual e deve-se fechar o emulador antes de gravar.

Compilação local concluída com .NET SDK 10.0.401. O pacote v22 foi reaberto pelo parser e todas as 372 entradas foram verificadas; o hash do executável autocontido embutido confere. Não foram adicionados nem executados testes automatizados.

## Fontes de código consultadas

- [PKHeX](https://github.com/kwsch/PKHeX) e [PKHeX.Core](https://github.com/kwsch/PKHeX/tree/master/PKHeX.Core)
- [SaveFile.cs: leitura/escrita de slots](https://github.com/kwsch/PKHeX/blob/master/PKHeX.Core/Saves/SaveFile.cs)
- [LegalityAnalysis.cs](https://github.com/kwsch/PKHeX/blob/master/PKHeX.Core/Legality/LegalityAnalysis.cs)
- [OpenHome README](https://github.com/andrewbenington/OpenHome)
- [PKForge README](https://github.com/sofianeelhor/PKForge)
- [PKSM README](https://github.com/FlagBrew/PKSM) · [FAQ](https://github.com/FlagBrew/PKSM/wiki/FAQs) · [Settings/Extra Saves](https://github.com/FlagBrew/PKSM/wiki/Settings)
- [PkManager README e arquitetura](https://github.com/fmangela/pkmanager)
- [DeSmuME: especificação DSV](https://github.com/TASEmulators/desmume/blob/master/desmume/doc/dsv.txt)
- [dsv2sav](https://github.com/j-tai/dsv2sav)
