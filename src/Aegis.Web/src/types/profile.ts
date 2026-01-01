// User Profile Types

export interface UserProfile {
  id: string
  email: string
  name: string
  displayName?: string
  avatar?: string
  bio?: string
  role: string
  teamId?: string
  teamName?: string
  department?: string
  location?: string
  timezone: string
  language: string
  isActive: boolean
  createdAt: string
  lastLoginAt?: string
  emailVerified: boolean
}

export interface UserStats {
  totalSessions: number
  totalQueries: number
  totalDocuments: number
  totalWorkspaces: number
  queriesThisWeek: number
  queriesThisMonth: number
  avgResponseTime: number
  topWorkspaces: WorkspaceUsage[]
  activityByDay: DayActivity[]
}

export interface WorkspaceUsage {
  workspaceId: string
  workspaceName: string
  queryCount: number
  lastUsedAt: string
}

export interface DayActivity {
  date: string
  queries: number
  sessions: number
}

export interface UpdateProfileRequest {
  name?: string
  displayName?: string
  bio?: string
  department?: string
  location?: string
  timezone?: string
  language?: string
}

export interface ChangePasswordRequest {
  currentPassword: string
  newPassword: string
  confirmPassword: string
}

export interface ApiKeyInfo {
  id: string
  name: string
  prefix: string
  createdAt: string
  lastUsedAt?: string
  expiresAt?: string
  scopes: string[]
}

// Re-export API key request/response types from admin for profile use
export interface ProfileApiKeyRequest {
  name: string
  scopes: string[]
  expiresInDays?: number
}

export interface ProfileApiKeyResponse {
  id: string
  key: string
  prefix: string
  expiresAt?: string
}

// Help Center Types

export interface HelpArticle {
  id: string
  title: string
  slug: string
  category: HelpCategory
  excerpt: string
  content: string
  tags: string[]
  updatedAt: string
}

export type HelpCategory =
  | 'getting-started'
  | 'chat'
  | 'workspaces'
  | 'documents'
  | 'search'
  | 'settings'
  | 'api'
  | 'troubleshooting'

export interface KeyboardShortcut {
  key: string
  modifiers: ShortcutModifier[]
  description: string
  category: ShortcutCategory
  action: string
}

export type ShortcutModifier = 'ctrl' | 'alt' | 'shift' | 'meta'
export type ShortcutCategory = 'navigation' | 'chat' | 'search' | 'general'

export interface FAQ {
  id: string
  question: string
  answer: string
  category: string
}
