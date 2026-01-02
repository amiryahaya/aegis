import { toTypedSchema } from '@vee-validate/zod'
import type { ZodSchema } from 'zod'

// Re-export all schemas
export * from './schemas'

// Helper to convert Zod schema to VeeValidate typed schema
export function toFormSchema<T extends ZodSchema>(schema: T) {
  return toTypedSchema(schema)
}
