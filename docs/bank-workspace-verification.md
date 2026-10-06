# Banco v172.0.4 — verificação

Referências estudadas: https://github.com/sofianeelhor/PKForge/wiki/The-Box-View e https://github.com/sofianeelhor/PKForge/wiki/The-Bank. Screenshot box-annotated.png inspecionado no browser. Código próprio, sem copiar fontes ou assets da aplicação.

Implementação: rail de save/perfil à esquerda; lista de caixas com ocupação; equipe e coleção global separadas; grade fixa 6x5 com sprites grandes e transparência recortada; inspector em todos os modos; busca/ordenação e filtros avançados; páginas de 30; setas e duplo clique; escolha explícita de caixa/espaço e opção de copiar; destino Equipe para Pokémon das caixas do save; escrita pendente, backups e conflitos mantidos.

--bank-editor passou antes do último ajuste de compactação. Verificou editor PK3–PK9, reconhecimento contextual SRAM LeafGreen, isolamento de perfil, abertura no banco, nenhum byte gravado no save, cópia para caixa escolhida mantendo origem, rejeição de destino ocupado, proteção do último Pokémon da equipe, grades 6x5 sem rolagem em 1000/1280/1920, coleção em páginas 30+10 e busca por Pikachu. Capturas incluem dados sintéticos em cópia temporária/em memória.

A revisão final compilou e publicou com exit 0 (aviso preexistente CS0108). Tentativas finais do executável Check foram bloqueadas antes de Main por Controle de Aplicativos do Windows, 0x800711C7. Não existe resultado aprovado para o novo caso compacto de altura 560 ou para o destino Equipe nesta revisão. Políticas de segurança não foram alteradas.

Runtime final self-contained executou --render-previews, exit 0, produzindo as telas incluindo pokemon.png. A distribuição final abriu nesta máquina, diferentemente do pacote v172.0.3. Não houve instalação sobre dados existentes. git diff --check aprovado.

## Publicação — 06/10/2026

Verificação local atualizada de --bank-editor: exit 0 com reconhecimento de uma cópia do SRAM LeafGreen, isolamento de perfis, transferências pendentes sem escrita do original, cópia e movimento de box para equipe, grade 6x5 em 1000x560/1000x700/1280x800/1920x1000 e coleção 30+10 com busca. O editor PK3–PK9 e os encontros legais passaram novamente.

A suíte de perfis recebeu ajustes para interagir com o editor novo, escolher destinos explicitamente e navegar pelas páginas de 30. A fixture SRAM Emerald inclui os identificadores de formato necessários à detecção nativa PKHeX. --profile-bank permite executar o trecho de perfis/banco separadamente da verificação de foco externo. A suíte completa e os pacotes serão conferidos no CI do PR #47 e da tag v172.0.4.
