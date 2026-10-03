# C17 — consultar atribuições de controles pela acessibilidade

Problema: KeyBindingRow desenha a tecla/botão, mas expõe somente o nome da ação. Quem consulta seu objeto acessível não recebe o mapeamento corrente nem a condição de edição. O ciclo melhora essa informação sem mudar bindings, navegação ou aparência.

Escopo: manter AccessibleName da ação e expor AccessibleDescription atualizada com atribuição e instrução Enter/Espaço somente quando Editable. Valor vazio deve ter indicação clara de não atribuído. Não anunciar Active/estados de gameplay a cada quadro. Não criar redesenho, persistência adicional ou comportamento novo de teclado.

Requisitos e aceitação:

- R1: objeto acessível de cada linha visível expõe ação e atribuição corrente, incluindo X/Y quando presentes nos modelos DS/3DS; GBA preserva suas dez ações navegáveis.
- R2: SetKey e troca de Editable atualizam a descrição da própria linha; presets, modo teclado/controle e restauração de padrões não deixam informação antiga.
- R3: Enter/Espaço conserva a ativação existente em linhas editáveis; não editáveis não anunciam instrução de edição nem ativam captura. Cancelar captura mantém informação equivalente.
- R4: consultas de acessibilidade não alteram configuração nem bindings; mesmas regiões/layout/tabulação. Atualização antes de criar handles é segura, chamadas idempotentes preservam descrição.
- R5: Windows CI verifica o AccessibilityObject real em controles/SettingsView e caminhos reais de atualização; pacote/aplicação e instalação/atualização devem passar antes de merge/release pública com hashes conferidos.

Implementação mínima: KeyBindingRow mantém nome/role existentes, centraliza atualização de metadados no construtor, SetKey e setter Editable. Eventos somente se necessários e com guarda de handle; descrição simples é suficiente, sem exigir objeto customizado ou notificações por frame. Fontes oficiais: [AccessibleDescription](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.control.accessibledescription), [AccessibleObject.Description](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.accessibleobject.description).

Verificação: primeiro medir a falta de descrição em CI com teste significativo; depois aplicar mudança e executar o mesmo teste e suite existente. Build local é permitido, execução nativa local permanece bloqueada pelo App Control. Fixture própria sem ROMs, dados pessoais ou emulador. Exposição programática não equivale a teste manual com Narrator/leitor de tela. Publicação somente ao convergir R1–R5, em sequência posterior à v171.10.8.
