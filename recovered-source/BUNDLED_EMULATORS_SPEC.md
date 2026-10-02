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

Estado: implementação em andamento. 3DS pode exigir dados do console e formatos compatíveis com Azahar. A inclusão do emulador não fornece esses dados nem substitui suas exigências.
