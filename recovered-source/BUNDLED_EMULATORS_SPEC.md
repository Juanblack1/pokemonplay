# Emuladores incluídos na distribuição

Pedido: primeira instalação não deve exigir instalar emuladores nem baixar cores.

## Contrato e critérios

1. Os ZIPs portátil e de atualização incluem RetroArch x64, core mGBA, core melonDS DS e Azahar x64, com dependências e licenças. Sem ROMs, saves, BIOS, firmware ou credenciais do proprietário.
2. Sem configuração prévia, GBA/DS usam os componentes incluídos automaticamente. A instalação portátil pode mudar de pasta sem caminhos persistidos quebrados. Configurações externas explícitas continuam prevalecendo.
3. 3DS usa Azahar incluído quando não existe o emulador legado. Dados do Azahar permanecem no diretório padrão do usuário, fora do runtime atualizado. Configuração/conta RetroArch em Settings/Emulators; saves no perfil existente.
4. Downloads upstream têm versões e hashes fixos; cores são compilados dos fontes e dependências capturados no mesmo build. Fontes correspondentes e instruções são publicados junto da release.
5. Windows CI verifica seleção automática, mobilidade, preferências existentes, comandos e estrutura dos ZIPs. Executáveis reais devem abrir sem DLLs ausentes; não confundir teste sem ROM com compatibilidade de todos os jogos.

## Plano

- Provisionar RetroArch e Azahar oficiais; compilar os dois cores com MinGW no CI e arquivar fontes/dependências.
- Detectar runtime incluído no aplicativo; preservar configurações fora do runtime.
- Testes de integração com fixtures e smoke de componentes reais no CI; atualizar documentação e publicar após checks.

Estado: PR #17 integrado como `c15efd6524ee25b35b9675f13258eec4608e300d`. Windows CI `37051941873`, head `e7fa33cd5f0b240c5019a3816b6a918beea7d581`: ambos os cores compilados e carregados com ABI Libretro 1, RetroArch --version concluído, Azahar permaneceu aberto sem ROM, suíte ProfilesCheck completa passou, dois ZIPs gerados/validados. Firestore passou nos 79 checks existentes. Seleção automática, nomes oficiais dos cores, mudança de pasta portátil, preferências externas/opt-out, configuração persistente, preset de teclado e fallback 3DS foram exercitados em fixtures. Não houve teste de jogabilidade com ROM real.

3DS pode exigir dados do console e formatos compatíveis com Azahar. A inclusão do emulador não fornece esses dados nem substitui suas exigências. Os componentes nativos e fontes são provisionados no runner efêmero do CI; nenhum pacote grande foi baixado no computador do proprietário.
