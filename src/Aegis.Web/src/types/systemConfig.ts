// System Configuration Types

// =============================================================================
// Notification Preferences
// =============================================================================

export interface UserNotificationPreferences {
  userId: string
  email: EmailNotificationSettings
  inApp: InAppNotificationSettings
  push: PushNotificationSettings
  digest: DigestSettings
  quietHours: QuietHoursSettings
  updatedAt: string
}

export interface EmailNotificationSettings {
  enabled: boolean
  queries: boolean
  sessions: boolean
  documents: boolean
  mentions: boolean
  teamUpdates: boolean
  securityAlerts: boolean
  weeklyDigest: boolean
  marketingEmails: boolean
}

export interface InAppNotificationSettings {
  enabled: boolean
  queries: boolean
  sessions: boolean
  documents: boolean
  mentions: boolean
  teamUpdates: boolean
  securityAlerts: boolean
  systemAnnouncements: boolean
  sound: boolean
  desktop: boolean
}

export interface PushNotificationSettings {
  enabled: boolean
  queries: boolean
  mentions: boolean
  securityAlerts: boolean
  urgentOnly: boolean
}

export interface DigestSettings {
  enabled: boolean
  frequency: DigestFrequency
  dayOfWeek: number // 0-6, Sunday = 0
  timeOfDay: string // HH:mm format
  includeAnalytics: boolean
  includeTeamActivity: boolean
}

export type DigestFrequency = 'daily' | 'weekly' | 'monthly' | 'never'

export interface QuietHoursSettings {
  enabled: boolean
  startTime: string // HH:mm format
  endTime: string // HH:mm format
  timezone: string
  allowUrgent: boolean
  weekendsOnly: boolean
}

// =============================================================================
// Email Templates
// =============================================================================

export interface EmailTemplate {
  id: string
  name: string
  slug: string
  subject: string
  htmlBody: string
  textBody: string
  category: EmailTemplateCategory
  variables: TemplateVariable[]
  isActive: boolean
  isDefault: boolean
  createdAt: string
  updatedAt: string
  createdBy: string
  lastEditedBy: string
}

export type EmailTemplateCategory =
  | 'authentication'
  | 'notifications'
  | 'alerts'
  | 'reports'
  | 'invitations'
  | 'system'

export interface TemplateVariable {
  name: string
  description: string
  example: string
  required: boolean
}

export interface CreateEmailTemplateRequest {
  name: string
  slug: string
  subject: string
  htmlBody: string
  textBody?: string
  category: EmailTemplateCategory
  variables?: TemplateVariable[]
}

export interface UpdateEmailTemplateRequest {
  name?: string
  subject?: string
  htmlBody?: string
  textBody?: string
  category?: EmailTemplateCategory
  variables?: TemplateVariable[]
  isActive?: boolean
}

// =============================================================================
// System Configuration
// =============================================================================

export interface SystemConfiguration {
  general: GeneralSettings
  security: SecuritySettings
  authentication: AuthenticationSettings
  storage: StorageSettings
  llm: LLMSettings
  notifications: SystemNotificationSettings
  limits: SystemLimits
  featureFlags: FeatureFlag[]
  maintenance: MaintenanceSettings
}

export interface GeneralSettings {
  siteName: string
  siteUrl: string
  adminEmail: string
  supportEmail: string
  defaultLanguage: string
  defaultTimezone: string
  dateFormat: string
  timeFormat: string
  logo: string | null
  favicon: string | null
  theme: 'light' | 'dark' | 'system'
}

export interface SecuritySettings {
  passwordMinLength: number
  passwordRequireUppercase: boolean
  passwordRequireLowercase: boolean
  passwordRequireNumbers: boolean
  passwordRequireSpecial: boolean
  passwordExpiryDays: number
  maxLoginAttempts: number
  lockoutDurationMinutes: number
  sessionTimeoutMinutes: number
  mfaRequired: boolean
  mfaGracePeriodDays: number
  ipWhitelist: string[]
  ipBlacklist: string[]
  allowedOrigins: string[]
}

export interface AuthenticationSettings {
  localAuthEnabled: boolean
  ssoEnabled: boolean
  ssoProviders: SSOProvider[]
  selfRegistrationEnabled: boolean
  emailVerificationRequired: boolean
  inviteOnlyMode: boolean
  defaultRole: string
  apiKeyAuthEnabled: boolean
  jwtExpiryMinutes: number
  refreshTokenExpiryDays: number
}

export interface SSOProvider {
  id: string
  name: string
  type: SSOProviderType
  clientId: string
  clientSecret?: string // Hidden in responses
  issuerUrl: string
  scopes: string[]
  isActive: boolean
  autoProvision: boolean
  defaultRole: string
}

export type SSOProviderType = 'oidc' | 'saml' | 'azure-ad' | 'okta' | 'auth0' | 'google' | 'github'

export interface StorageSettings {
  provider: StorageProvider
  maxFileSizeMb: number
  allowedFileTypes: string[]
  maxStoragePerUserGb: number
  maxStoragePerWorkspaceGb: number
  retentionDays: number
  compressionEnabled: boolean
  encryptionEnabled: boolean
}

export type StorageProvider = 'local' | 's3' | 'azure-blob' | 'gcs' | 'minio'

export interface LLMSettings {
  provider: LLMProvider
  model: string
  apiKey?: string // Hidden in responses
  apiEndpoint: string
  maxTokens: number
  temperature: number
  topP: number
  frequencyPenalty: number
  presencePenalty: number
  timeout: number
  retryAttempts: number
  streamingEnabled: boolean
  fallbackProvider?: LLMProvider
  fallbackModel?: string
}

