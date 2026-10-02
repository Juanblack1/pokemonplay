# Pokémon Play — workflow agêntico SDD para instalador Windows

Status: implementação concluída; validação final no CI e publicação pendentes.
Data: 2026-10-02.

## Objetivo e escopo

Entregar um único arquivo de instalação `pokemon-play-win-x64-setup.exe` para quem baixa a aplicação. O instalador coloca os arquivos no computador, cria atalhos e oferece abrir o launcher. As próximas versões continuam chegando pelo atualizador interno, com preservação dos dados. Manter o ZIP portátil disponível.

Um único arquivo para download não implica um único arquivo em disco: runtime, saves e configurações continuam em pastas. Preservar os emuladores e fontes correspondentes já publicados na v171.8.0. Não incluir jogos, ROMs, BIOS ou chaves. Não alterar recursos de jogo, banco ou nuvem nesta entrega.

## Evidências do projeto

- `README.md`: download atual é `pokemon-play-win-x64-update.zip`; entrada é `PokemonPlayRuntime/Pokemons Play.exe`.
- `recovered-source/tools/publish-update.ps1`: publica runtime .NET autocontido, helper, manifesto de versão/repositório, ZIP e SHA-256.
- `.github/workflows/release-update.yml`: tags estáveis disparam build, testes e upload do ZIP.
- `.github/workflows/verify.yml`: verifica ProfilesCheck e empacotamento em pull requests.
- `recovered-source/main-app/AppPaths.cs`: raiz de dados é a pasta acima de PokemonPlayRuntime.
- `recovered-source/main-app/AppUpdateService.cs`: procura exatamente o nome do ZIP existente, verifica digest e manifesto.
- `recovered-source/updater/Program.cs`: troca apenas PokemonPlayRuntime e restaura a versão anterior se a nova não confirmar abertura.

Consequência: usar uma pasta gravável por usuário mantém o contrato atual de caminhos e atualização. Instalar em Program Files exigiria separar os dados e rever permissões; fica fora desta proposta.

## Requisitos e critérios de aceitação

| ID | Requisito | Evidência exigida |
|---|---|---|
| R1 | Instalar por usuário em `%LOCALAPPDATA%\Programs\PokemonPlay`, sem elevação, no Windows 10/11 x64. | Instalação em Windows limpo, usuário padrão e sem .NET previamente instalado; launcher abre. |
| R2 | Criar atalho no menu Iniciar e oferecer atalho na área de trabalho e abertura ao terminar. | Atalhos apontam para runtime correto e funcionam com espaços e acentos no caminho. |
| R3 | Cada tag estável gera instalador, ZIP e checksums da mesma versão e repositório. | Inspeção do manifesto e artefatos; pipeline falha antes de publicar se algum build ou teste falhar. |
| R4 | Atualizar N → N+1 pelo launcher instalado sem baixar novamente o instalador. | Duas versões locais distintas; versão nova abre e atalhos continuam válidos. Verificação real de release após publicação autorizada. |
| R5 | Preservar saves, perfis, Banco Pokémon, configurações e backups em atualização e reinstalação. | Inventário de caminhos persistentes e hashes de arquivos sentinela antes/depois de ambos os fluxos. |
| R6 | Preservar rollback e validação de integridade existentes. | Pacote corrompido, manifesto incorreto, runtime em uso e falha na abertura da versão nova não eliminam dados nem deixam o runtime anterior indisponível. |
| R7 | Desinstalar remove arquivos gerenciados e atalhos, preservando os dados pessoais por padrão. | Desinstalação após criar sentinelas; dados sobrevivem e reinstalação os reconhece. |
| R8 | ZIP portátil e instalações antigas continuam atualizando pelo contrato existente. | Atualização em uma pasta portátil com sentinelas; nova instalação não move ou apaga uma instalação portátil. |
| R9 | Distribuição contém apenas arquivos permitidos do aplicativo e emuladores existentes. | Inspeção do instalador/ZIP; sem dados locais, credenciais, ROMs ou BIOS; fontes correspondentes preservadas. |
| R10 | Documentar instalação, atualização, opção portátil e desinstalação com preservação de dados. | README corresponde ao comportamento efetivamente verificado. |
| R11 | Reinstalação respeita aplicativo/emulador aberto e evita substituir runtime em uso. | Bloqueio claro ou fechamento consentido; teste sem perda de dados. Instalador antigo não faz downgrade silencioso. |

## Plano técnico proposto

Usar Inno Setup, sujeito à confirmação técnica da ferramenta e licença na fase de arquitetura. Fixar versão da ferramenta de build e verificar sua origem/integridade; consultar documentação oficial antes de implementar comandos. O instalador consome exatamente o runtime produzido pelo script existente, evitando compilar uma segunda versão divergente.

Manter `PokemonPlayRuntime` como unidade substituível, o nome do ZIP e os manifestos atuais. Não usar PublishSingleFile como substituto do instalador. Descobrir todos os diretórios persistentes antes de definir regras de desinstalação; nunca apagar recursivamente a raiz inteira. Atualização interna pode criar arquivos que o desinstalador original não conhece: documentar e implementar limpeza delimitada ao runtime gerenciado.

Extender o script de empacotamento para produzir ambos os formatos; separar geração local de upload. Workflow de PR valida também o instalador. Workflow de tag publica os artefatos somente após os checks; preparar release em rascunho e torná-la pública apenas quando todos os uploads forem confirmados, para evitar latest incompleta.

Assinatura de código não é pré-requisito desta preparação. Se houver certificado disponível, integrar sem colocá-lo no repositório; sem certificado, registrar instalador não assinado e verificar a experiência observada no Windows. Não prometer ausência de avisos do Windows.

