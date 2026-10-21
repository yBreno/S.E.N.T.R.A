import test from 'node:test'
import assert from 'node:assert/strict'
import { mergeVerificationResults } from '../src/services/verificationState.js'

test('sucesso recebe horário de resposta válida e erro inicial não cria dados', () => {
  const error = new Error('Falhou')
  const results = mergeVerificationResults([], [{ id: 'agents', data: 0, items: [] }, { id: 'equipments', error }], 'time-1')
  assert.equal(results[0].receivedAt, 'time-1')
  assert.equal(results[0].stale, false)
  assert.equal(Object.hasOwn(results[1], 'data'), false)
})

test('falha parcial preserva a última resposta válida e seu horário por endpoint', () => {
  const previous = [{ id: 'equipments', data: 1, items: [{ id: 'old' }], receivedAt: 'old-time' }, { id: 'agents', data: 0, items: [], receivedAt: 'old-time' }]
  const current = mergeVerificationResults(previous, [{ id: 'equipments', error: new Error('Falha') }, { id: 'agents', data: 2, items: [{}, {}] }], 'new-time')
  assert.equal(current[0].items[0].id, 'old')
  assert.equal(current[0].stale, true)
  assert.equal(current[0].receivedAt, 'old-time')
  assert.equal(current[1].data, 2)
  assert.equal(current[1].stale, false)
  assert.equal(current[1].receivedAt, 'new-time')
  assert.equal(previous[0].stale, undefined)
})

test('falhas repetidas mantêm a resposta válida inclusive quando era vazia', () => {
  const first = mergeVerificationResults([], [{ id: 'agents', data: 0, items: [] }], 'time-1')
  const second = mergeVerificationResults(first, [{ id: 'agents', error: new Error('Falha 1') }], 'time-2')
  const third = mergeVerificationResults(second, [{ id: 'agents', error: new Error('Falha 2') }], 'time-3')
  assert.equal(third[0].data, 0)
  assert.equal(third[0].receivedAt, 'time-1')
  assert.equal(third[0].stale, true)
})

test('uma resposta vazia bem-sucedida substitui registros antigos e limpa erro/stale', () => {
  const result = mergeVerificationResults([{ id: 'equipments', data: 1, items: [{}], error: new Error('Anterior'), stale: true }],
    [{ id: 'equipments', data: 0, items: [] }], 'now')
  assert.deepEqual(result[0], { id: 'equipments', data: 0, items: [], receivedAt: 'now', stale: false })
})
