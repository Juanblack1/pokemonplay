# Código recuperado de Pokemons Play

O mantenedor confirmou ser o autor do aplicativo original. A reconstrução a
partir de seu próprio executável documentada abaixo faz parte da história do
projeto. O código próprio é publicado sob [GNU GPL versão 3](../LICENSE);
componentes de terceiros mantêm suas licenças originais.


## Perfis de save v47

Na v49, o **Banco Pokémon** permite escolher jogo e perfil e clicar em **Abrir perfil**, sem localizar o arquivo manualmente. Se houver mais de um save compatível, a lista de arquivos permite escolher qual abrir. O contexto do save aberto fica visível; selecionar outro perfil no banco não muda o perfil escolhido para jogar. Perfis vazios orientam a salvar dentro do jogo. Ao trocar um save com alterações pendentes, é possível salvar, descartar ou cancelar.

Na v48, clicar em **Jogar** abre a escolha de perfil antes de iniciar o emulador. O último perfil fica pré-selecionado, **Cancelar** interrompe a abertura e **Novo perfil** permite criar uma aventura sem sair desse fluxo. Para jogos 3DS, a seleção oferece apenas Principal.

Em **Meus saves**, selecione o jogo e use **Novo perfil**. É possível começar uma aventura nova, copiar o perfil ativo ou importar um save compatível. O perfil Principal conserva a pasta anterior; perfis extras ficam em `Saves/ProfileSaves/<jogo>/<id>`. A seleção é persistida em `Settings/SaveProfiles` e aplicada aos emuladores GBA/DS. Feche o emulador para criar ou trocar de perfil. A Biblioteca mostra o nome do perfil no botão Jogar.

O banco foi corrigido para ler arquivos `.pk3`, `.pk4` e `.pk5` pelo formato indicado na extensão, inclusive os arquivos criptografados gravados pelas versões anteriores. Novos arquivos de banco usam dados decifrados. A atualização do launcher preserva também a pasta `Pokemon Bank`.

Verificação reproduzível (na raiz do workspace): `dotnet run --project recovered-source/tests/ProfilesCheck/Check.csproj`. Os testes criam saves sintéticos Gen 3–5, testam Pokémon PK3–PK9 e exercitam os métodos reais de edição, banco, movimentação, gravação e backup. Também verificam isolamento de perfis, importação, configuração melonDS e bloqueio com emulador aberto. Os testes exercitam a análise de legalidade PKHeX sem alterar arquivos. O relatório orienta a revisão e não garante aceitação em jogos oficiais; saves sintéticos não representam jogabilidade real e o Firebase ainda requer teste online autenticado.

Este diretório contém uma **decompilação aproximada** do binário recebido, não os arquivos C# originais do desenvolvedor. A estrutura, nomes de classes e fluxo do programa foram recuperados da metadata .NET; comentários, organização original e alguns detalhes de formatação não podem ser reconstruídos com fidelidade.

## O que foi recuperado

- `stub/`: fonte C# decompilado do loader `PokemonsPlayStub.v21`, incluindo seu projeto decompilado.
- `main-app/`: fonte C# decompilado do aplicativo `PokemonMenu.v21`; aqui estão `LauncherForm`, `LibraryView`, `SettingsView`, `SaveManagerView`, `LauncherSettings`, `GameCatalog` e `FirebaseCloudSaveService`.
- `main-app/PokemonBankView.cs`: primeira tela do banco local de Pokémon, integrada ao PKHeX.Core para saves Gen 3–5.
- `payload/`: executável principal e arquivos auxiliares pequenos necessários para inspecionar a configuração. Nenhuma imagem ROM foi extraída para esta pasta.
- `tools/extract_payload.py`: extrator com allowlist para recuperar somente o aplicativo e configurações a partir do bundle original.

## Origem e integridade

- Bundle original: `../Pokemons Play.exe`
- SHA-256 do bundle: `26e450375e75b229f5c97855765987b1cc8e1aaa9f78f5a6bd77ea7735f5aa15`
- Marcador do pacote: `POKEMONS_PLAY_DATA_20260921_V21`
- O loader usa DEFLATE raw e um manifesto de arquivos (nome + tamanho); não é um arquivo ZIP.
- Executável principal extraído: SHA-256 `ef71a5744b9af4b752371e09b162b60f54d58c514f91cc0c1280395dcbe4f857`.
- A decompilação C# foi gerada por ILSpy/ilspycmd 11.1.0.9782. O zip de ILSpy foi obtido das releases oficiais e verificado pelo SHA-256 publicado no GitHub; o pacote NuGet foi verificado pelo hash SHA-512 do registro NuGet.

