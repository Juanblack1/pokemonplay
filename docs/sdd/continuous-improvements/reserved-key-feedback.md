# Feedback para tecla reservada

KeyCaptureDialog ignora F12 sem explicar a recusa, embora GameHostForm reserve F12 para voltar ao menu. Este ciclo melhora somente a clareza da captura.

Ao receber F12, manter o diálogo aberto e mostrar no texto e em seu objeto acessível que F12 está reservado para voltar ao menu e que outra tecla deve ser escolhida. Preservar o contexto da ação e a instrução de Escape. Não atribuir F12, alterar layout ou criar persistência adicional.

- R1: F12 apresenta explicação específica atual; CapturedKey permanece null e DialogResult None.
- R2: F12 seguido de Escape cancela sem mudar mapeamento, preset ou arquivo de configuração.
- R3: F12 seguido de tecla válida confirma apenas a tecla válida pelo fluxo real SettingsView.
- R4: a instrução inicial e a explicação mantêm ação e Escape. Repetir F12 é seguro e não encerra o diálogo.
- R5: primeiro reproduzir a falta de feedback em teste Windows com modal real e AccessibilityObject; depois aplicar correção mínima e passar suíte, build, pacote e verificador de instalação/atualização no CI. Criar PR pronta para revisão. Após integração, publicar a próxima atualização autorizada pelo usuário e verificar release, assets e hashes públicos.

Teste usa configurações sintéticas e handlers existentes do modal, sem ROMs, emulador ou dados pessoais. Dispatch do handler e consulta programática de acessibilidade não comprovam teclado físico ou sessão Narrator. App Control impede execução nativa local; build local e execução Windows CI são os canais de validação.
