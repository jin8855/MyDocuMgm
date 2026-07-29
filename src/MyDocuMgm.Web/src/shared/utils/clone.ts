import { toRaw } from 'vue'

export function cloneValue<T>(value: T): T {
  return structuredClone(toRaw(value))
}
