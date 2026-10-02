# Instalador Windows — implementação v171.9.0

O baseline efetivo é v171.8.0, commit ae0adcfb6edaf2a5699a88635cd4b66723d0ab35. Ele já inclui emuladores e fontes correspondentes; esse contrato publicado substitui a hipótese inicial do workflow que excluía emuladores. ROMs, BIOS, chaves e dados pessoais continuam excluídos.

Instalador Inno Setup 6.7.3 por usuário, com compilador e download fixados por SHA-256. Runtime, ZIP portátil e instalador têm a mesma versão. Reinstalação verifica versão e arquivos em uso; desinstalação limita a limpeza ao runtime e aos arquivos gerenciados.

O ZIP v171.8.0 tinha 15.136 entradas, acima das 4.096 aceitas pelos launchers existentes. O novo ZIP de atualização inclui a árvore completa de emuladores em `emulators-runtime.zip`. O launcher extrai esse arquivo em staging, valida caminhos, limites e componentes obrigatórios e move a árvore antes de sinalizar abertura ao helper. ZIP portátil e instalador mantêm os arquivos expandidos. Dados do usuário continuam fora do runtime. Há testes de rejeição de caminhos, duplicatas e links e de recuperação após interrupção.

Publicação usa release em rascunho até confirmar upload dos oito assets; só então passa a latest. CI verifica a suíte existente e o ciclo instalar/reinstalar/atualizar/rejeitar downgrade/desinstalar/reinstalar com sentinelas e hashes. Teste separado executa o helper antigo contra o pacote novo e verifica readiness e preservação.

Evidências locais: suíte de baseline aprovada; compilação inicial do instalador aprovada; instalação local e arquivos dos emuladores confirmados. O primeiro teste encontrou limite de caminhos do Inno em fixtures extensas; os diretórios temporários foram encurtados. O segundo identificou um instalador compilado antes da correção do grupo de atalhos; a versão integrada usa `{group}`. A execução da suíte recompilada foi bloqueada por App Control (0x800711C7) deste Windows; não foi alterada a política. Resultados finais devem ser confirmados no CI antes da publicação. Não há VM local limpa Windows 10/11 disponível nem teste de jogabilidade com ROM real.