## Segurança e manutenção

- Os executáveis recuperados são artefatos de referência. Não os execute para consultar o código-fonte.
- O fluxo de login atual usa o ID público do cliente Google e PKCE, sem incluir um `client_secret` na troca de código. O segredo OAuth de versões anteriores deve ser revogado antes de tornar releases antigas públicas. As regras do Firestore também devem limitar cada usuário aos próprios dados antes de habilitar a nuvem em uma distribuição pública.
- O workspace local versiona o bundle original via Git LFS. Nenhum remoto foi configurado.
- Para novas alterações, use `main-app/` como fonte de trabalho e mantenha `stub/` como documentação de como o programa empacotado atualiza/instala os arquivos.

## Reconstrução

1. Rode `python tools/extract_payload.py "../Pokemons Play.exe" payload` para recuperar somente os caminhos permitidos.
2. Decompile o executável extraído com ILSpy: `ilspycmd -p -o main-app "payload/Pokemons Play.exe"`.
3. Não use a allowlist para copiar arquivos ROM; o extrator ignora todos os itens que não estejam listados.

## Build de desenvolvimento

O projeto `main-app/` foi migrado para `net10.0-windows` e usa PKHeX.Core 26.8.26. Com o .NET 10 SDK instalado, compile com:

```powershell
dotnet build "main-app/Pokemons Play.csproj"
```

O projeto principal pode ser executado diretamente com `dotnet run --project "main-app/Pokemons Play.csproj"`. O launcher empacotado da raiz foi reconstruído como v26: o runtime .NET 10 autocontido fica isolado em `PokemonPlayRuntime/`, enquanto as 372 entradas anteriores são preservadas pelo repacker em streaming, sem extração de ROMs para a árvore do repositório. `tools/repack_bundle.py` valida o bundle e o hash do launcher interno antes de publicar a saída.

O executável anterior continua recuperável pelo histórico Git/LFS. A instalação usa o marcador `.complete-v26`, mantendo a pasta anterior como backup ao atualizar.



## Correções visuais v24

- Capas e executáveis são resolvidos na raiz da instalação, acima de PokemonPlayRuntime.
- Tipografia Segoe UI, acentos em UTF-8 e cabeçalhos com ilustração gerada embutida na DLL.
- Biblioteca calcula a altura de cada geração conforme a quantidade de colunas.
- Repack substitui as entradas antigas do runtime em vez de acumular cópias.
- A entrada instalada da pasta principal encaminha para o runtime atual.
- Prévia dos controles reais: `"Pokemons Play.exe" --render-previews "pasta-da-instalação" "pasta-de-saída"`.


## Design v25

Componentes nativos inspirados em Tailwind e shadcn/ui: navegação lateral, botões com medidas calculadas pelo texto, seletores desenhados, slider de áudio, rodapé fixo de configurações e diálogos com labels separados. Regras em DESIGN.md na raiz. A versão aparece no título da janela.


## Identidade pixel art v26

A v26 retoma o design gerado com base no PKForge: azul-marinho, grade, bordas em degraus, navegação inferior e Pixelify Sans embutida. Conserva os componentes com tamanhos corrigidos, os campos personalizados e as capas originais. Licença da fonte em main-app/Assets/Fonts/OFL.txt.

## Banco global e contas Google

O banco global continua local e independente dos perfis de cada jogo, em `Pokemon Bank/`. No Banco Pokémon, a pessoa pode conectar uma conta Google e escolher **Enviar** ou **Restaurar**; não há sincronização automática. A sessão fica protegida pelo DPAPI do Windows neste computador. A cópia remota é um arquivo de coleção armazenado no Firebase/Firestore deste app, em um caminho por UID autenticado. Ela fica associada ao login Google, mas não aparece como arquivo no Google Drive.

O envio inclui arquivos Pokémon PK3/PK4/PK5 válidos e os metadados de origem sem caminhos locais do computador. Arquivos inválidos ou não relacionados permanecem locais e não entram na cópia. O limite atual é 4 MiB compactados, 64 MiB descompactados e 20.000 Pokémon. Restaurar valida os Pokémon antes de substituir a coleção e guarda uma cópia integral da pasta local em `Backups/Automaticos`. O usuário precisa confirmar cada envio e restauração; uma coleção local vazia não apaga a cópia remota.

