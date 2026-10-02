import { readFileSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { initializeTestEnvironment, assertSucceeds, assertFails } from '@firebase/rules-unit-testing';
import { doc, collection, collectionGroup, getDoc, getDocs, setDoc, updateDoc, deleteDoc, setLogLevel } from 'firebase/firestore';

const rules = readFileSync('firestore.rules', 'utf8').replace(/\r\n/g, '\n');
setLogLevel('silent');
const env = await initializeTestEnvironment({ projectId: 'demo-pokemonplay-rules', firestore: { host: '127.0.0.1', port: 8189, rules } });
const cases = [];
async function check(label, operation, allowed) {
  await (allowed ? assertSucceeds(operation) : assertFails(operation));
  cases.push({ label, expected: allowed ? 'ALLOW' : 'DENY', passed: true });
  console.log('PASS ' + label);
}
try {
  for (const uid of ['alice', 'bob']) {
    const own = env.authenticatedContext(uid).firestore();
    const other = env.authenticatedContext(uid === 'alice' ? 'bob' : 'alice').firestore();
    const anon = env.unauthenticatedContext().firestore();
    for (const suffix of ['saves/pokemon-fire-red', 'saves/pokemon-fire-red/chunks/00000', 'saves/global-pokemon-bank', 'saves/global-pokemon-bank/chunks/00000']) {
      const path = `users/${uid}/${suffix}`;
      await check(`${uid} create own ${suffix}`, setDoc(doc(own, path), { data: 'fixture', ownerId: uid }), true);
      await check(`${uid} read own ${suffix}`, getDoc(doc(own, path)), true);
      await check(`${uid} update own ${suffix}`, updateDoc(doc(own, path), { data: 'updated' }), true);
      await check(`other read ${path}`, getDoc(doc(other, path)), false);
      await check(`other update ${path}`, updateDoc(doc(other, path), { data: 'stolen' }), false);
      await check(`other delete ${path}`, deleteDoc(doc(other, path)), false);
      await check(`anonymous read ${path}`, getDoc(doc(anon, path)), false);
      await check(`anonymous write ${path}`, setDoc(doc(anon, path), { data: 'anonymous' }), false);
    }
    await check(`${uid} list own saves`, getDocs(collection(own, `users/${uid}/saves`)), true);
    await check(`other list ${uid} saves`, getDocs(collection(other, `users/${uid}/saves`)), false);
    await check(`other create under ${uid} with forged owner field`, setDoc(doc(other, `users/${uid}/saves/forged`), { ownerId: uid }), false);
    await check(`${uid} delete own chunk`, deleteDoc(doc(own, `users/${uid}/saves/pokemon-fire-red/chunks/00000`)), true);
  }
  const alice = env.authenticatedContext('alice').firestore();
  const forgedAdmin = env.authenticatedContext('mallory', { admin: true, email_verified: true, email: 'alice@example.test' }).firestore();
  await check('forged admin claims cannot read alice', getDoc(doc(forgedAdmin, 'users/alice/saves/pokemon-fire-red')), false);
  await check('users enumeration denied', getDocs(collection(alice, 'users')), false);
  await check('collection group across users denied', getDocs(collectionGroup(alice, 'chunks')), false);
  await check('authenticated legacy root read denied', getDoc(doc(alice, 'saves/legacy')), false);
  await check('authenticated legacy root write denied', setDoc(doc(alice, 'saves/legacy'), { data: 'fixture' }), false);
  await check('anonymous create new user denied', setDoc(doc(env.unauthenticatedContext().firestore(), 'users/new-user'), { role: 'admin' }), false);
  await check('owner arbitrary schema accepted (validation gap)', setDoc(doc(alice, 'users/alice/saves/arbitrary-schema'), { data: 123, unexpected: true }), true);
  const result = { projectId: 'demo-pokemonplay-rules', mode: 'local emulator, live rules snapshot', timestamp: new Date().toISOString(), rulesSha256: createHash('sha256').update(rules).digest('hex'), passed: cases.length, failed: 0, cases, findings: [{ severity: 'minor', issue: 'Rules enforce UID ownership but impose no application schema or size limits inside the owner subtree.' }] };
  writeFileSync('audit-result.json', JSON.stringify(result, null, 2) + '\n');
  console.log(`RESULT ${cases.length} passed, 0 failed`);
} finally {
  await env.cleanup();
}
