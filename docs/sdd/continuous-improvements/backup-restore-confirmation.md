# Identificação completa do backup antes de restaurar

A lista de backups abrevia nomes longos para caber na interface. A confirmação de restauração reutiliza essa apresentação, então o trecho central pode desaparecer. Nomes distintos que compartilham o prefixo e o sufixo visíveis, perfil, tamanho e minuto podem produzir o mesmo texto de confirmação.

Manter a lista compacta. Na confirmação, exibir explicitamente o nome completo do arquivo selecionado, além do resumo já existente, do perfil-alvo e dos avisos sobre substituição e proteção automática do estado atual. Não alterar seleção, descompactação, validação, ordenação, escrita de saves ou política de backups.

- R1: dois basenames longos distintos somente no trecho truncado geram confirmações diferentes, cada uma contendo seu basename completo.
- R2: confirmação conserva resumo/tamanho/data/tipo do backup, nome do perfil, aviso de substituição e aviso da cópia automática de segurança.
- R3: nomes curtos continuam corretos e nomes longos não são truncados no campo explícito do nome completo.
- R4: os arquivos sintéticos usados na confirmação são somente lidos e permanecem byte a byte inalterados.
- R5: teste primeiro reproduz colisão real nas confirmações geradas; correção passa teste focalizado e suíte/pacote/verificador de instalação no Windows CI. PR fica pronto para revisão.

O fluxo de restauração continua sem publicação de release nova: a instrução atual do usuário proíbe criar tags e publicar pacotes. Não há teste com saves reais de usuário, payloads de ROM, dados pessoais ou escrita no destino. A restauração em produção não será executada pelo teste.
