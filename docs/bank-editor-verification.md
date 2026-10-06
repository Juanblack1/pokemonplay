# Verificação local v172.0.3

- Release build: aprovado, 1 aviso CS0108 preexistente em InputWorkbench.Capture.
- --bank-editor: aprovado. Inclui formatos PK3 a PK9, clone inalterado, edições de natureza/sexo/nível/IV/OT, EV >510 rejeitado, Pikachu LG da base PKHeX com análise válida, perfil isolado, reconhecimento do SRAM LeafGreen real e abertura no banco usando cópia temporária sem escrita do save.
- --game-window-ux, --embedded-session-config, --game-controls: aprovados.
- Prévias de editor, encontros e banco com equipe: inspecionadas.
- --retro-achievements: falhou por timeout em Owned child foreground did not pause actual launcher session clock; não chegou aos testes de SRAM posteriores.
- Runtime win-x64 self-contained compilado. Smoke test --render-previews bloqueado antes de Main por Controle de Aplicativos do Windows, FileLoadException 0x800711C7. Evento Application 1026 confirmado. Não alterada política de proteção. O pacote não é confirmado executável neste computador.
- Não executada validação manual de jogabilidade com ROM e emuladores reais; não instalados arquivos por cima do runtime do usuário.
