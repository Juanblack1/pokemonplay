# Atualizações automáticas

Ao abrir o launcher, ele verifica a release estável do repositório configurado e baixa uma versão nova em segundo plano. O cabeçalho mostra o progresso e, ao concluir, oferece **Reiniciar e atualizar**. Sem internet, o app continua disponível; o diálogo de atualizações permite verificar e tentar novamente.

Se a pessoa fechar o launcher, o pacote validado permanece preparado. Na próxima abertura normal, ele é instalado antes de iniciar o launcher. Jogos ativos impedem a instalação. Reiniciar pelo botão exige encerrar os emuladores e salvar alterações no banco Pokémon.

O download verifica tamanho, SHA-256 e manifesto. O pacote preparado registra hashes e é revalidado antes da instalação automática. Falhas de instalação não entram em um ciclo de tentativas. A substituição fica limitada ao runtime e mantém a recuperação da versão anterior, ROMs, configurações e saves.

Validação: AppUpdatesCheck cobre download automático no launcher, concorrência, persistência, alteração de arquivos, origem, cancelamento, integridade e recuperação do instalador. O pacote público não inclui ROMs ou dados pessoais.
