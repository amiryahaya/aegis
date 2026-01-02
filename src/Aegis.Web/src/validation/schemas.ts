import { z } from 'zod'

// ============================================
// AUTH SCHEMAS
// ============================================

export const loginSchema = z.object({
  email: z
    .string()
    .min(1, 'Email is required')
    .email('Please enter a valid email address'),
  password: z
    .string()
    .min(1, 'Password is required')
    .min(8, 'Password must be at least 8 characters'),
  rememberMe: z.boolean().optional().default(false)
})

export type LoginFormData = z.infer<typeof loginSchema>

export const registerSchema = z.object({
  name: z
    .string()
    .min(1, 'Name is required')
    .min(2, 'Name must be at least 2 characters')
    .max(100, 'Name must be less than 100 characters'),
  email: z
    .string()
    .min(1, 'Email is required')
    .email('Please enter a valid email address'),
  password: z
    .string()
    .min(1, 'Password is required')
    .min(8, 'Password must be at least 8 characters')
    .regex(/[A-Z]/, 'Password must contain at least one uppercase letter')
    .regex(/[a-z]/, 'Password must contain at least one lowercase letter')
    .regex(/[0-9]/, 'Password must contain at least one number'),
  confirmPassword: z.string().min(1, 'Please confirm your password')
}).refine((data) => data.password === data.confirmPassword, {
  message: 'Passwords do not match',
  path: ['confirmPassword']
})

export type RegisterFormData = z.infer<typeof registerSchema>

// ============================================
// SESSION SCHEMAS
// ============================================

export const createSessionSchema = z.object({
  title: z
    .string()
    .min(1, 'Session title is required')
    .max(200, 'Title must be less than 200 characters'),
  workspaceId: z
    .string()
    .min(1, 'Please select a workspace')
    .uuid('Invalid workspace ID'),
  template: z.enum(['quick-query', 'research', 'analysis', 'document', 'exploration', 'comparison']).optional(),
  settings: z.object({
    maxTurns: z.number().min(1).max(100).optional(),
    contextWindow: z.number().min(1).max(50).optional(),
    streamResponses: z.boolean().optional()
  }).optional()
})

export type CreateSessionFormData = z.infer<typeof createSessionSchema>

export const sessionSettingsSchema = z.object({
  title: z
    .string()
    .min(1, 'Session title is required')
    .max(200, 'Title must be less than 200 characters'),
  maxTurns: z.number().min(1, 'Minimum is 1').max(100, 'Maximum is 100'),
  contextWindow: z.number().min(1, 'Minimum is 1').max(50, 'Maximum is 50'),
  streamResponses: z.boolean()
})

export type SessionSettingsFormData = z.infer<typeof sessionSettingsSchema>

// ============================================
// WORKSPACE SCHEMAS
// ============================================

export const createWorkspaceSchema = z.object({
  name: z
    .string()
    .min(1, 'Workspace name is required')
    .min(3, 'Name must be at least 3 characters')
    .max(100, 'Name must be less than 100 characters'),
  description: z
    .string()
    .max(500, 'Description must be less than 500 characters')
    .optional(),
  visibility: z.enum(['private', 'team', 'public']).default('private'),
  settings: z.object({
    allowComments: z.boolean().optional(),
    allowSharing: z.boolean().optional(),
    requireApproval: z.boolean().optional()
  }).optional()
})

export type CreateWorkspaceFormData = z.infer<typeof createWorkspaceSchema>

export const workspaceSettingsSchema = z.object({
  name: z
    .string()
    .min(1, 'Workspace name is required')
    .min(3, 'Name must be at least 3 characters')
    .max(100, 'Name must be less than 100 characters'),
  description: z
    .string()
    .max(500, 'Description must be less than 500 characters')
    .optional(),
  visibility: z.enum(['private', 'team', 'public']),
  allowComments: z.boolean(),
  allowSharing: z.boolean(),
  requireApproval: z.boolean()
})

export type WorkspaceSettingsFormData = z.infer<typeof workspaceSettingsSchema>

// ============================================
// DATA SOURCE SCHEMAS
// ============================================

export const addDataSourceSchema = z.object({
  name: z
    .string()
    .min(1, 'Data source name is required')
    .max(100, 'Name must be less than 100 characters'),
  type: z.enum(['file', 'url', 'database', 'api', 's3', 'sharepoint', 'confluence']),
  configuration: z.record(z.string(), z.unknown()).optional(),
  syncSchedule: z.enum(['manual', 'hourly', 'daily', 'weekly']).optional()
})

