export function mergeVerificationResults(previous, incoming, receivedAt) {
  return incoming.map((result) => {
    if (!result.error) return { ...result, receivedAt, stale: false }
    const lastValid = previous.find((item) => item.id === result.id && Object.hasOwn(item, 'data'))
    return lastValid ? { ...lastValid, ...result, stale: true } : { ...result, stale: false }
  })
}
