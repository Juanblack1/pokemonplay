# Verificação das regras do Firestore — 2026-10-02

O Firebase MCP autenticado leu as regras ativas do banco `(default)` do projeto `true-truck-508712-c8`, Standard, em `southamerica-east1`. O snapshot está em `recovered-source/tests/FirestoreRulesCheck/firestore.rules` e não é uma configuração de implantação. Nenhuma regra nem dado de produção foi alterado.

SHA-256 do snapshot UTF-8 com quebras de linha normalizadas para LF e quebra final: `7705b473f5202db29f4099ca32f5b276758be094337b178b0966269bd6b80023`.

## Evidência de isolamento

O emulador oficial Firestore 1.22.0 executou **79 verificações, 79 passaram, 0 falharam** em 2026-10-02, usando o projeto local `demo-pokemonplay-rules`, dois UIDs simulados (`alice`, `bob`) e visitantes anônimos. A biblioteca `@firebase/rules-unit-testing` usa tokens simulados apenas no emulador; este resultado não representa login de duas contas reais contra produção.

- O proprietário cria, lê, atualiza, lista e exclui seus dados.
- Outro UID não lê, cria, altera, exclui ou lista os dados do proprietário, inclusive manifests e chunks de saves e do banco global.
- Visitantes anônimos não leem ou gravam dados.
- Campos de proprietário falsificados, email verificado e uma claim `admin` não concedem acesso ao UID alheio.
- Enumeração de usuários, consultas de chunks entre usuários e o caminho legado fora de `/users/{uid}` são negados.

O serviço do aplicativo constrói caminhos em `users/{uid}/saves/{id}` e `users/{uid}/saves/{id}/chunks/{chunk}`. As regras ativas exigem `request.auth != null && request.auth.uid == userId` para toda a subárvore. Assim, as operações testadas exercitam os caminhos utilizados pela aplicação.

## Limitação conhecida

Avaliação da skill `firebase-security-rules-auditor`:

```json
{
  "score": 4,
  "summary": "Isolamento por UID verificado; faltam validações do esquema dentro dos dados do próprio usuário.",
  "findings": [{
    "check": "Storage Abuse / Type Safety / Update Bypass",
    "severity": "minor",
    "issue": "As regras aceitam quaisquer campos, tipos e subcoleções dentro da subárvore do próprio UID, sem limites específicos da aplicação. Isso permite corrupção dos próprios dados ou uso indevido de quota, mas não concede acesso a outra conta.",
    "recommendation": "Definir o esquema completo de manifests e chunks e impor tipos, campos e limites consistentes para create e update em um ciclo posterior."
  }]
}
```

## Reproduzir

Com Node.js 22 ou superior e Java 21 no PATH:

```powershell
cd recovered-source/tests/FirestoreRulesCheck
npm ci
npx -y firebase-tools@latest emulators:exec --only firestore --project demo-pokemonplay-rules "npm test"
```

O comando imprime cada verificação e gera `audit-result.json`, ignorado pelo Git. O CI executa a mesma suíte. Para uma nova auditoria de produção, buscar novamente as regras ativas pelo Firebase MCP e comparar com o snapshot: testes de uma cópia local não detectam alterações posteriores no servidor.