export type LLMProvider = 'openai' | 'azure-openai' | 'anthropic' | 'ollama' | 'huggingface' | 'cohere'

export interface SystemNotificationSettings {
  smtpEnabled: boolean
  smtpHost: string
  smtpPort: number
  smtpUsername: string
  smtpPassword?: string // Hidden in responses
  smtpSecure: boolean
  fromEmail: string
  fromName: string
  webhooksEnabled: boolean
  slackEnabled: boolean
  slackWebhookUrl?: string
  teamsEnabled: boolean
  teamsWebhookUrl?: string
}

export interface SystemLimits {
  maxUsersPerTeam: number
  maxTeamsPerOrg: number
  maxWorkspacesPerTeam: number
  maxDocumentsPerWorkspace: number
  maxQueriesPerMinute: number
  maxQueriesPerDay: number
  maxConcurrentSessions: number
  maxApiKeysPerUser: number
  maxWebhooksPerWorkspace: number
  maxFileUploadsPerDay: number
}

export interface FeatureFlag {
  id: string
  name: string
  key: string
  description: string
  enabled: boolean
  enabledForRoles: string[]
  enabledForUsers: string[]
  enabledForTeams: string[]
  rolloutPercentage: number
  createdAt: string
  updatedAt: string
}

export interface CreateFeatureFlagRequest {
  name: string
  key: string
  description?: string
  enabled?: boolean
  enabledForRoles?: string[]
  rolloutPercentage?: number
}

export interface UpdateFeatureFlagRequest {
  name?: string
  description?: string
  enabled?: boolean
  enabledForRoles?: string[]
  enabledForUsers?: string[]
  enabledForTeams?: string[]
  rolloutPercentage?: number
}

export interface MaintenanceSettings {
  enabled: boolean
  message: string
  startTime?: string
  endTime?: string
  allowAdminAccess: boolean
  scheduledMaintenances: ScheduledMaintenance[]
}

export interface ScheduledMaintenance {
  id: string
  title: string
  description: string
  startTime: string
  endTime: string
  affectedServices: string[]
  notifyUsers: boolean
  createdAt: string
}

// =============================================================================
// Configuration History / Audit
// =============================================================================

export interface ConfigurationChange {
  id: string
  section: string
  field: string
  oldValue: unknown
  newValue: unknown
  changedBy: string
  changedByName: string
  changedAt: string
  reason?: string
}

// =============================================================================
// Utility Types
// =============================================================================

export type ConfigSection =
  | 'general'
  | 'security'
  | 'authentication'
  | 'storage'
  | 'llm'
  | 'notifications'
  | 'limits'
  | 'featureFlags'
  | 'maintenance'

export const CONFIG_SECTION_LABELS: Record<ConfigSection, string> = {
  general: 'General',
  security: 'Security',
  authentication: 'Authentication',
  storage: 'Storage',
  llm: 'LLM & AI',
  notifications: 'Notifications',
  limits: 'System Limits',
  featureFlags: 'Feature Flags',
  maintenance: 'Maintenance'
}

export const EMAIL_TEMPLATE_CATEGORIES: Record<EmailTemplateCategory, string> = {
  authentication: 'Authentication',
  notifications: 'Notifications',
  alerts: 'Alerts',
  reports: 'Reports',
  invitations: 'Invitations',
  system: 'System'
}

export const SSO_PROVIDER_LABELS: Record<SSOProviderType, string> = {
  'oidc': 'OpenID Connect',
  'saml': 'SAML 2.0',
  'azure-ad': 'Azure AD',
  'okta': 'Okta',
  'auth0': 'Auth0',
  'google': 'Google',
  'github': 'GitHub'
}

export const STORAGE_PROVIDER_LABELS: Record<StorageProvider, string> = {
  'local': 'Local Storage',
  's3': 'Amazon S3',
  'azure-blob': 'Azure Blob Storage',
  'gcs': 'Google Cloud Storage',
  'minio': 'MinIO'
}

export const LLM_PROVIDER_LABELS: Record<LLMProvider, string> = {
  'openai': 'OpenAI',
  'azure-openai': 'Azure OpenAI',
  'anthropic': 'Anthropic',
  'ollama': 'Ollama',
  'huggingface': 'HuggingFace',
  'cohere': 'Cohere'
}

// Utility function to get default notification preferences
export function getDefaultUserNotificationPreferences(userId: string): UserNotificationPreferences {
  return {
    userId,
    email: {
      enabled: true,
      queries: false,
      sessions: false,
      documents: true,
      mentions: true,
      teamUpdates: true,
      securityAlerts: true,
      weeklyDigest: true,
      marketingEmails: false
    },
    inApp: {
      enabled: true,
      queries: true,
      sessions: true,
      documents: true,
      mentions: true,
      teamUpdates: true,
      securityAlerts: true,
      systemAnnouncements: true,
      sound: true,
      desktop: false
    },
    push: {
      enabled: false,
      queries: false,
      mentions: true,
      securityAlerts: true,
      urgentOnly: true
    },
    digest: {
      enabled: true,
      frequency: 'weekly',
      dayOfWeek: 1, // Monday
      timeOfDay: '09:00',
      includeAnalytics: true,
      includeTeamActivity: true
    },
    quietHours: {
      enabled: false,
      startTime: '22:00',
      endTime: '08:00',
      timezone: 'UTC',
      allowUrgent: true,
      weekendsOnly: false
    },
    updatedAt: new Date().toISOString()
  }
}