## Orquestração dos agentes

O coordenador mantém este contrato, integra mudanças e reúne evidências. Papéis disponíveis no runtime são usados como especialistas desta tarefa; não pressupor memória persistente ou skills automaticamente carregadas.

Antes de despachar, seguir `multiagent-development`: apresentar modelos suportados e pedir a escolha, salvo se o usuário já delegou essa decisão. Preferência sugerida: modelo atual herdado para toda a equipe. Usar no máximo três especialistas ativos além do coordenador. Nunca iniciar fases dependentes com contrato ainda indefinido.

| Fase | Agente | Responsabilidade / propriedade | Entrada | Saída e condição de parada |
|---|---|---|---|---|
| 1 | spec-author | Requisitos e inventário de dados; documentos desta pasta | Contexto do usuário, R1–R11, caminhos e fontes acima | Especificação completa, hipóteses e lacunas materiais explícitas. Sem código de produção. |
| 2 | tech-lead | Arquitetura e contratos de instalação/build/update | Especificação da fase 1 e código atual | Plano com ferramenta fixada, layout, reinstalação, desinstalação, versão e compatibilidade. Resolver contradições antes da implementação. |
| 3 | task-planner | Tarefas e matriz de rastreabilidade | Especificação e plano acordados | Tarefas pequenas com dono, dependências, critérios e comandos de validação. Todo requisito coberto. |
| 4A | implementation-engineer | `installer/` e scripts locais de build instalador; nome final acordado na fase 2 | Contrato de runtime e versão congelado | Instalador local gerado e validação estática; não editar CI ou helper sem repactuar ownership. |
| 4B | implementation-engineer | `publish-update.ps1`, workflows e README | Mesmo contrato; aguardar script de 4A para integrar build | Geração conjunta, checks e instruções; pode preparar CI em paralelo, integração depende de 4A. |
| 5 | qa | Evidências e testes de aceitação; arquivos de teste atribuídos pelo coordenador | Builds N/N+1 e R1–R11 | Matriz com comando/ambiente/resultado por requisito, bugs reproduzíveis e lacunas. Sem alegar execução em VM ausente. |
| 6 | coordenador | Integração, revisão e reparos com donos originais | Diffs reais e relatório de QA | Critérios atendidos com evidência fresca ou bloqueios específicos registrados; publicação é etapa separada. |

Brief obrigatório em cada despacho: papel, objetivo limitado, arquivos que pode editar, contratos aceitos, skills relevantes, critérios, saída esperada e condição de parada. Informar: você não está sozinho no repositório; preserve mudanças de outros e coordene alterações fora da sua propriedade. Agentes não fazem push, criam tags ou publicam releases por conta própria.

Skills por fase: SDD nas fases 1–3 e coordenação; verification-before-completion na integração e QA; TDD apenas nos comportamentos testáveis de preservação e falha quando houver alteração nesses módulos. QA revisa de forma independente; coordenador verifica os resultados, sem confiar apenas no relato dos agentes.

## Tarefas ordenadas

- [ ] T1 — Inventariar dados persistentes, configurações e instalações antigas. R5/R7/R8. Dono: spec-author.
- [ ] T2 — Fechar versão/ferramenta, layout, regras de reinstalação/downgrade e desinstalação. Depende T1; R1/R2/R7/R11. Dono: tech-lead.
- [ ] T3 — Revisar requisitos/plano e produzir tarefas rastreáveis; nenhuma contradição aberta. Depende T2; todos R. Dono: task-planner.
- [ ] T4 — Implementar instalador e geração local consumindo runtime único. Depende T3; R1/R2/R5/R7/R9/R11. Dono: agente 4A.
- [ ] T5 — Integrar build e CI de ambos os artefatos, checksums e publicação completa. Depende T3/T4 para integração; R3/R9. Dono: agente 4B.
- [ ] T6 — Preparar fixtures e testes significativos de preservação/compatibilidade/falhas. Depende T3; R4–R8/R11. Dono: qa, isolado dos arquivos dos implementadores.
- [ ] T7 — Executar instalação/reinstalação/desinstalação em Windows limpo e N → N+1, portátil e instalado. Depende T4/T5/T6; R1–R9/R11. Dono: qa.
- [ ] T8 — Corrigir falhas e repetir apenas verificações afetadas; revisar diff integrado. Depende T7; todos R. Dono: coordenador com donos originais.
- [ ] T9 — Atualizar README e registrar evidências finais. Depende T8; R10. Dono: agente 4B/coordenador.
- [ ] T10 — Publicar após autorização e conferir assets/download e atualização real. Depende T9; R3/R4. Dono: coordenador. Não publicar durante preparação.

## Verificação e convergência

Base existente: `dotnet run --configuration Release --project recovered-source/tests/ProfilesCheck/Check.csproj` e geração local por `publish-update.ps1` em diretório novo. Somar compilação/inspeção do instalador e testes Windows descritos nos critérios. Testes existentes isoladamente não demonstram que o instalador funciona.

Registrar em `evidence.md`: commit/build testado, comandos e códigos de saída, versões N/N+1, sistema/usuário, inventário e hashes antes/depois, resultados por R e limitações. Atualização deve usar dados sintéticos, sem modificar saves reais. Não marcar requisito como aprovado por inspeção quando ele exige execução.

Loop: QA identifica falha → coordenador atribui reparo ao dono → executar verificação afetada → atualizar especificação se contrato mudou → QA confirma. Encerrar implementação somente com R1–R11 cobertos, separando o smoke test pós-publicação que exige uma release real. Este arquivo prepara a execução; não comprova entrega do instalador.