O Banco global pode ser consultado e editado mesmo sem abrir um save de jogo. Para mover um Pokémon do banco para uma partida, primeiro abra o jogo e selecione o perfil/save de destino. Coleções maiores são divididas em páginas de 48 Pokémon; o filtro aceita o número da espécie, como `25`, e a ordenação pode priorizar os mais recentes ou o número da espécie. Os botões **Exportar banco ZIP** e **Importar banco ZIP** criam uma cópia portátil independente da conta Google. A importação valida toda a coleção e salva automaticamente o banco anterior em `Backups/Automaticos` antes de substituir.

Em **Meus saves**, o nome do perfil ativo pode ser alterado sem mudar a pasta de progresso, o ID usado pela nuvem ou os bytes do save.

O mesmo painel mostra a data da cópia automática mais recente do perfil ativo, quando ela existe; perfis ainda sem cópia indicam que o primeiro backup ocorre ao abrir o jogo.

Na Biblioteca, marque a estrela de um jogo para adicioná-lo aos favoritos. A escolha fica neste computador em `Settings/GameFavorites.json`; **Favoritos** filtra a coleção sem afetar os arquivos do jogo.

## Restauração de backups v58

**Restaurar backup** agora lista apenas os ZIPs criados para o jogo e o perfil ativos, com data e tamanho. Antes de substituir qualquer arquivo, o launcher abre o ZIP em uma pasta temporária e confere se cada save reconhecido corresponde ao jogo e tem checksums válidos. Um backup incompatível é recusado antes de alterar o progresso local; quando a restauração é válida, o estado atual ainda é copiado para `Saves/Backups/Automaticos`.

## Busca no banco global v59

O filtro do Banco Pokémon aceita número da espécie, apelido e nome do arquivo. A busca textual lê os dados Pokémon uma vez por arquivo e atualiza o índice quando tamanho ou data de modificação mudam, mantendo a paginação de 48 itens. A busca pelo número também confere a espécie dentro do arquivo, mesmo quando o nome do arquivo está incorreto.

## Ordenação confiável do banco v60

**Número da espécie** agora ordena e filtra usando os dados do Pokémon, não apenas o nome do arquivo. Arquivos inválidos são omitidos dessa ordenação, e a busca paginada sempre mostra o mesmo Pokémon indicado pelo resultado.

## Arquivos individuais no banco v61

O Banco Pokémon pode importar um PK3, PK4 ou PK5 válido para a coleção global sem abrir um save de jogo, e exportar o Pokémon selecionado no formato original. A importação valida checksum e espécie, guarda metadados de origem sem caminho local e deixa intacta a coleção quando o arquivo é inválido. O backup ZIP do banco continua sendo a opção para copiar ou substituir uma coleção inteira.

## Organização adicional do banco v62

Além dos mais recentes e do número da espécie, agora é possível ordenar a coleção pelo menor nível ou pela geração. As quatro opções mantêm a paginação e usam o índice local validado de cada Pokémon.

## Banco global v63

O banco global valida os arquivos antes de incluí-los na busca, ordenação, contagem e paginação. Arquivos inválidos são mantidos na pasta e não ocupam vagas; quando não há nenhum Pokémon válido, a tela explica a situação e oferece acesso à pasta do banco.

## Leitura segura do banco v64

A leitura de arquivos Pokémon também converte erros comuns de decodificação em um erro de arquivo inválido. Isso permite ignorar arquivos truncados ou malformados na lista sem fechar a tela do banco.

## Atualização da tela após restaurar o banco v65

Depois de importar um ZIP ou restaurar a cópia da conta Google, a lista global recarrega, limpa filtros antigos e remove a seleção anterior para mostrar a coleção restaurada.

## Validação única do banco v66

O índice do banco local e o módulo de backup agora usam o mesmo leitor PK3/PK4/PK5, mantendo contagem, listagem e validação da nuvem alinhadas para arquivos inválidos.

## Pré-validação do envio à nuvem v67

Antes de pedir confirmação de envio do banco, o aplicativo gera e valida uma única cópia ZIP, mostra tamanho e quantidade, bloqueia cópias acima de 4 MB e envia exatamente o arquivo confirmado. Arquivos temporários são removidos ao terminar ou cancelar.