export type AddDataSourceFormData = z.infer<typeof addDataSourceSchema>

// ============================================
// PROFILE SCHEMAS
// ============================================

export const profileSchema = z.object({
  name: z
    .string()
    .min(1, 'Name is required')
    .min(2, 'Name must be at least 2 characters')
    .max(100, 'Name must be less than 100 characters'),
  email: z
    .string()
    .min(1, 'Email is required')
    .email('Please enter a valid email address'),
  bio: z
    .string()
    .max(500, 'Bio must be less than 500 characters')
    .optional(),
  timezone: z.string().optional(),
  language: z.string().optional()
})

export type ProfileFormData = z.infer<typeof profileSchema>

export const changePasswordSchema = z.object({
  currentPassword: z
    .string()
    .min(1, 'Current password is required'),
  newPassword: z
    .string()
    .min(1, 'New password is required')
    .min(8, 'Password must be at least 8 characters')
    .regex(/[A-Z]/, 'Password must contain at least one uppercase letter')
    .regex(/[a-z]/, 'Password must contain at least one lowercase letter')
    .regex(/[0-9]/, 'Password must contain at least one number'),
  confirmPassword: z.string().min(1, 'Please confirm your password')
}).refine((data) => data.newPassword === data.confirmPassword, {
  message: 'Passwords do not match',
  path: ['confirmPassword']
})

export type ChangePasswordFormData = z.infer<typeof changePasswordSchema>

// ============================================
// API KEY SCHEMAS
// ============================================

export const createApiKeySchema = z.object({
  name: z
    .string()
    .min(1, 'API key name is required')
    .max(100, 'Name must be less than 100 characters'),
  scopes: z.array(z.string()).min(1, 'Please select at least one scope'),
  expiresAt: z.date().optional()
})

export type CreateApiKeyFormData = z.infer<typeof createApiKeySchema>

// ============================================
// COMMENT SCHEMAS
// ============================================

export const commentSchema = z.object({
  content: z
    .string()
    .min(1, 'Comment cannot be empty')
    .max(5000, 'Comment must be less than 5000 characters')
})

export type CommentFormData = z.infer<typeof commentSchema>

// ============================================
// QUERY SCHEMAS
// ============================================

export const querySchema = z.object({
  query: z
    .string()
    .min(1, 'Please enter a question')
    .max(10000, 'Query must be less than 10000 characters')
})

export type QueryFormData = z.infer<typeof querySchema>

// ============================================
// SEARCH SCHEMAS
// ============================================

export const searchSchema = z.object({
  query: z
    .string()
    .min(1, 'Please enter a search term')
    .max(500, 'Search query must be less than 500 characters'),
  type: z.enum(['all', 'session', 'document', 'workspace', 'message']).optional(),
  dateRange: z.enum(['today', 'week', 'month', 'year', 'all']).optional(),
  workspaceId: z.string().uuid().optional()
})

export type SearchFormData = z.infer<typeof searchSchema>

// ============================================
// WEBHOOK SCHEMAS
// ============================================

export const webhookSchema = z.object({
  url: z
    .string()
    .min(1, 'Webhook URL is required')
    .url('Please enter a valid URL'),
  events: z.array(z.string()).min(1, 'Please select at least one event'),
  secret: z
    .string()
    .min(16, 'Secret must be at least 16 characters')
    .optional(),
  active: z.boolean().default(true)
})

export type WebhookFormData = z.infer<typeof webhookSchema>

// ============================================
// SHARE SCHEMAS
// ============================================

export const shareSchema = z.object({
  email: z
    .string()
    .min(1, 'Email is required')
    .email('Please enter a valid email address'),
  role: z.enum(['viewer', 'commenter', 'editor', 'admin']),
  message: z.string().max(500, 'Message must be less than 500 characters').optional()
})

export type ShareFormData = z.infer<typeof shareSchema>

export const shareableLinkSchema = z.object({
  expiresInDays: z.number().min(1).max(365).optional(),
  maxUses: z.number().min(1).max(1000).optional(),
  password: z.string().min(6, 'Password must be at least 6 characters').optional(),
  allowDownload: z.boolean().default(false)
})

export type ShareableLinkFormData = z.infer<typeof shareableLinkSchema>