## Detalhes antes de restaurar da nuvem v68

A confirmação de restauração do banco agora apresenta a conta, a data local da cópia remota, seu tamanho compactado, a quantidade de Pokémon e o aviso de substituição com backup local.

## Rollback de restauração do banco v69

A troca de pastas da restauração mantém rollback se a coleção validada não puder ocupar o destino. Uma falha simulada na movimentação confirma que a coleção anterior volta intacta e que as pastas temporárias são limpas.

## Rollback ao restaurar saves v70

A restauração de backup de perfil usa a mesma troca de pasta com rollback coberta por teste: se a pasta validada não puder substituir o destino, o save local anterior é restaurado.

## Perfil ativo durante o jogo v71

A janela do Pokemons Play agora mostra o jogo e o perfil de save selecionado enquanto o emulador está aberto, incluindo o título da janela e a barra superior.

## Atalho de retorno ao menu v72

A barra do jogo informa que F12 retorna ao menu após confirmação; o identificador do atalho é compartilhado no registro e na remoção da tecla.

## Proteção da sessão Google v73

A sessão protegida do Firebase é gravada primeiro em arquivo temporário e substituída no mesmo volume. Falhas de leitura, acesso ou descriptografia deixam o app abrir sem apagar o arquivo de sessão.

## Resumo de progresso ao escolher perfil v74

O seletor de perfil mostra quantos saves compatíveis há e a data do mais recente; perfis sem save exibem essa informação antes do jogo abrir.

## Diagnóstico de perfil vazio v75

Ao escolher um perfil para iniciar o jogo, o seletor agora diferencia uma pasta realmente vazia de uma pasta com arquivos que não foram reconhecidos como saves compatíveis. Isso orienta o próximo passo sem alterar nem remover arquivos existentes.

## Abrir a pasta do perfil v76

O seletor de jogo oferece um botão para abrir diretamente a pasta do perfil selecionado. Ele fica disponível quando a pasta existe e muda junto com a seleção, facilitando conferir ou organizar saves sem procurar o diretório manualmente.

## Cópia Google do banco global v77

A tela do banco agora explica sem depender de tooltip que a coleção local permanece neste computador e que a cópia remota é manual, vinculada à conta Google no Firebase do app. Ela não é salva no Google Drive nem sincronizada automaticamente.

## Confirmação ao substituir backup do banco Google v78

Antes do envio para uma conta que já possui uma cópia, a confirmação mostra a quantidade e o tamanho da coleção local e a quantidade, data e tamanho da cópia remota que será substituída. Assim fica mais fácil conferir se a conta e a versão de destino estão corretas.

## Confirmação estável da restauração Google v79

A restauração do Banco global agora verifica se a versão remota continua a mesma que foi apresentada na confirmação. Se outro dispositivo mudar a cópia nesse intervalo, o app interrompe a restauração para que a pessoa atualize os detalhes e confirme novamente.

## Identificação dos backups por perfil v80

A lista de restauração agora mostra o nome do arquivo junto com a data, o tamanho e o tipo de backup. Nomes longos são abreviados mantendo o início, o sufixo e a extensão, para que os metadados continuem visíveis e cópias próximas no tempo sejam mais fáceis de distinguir.

## Abrir a pasta do Banco Pokémon v81

A barra de ações do Banco Pokémon agora abre diretamente a pasta da coleção global local. Os arquivos continuam compartilhados entre perfis de jogo e podem ser conferidos ou copiados pelo Explorador.

## Confirmação do backup escolhido v82

A confirmação final de restauração repete o perfil e os detalhes do arquivo selecionado — nome abreviado quando longo, data, tamanho e tipo — antes de substituir os saves locais. O save atual é guardado em Backups/Automaticos.

## Identificar a conta Google dos saves v83

A tela Meus saves e as confirmações de envio/restauração mostram o e-mail Google ativo. Isso deixa claro qual conta receberá ou fornecerá o backup daquele jogo.

## Progresso por perfil em Meus saves v84

A tela de gerenciamento mostra a quantidade de saves compatíveis e a data do mais recente no perfil escolhido. Ao trocar o perfil, o resumo acompanha a seleção.

## Contexto antes de sincronizar um save v85

As confirmações de envio e restauração agora mostram o jogo, o perfil ativo e a conta Google. A restauração também lembra que o progresso local será guardado em backup antes da troca.

## Separação dos backups por perfil v86

Com a conta Google conectada, a tela Meus saves identifica jogo e perfil ativos e explica que cada perfil tem sua própria cópia privada no Firebase.

## Erros ao abrir a pasta do banco v87

Se o Explorador ou as permissões impedirem abrir a pasta do Banco Pokémon, a tela mostra o erro e mantém o aplicativo aberto.

## Identificação ao importar banco global v88

A confirmação para importar um ZIP do Banco global inclui o nome do arquivo escolhido e a quantidade de Pokémon, e lembra que a coleção atual será copiada antes da troca.

## Importar vários Pokémon ao Banco global v89

O importador do Banco global aceita selecionar vários arquivos PK3, PK4 e PK5 de uma vez. Todos são validados antes da gravação; se a lista contiver um arquivo inválido, o banco permanece sem alterações.

## Selecionar formatos mistos na importação em lote v90

O seletor oferece um filtro conjunto para PK3, PK4 e PK5, permitindo escolher arquivos de gerações diferentes na mesma operação, além dos filtros individuais.

## Banco global para PK6–PK9 v91

O Banco global aceita Pokémon PK6, PK7, PK8 e PK9 além de PK3–PK5. Leitura e exportação seguem a extensão do arquivo para distinguir formatos com tamanhos iguais, e o resumo local, os backups e o seletor em lote incluem as novas gerações. A suíte faz round-trip de importação/exportação e validação de backup para os quatro formatos.

## Filtrar o Banco global por geração v92

A barra de navegação do Banco global oferece um filtro direto para Gen 3 a Gen 9, que pode ser combinado com busca por espécie/apelido e ordenação. Se a geração selecionada não tiver arquivos válidos, a tela explica o filtro e permite voltar a todas as gerações.

## Conferir extensão e geração ao importar v93

O leitor do Banco global rejeita arquivos cujo conteúdo válido indique outra geração que a extensão PK3–PK9. A importação é interrompida antes de criar a pasta ou gravar arquivos; isso evita itens que depois não poderiam ser exportados com a extensão anunciada.

## Resultado da importação por geração v94

A mensagem após importar vários Pokémon informa o total e a quantidade por geração, e a importação de um único arquivo inclui a geração e o nome do arquivo. A mensagem de erro também cita PK3–PK9 e explica a checagem da extensão.

## Compatibilidade visível ao transferir v95

Ao selecionar um Pokémon no Banco com um save aberto, a tela informa a geração do conteúdo e do save. O botão de transferência só ativa quando os formatos coincidem; para formatos diferentes, o app identifica o bloqueio e informa que o original fica no Banco global.

## Ação do Banco explicada v96

A ação no Banco global aparece como Copiar para save, pois preserva o Pokémon original na coleção. Nas caixas do save aberto, o botão continua como Mover Pokémon, pois remove o slot de origem dentro do próprio save.

## Remover Pokémon do Banco com cópia recuperável v97

A ação Remover do banco pede confirmação e move o arquivo Pokémon e seus metadados de origem para Backups/Automaticos/Pokemon-Banco-Removidos dentro da instalação. O app recalcula a coleção e mantém o backup local para recuperação manual.

## Confirmação de composição por geração nos ZIPs v98

Antes de substituir o Banco global, o app informa o número de Pokémon de cada geração contida no ZIP, junto do nome do arquivo, total e cópia local que será guardada. A restauração revalida os formatos PK3–PK9 antes de trocar a coleção.

## Análise de legalidade com PKHeX v99

O Banco global e os slots de saves oferecem Analisar com PKHeX. A leitura gera um relatório técnico somente leitura, com quantidade de inconsistências e um aviso claro de que a análise não garante aceitação em jogos oficiais. O arquivo Pokémon não é modificado.

## Restaurar Pokémon removidos pelo app v100

A ação Restaurar removido permite selecionar um arquivo na pasta de removidos e recuperá-lo no Banco global. O app preserva o sidecar de origem; se o nome antigo já estiver ocupado, cria um nome de restauração sem sobrescrever nenhum arquivo.

## Contagem de Pokémon recuperáveis v101

O botão Restaurar removido fica desativado quando a pasta está vazia e exibe a quantidade disponível quando há arquivos para recuperar. O texto acessível acompanha a mesma contagem.
