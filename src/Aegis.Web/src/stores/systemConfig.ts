import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type {
  UserNotificationPreferences,
  EmailTemplate,
  SystemConfiguration,
  FeatureFlag,
  ConfigurationChange,
  CreateEmailTemplateRequest,
  UpdateEmailTemplateRequest,
  CreateFeatureFlagRequest,
  UpdateFeatureFlagRequest,
  EmailTemplateCategory,
  ConfigSection
} from '@/types'
import { getDefaultUserNotificationPreferences } from '@/types/systemConfig'

export const useSystemConfigStore = defineStore('systemConfig', () => {
  // State
  const notificationPreferences = ref<UserNotificationPreferences | null>(null)
  const emailTemplates = ref<EmailTemplate[]>([])
  const systemConfig = ref<SystemConfiguration | null>(null)
  const featureFlags = ref<FeatureFlag[]>([])
  const configHistory = ref<ConfigurationChange[]>([])
  const isLoading = ref(false)
  const isSaving = ref(false)
  const error = ref<string | null>(null)
  const selectedSection = ref<ConfigSection>('general')

  // Computed
  const activeFeatureFlags = computed(() =>
    featureFlags.value.filter(f => f.enabled)
  )

  const templatesByCategory = computed(() => {
    const grouped: Record<EmailTemplateCategory, EmailTemplate[]> = {
      authentication: [],
      notifications: [],
      alerts: [],
      reports: [],
      invitations: [],
      system: []
    }
    emailTemplates.value.forEach(t => {
      grouped[t.category].push(t)
    })
    return grouped
  })

  const isMaintenanceMode = computed(() =>
    systemConfig.value?.maintenance.enabled ?? false
  )

  // Notification Preferences Actions
  async function fetchNotificationPreferences(userId: string): Promise<void> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      // Mock: return default preferences
      notificationPreferences.value = getDefaultUserNotificationPreferences(userId)
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch preferences'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function updateNotificationPreferences(
    preferences: Partial<UserNotificationPreferences>
  ): Promise<void> {
    isSaving.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      if (notificationPreferences.value) {
        notificationPreferences.value = {
          ...notificationPreferences.value,
          ...preferences,
          updatedAt: new Date().toISOString()
        }
      }
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to update preferences'
      throw err
    } finally {
      isSaving.value = false
    }
  }

  // Email Template Actions
  async function fetchEmailTemplates(): Promise<void> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 500))
      emailTemplates.value = generateMockEmailTemplates()
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch templates'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function fetchEmailTemplateById(id: string): Promise<EmailTemplate | null> {
    try {
      await new Promise(resolve => setTimeout(resolve, 200))
      return emailTemplates.value.find(t => t.id === id) || null
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch template'
      throw err
    }
  }

  async function createEmailTemplate(request: CreateEmailTemplateRequest): Promise<EmailTemplate> {
    isSaving.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      const template: EmailTemplate = {
        id: crypto.randomUUID(),
        ...request,
        textBody: request.textBody || '',
        variables: request.variables || [],
        isActive: true,
        isDefault: false,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        createdBy: 'current-user-id',
        lastEditedBy: 'current-user-id'
      }
      emailTemplates.value.unshift(template)
      return template
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to create template'
      throw err
    } finally {
      isSaving.value = false
    }
  }

  async function updateEmailTemplate(
    id: string,
    request: UpdateEmailTemplateRequest
  ): Promise<EmailTemplate | null> {
    isSaving.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      const index = emailTemplates.value.findIndex(t => t.id === id)
      if (index !== -1) {
        emailTemplates.value[index] = {
          ...emailTemplates.value[index],
          ...request,
          updatedAt: new Date().toISOString(),
          lastEditedBy: 'current-user-id'
        }
        return emailTemplates.value[index]
      }
      return null
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to update template'
      throw err
    } finally {
      isSaving.value = false
    }
  }

  async function deleteEmailTemplate(id: string): Promise<boolean> {
    isSaving.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 200))
      const index = emailTemplates.value.findIndex(t => t.id === id)
      if (index !== -1) {
        emailTemplates.value.splice(index, 1)
        return true
      }
      return false
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to delete template'
      throw err
    } finally {
      isSaving.value = false
    }
  }

  async function duplicateEmailTemplate(id: string): Promise<EmailTemplate | null> {
    const template = emailTemplates.value.find(t => t.id === id)
    if (template) {
      return createEmailTemplate({
        name: `${template.name} (Copy)`,
        slug: `${template.slug}-copy-${Date.now()}`,
        subject: template.subject,
        htmlBody: template.htmlBody,
        textBody: template.textBody,
        category: template.category,
        variables: template.variables
      })
    }
    return null
  }

  // System Configuration Actions
  async function fetchSystemConfiguration(): Promise<void> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 500))
      systemConfig.value = generateMockSystemConfiguration()
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch configuration'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function updateSystemConfiguration(
    section: ConfigSection,
    updates: Record<string, unknown>,
    reason?: string
  ): Promise<void> {
    isSaving.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      if (systemConfig.value) {
        const sectionData = systemConfig.value[section as keyof SystemConfiguration]
        if (typeof sectionData === 'object' && sectionData !== null) {
          // Record changes for audit
          Object.entries(updates).forEach(([key, newValue]) => {
            const oldValue = (sectionData as Record<string, unknown>)[key]
            if (oldValue !== newValue) {
              configHistory.value.unshift({
                id: crypto.randomUUID(),
                section,
                field: key,
                oldValue,
                newValue,
                changedBy: 'current-user-id',
                changedByName: 'Current User',
                changedAt: new Date().toISOString(),
                reason
              })
            }
          })

          // Update configuration
          Object.assign(sectionData, updates)
        }
      }
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to update configuration'
      throw err
    } finally {
      isSaving.value = false
    }
  }

  // Feature Flags Actions
  async function fetchFeatureFlags(): Promise<void> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 400))
      featureFlags.value = generateMockFeatureFlags()
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch feature flags'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function createFeatureFlag(request: CreateFeatureFlagRequest): Promise<FeatureFlag> {
    isSaving.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      const flag: FeatureFlag = {
        id: crypto.randomUUID(),
        name: request.name,
        key: request.key,
        description: request.description || '',
        enabled: request.enabled ?? false,
        enabledForRoles: request.enabledForRoles || [],
        enabledForUsers: [],
        enabledForTeams: [],
        rolloutPercentage: request.rolloutPercentage ?? 0,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString()
      }
      featureFlags.value.unshift(flag)
      return flag
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to create feature flag'
      throw err
    } finally {
      isSaving.value = false
    }
  }

  async function updateFeatureFlag(
    id: string,
    request: UpdateFeatureFlagRequest
  ): Promise<FeatureFlag | null> {
    isSaving.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 200))
      const index = featureFlags.value.findIndex(f => f.id === id)
      if (index !== -1) {
        featureFlags.value[index] = {
          ...featureFlags.value[index],
          ...request,
          updatedAt: new Date().toISOString()
        }
        return featureFlags.value[index]
      }
      return null
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to update feature flag'
      throw err
    } finally {
      isSaving.value = false
    }
  }

  async function toggleFeatureFlag(id: string): Promise<boolean> {
    const flag = featureFlags.value.find(f => f.id === id)
    if (flag) {
      await updateFeatureFlag(id, { enabled: !flag.enabled })
      return true
    }
    return false
  }

  async function deleteFeatureFlag(id: string): Promise<boolean> {
    isSaving.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 200))
      const index = featureFlags.value.findIndex(f => f.id === id)
      if (index !== -1) {
        featureFlags.value.splice(index, 1)
        return true
      }
      return false
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to delete feature flag'
      throw err
    } finally {
      isSaving.value = false
    }
  }

  // Configuration History
  async function fetchConfigHistory(section?: ConfigSection): Promise<void> {
    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      // Mock history is already populated from updates
      if (section) {
        configHistory.value = configHistory.value.filter(c => c.section === section)
      }
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch history'
      throw err
    }
  }

  // Utility Actions
  function setSelectedSection(section: ConfigSection): void {
    selectedSection.value = section
  }

  function clearError(): void {
    error.value = null
  }

  return {
    // State
    notificationPreferences,
    emailTemplates,
    systemConfig,
    featureFlags,
    configHistory,
    isLoading,
    isSaving,
    error,
    selectedSection,
    // Computed
    activeFeatureFlags,
    templatesByCategory,
    isMaintenanceMode,
    // Notification Preferences Actions
    fetchNotificationPreferences,
    updateNotificationPreferences,
    // Email Template Actions
    fetchEmailTemplates,
    fetchEmailTemplateById,
    createEmailTemplate,
    updateEmailTemplate,
    deleteEmailTemplate,
    duplicateEmailTemplate,
    // System Configuration Actions
    fetchSystemConfiguration,
    updateSystemConfiguration,
    // Feature Flags Actions
    fetchFeatureFlags,
    createFeatureFlag,
    updateFeatureFlag,
    toggleFeatureFlag,
    deleteFeatureFlag,
    // Configuration History
    fetchConfigHistory,
    // Utility Actions
    setSelectedSection,
    clearError
  }
})

// =============================================================================
// Mock Data Generators
// =============================================================================

function generateMockEmailTemplates(): EmailTemplate[] {
  return [
    {
      id: '1',
      name: 'Welcome Email',
      slug: 'welcome-email',
      subject: 'Welcome to {{siteName}}!',
      htmlBody: '<h1>Welcome, {{userName}}!</h1><p>Thank you for joining {{siteName}}.</p>',
      textBody: 'Welcome, {{userName}}! Thank you for joining {{siteName}}.',
      category: 'authentication',
      variables: [
        { name: 'userName', description: 'User\'s display name', example: 'John Doe', required: true },
        { name: 'siteName', description: 'Application name', example: 'AEGIS', required: true }
      ],
      isActive: true,
      isDefault: true,
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-06-15T10:30:00Z',
      createdBy: 'system',
      lastEditedBy: 'admin'
    },
    {
      id: '2',
      name: 'Password Reset',
      slug: 'password-reset',
      subject: 'Reset Your Password',
      htmlBody: '<h1>Password Reset</h1><p>Click <a href="{{resetLink}}">here</a> to reset your password.</p>',
      textBody: 'Click the following link to reset your password: {{resetLink}}',
      category: 'authentication',
      variables: [
        { name: 'resetLink', description: 'Password reset URL', example: 'https://app.com/reset/abc123', required: true },
        { name: 'expiryHours', description: 'Link expiry time in hours', example: '24', required: false }
      ],
      isActive: true,
      isDefault: true,
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-06-15T10:30:00Z',
      createdBy: 'system',
      lastEditedBy: 'admin'
    },
    {
      id: '3',
      name: 'Team Invitation',
      slug: 'team-invitation',
      subject: 'You\'ve been invited to join {{teamName}}',
      htmlBody: '<h1>Team Invitation</h1><p>{{inviterName}} has invited you to join {{teamName}}.</p>',
      textBody: '{{inviterName}} has invited you to join {{teamName}}.',
      category: 'invitations',
      variables: [
        { name: 'inviterName', description: 'Name of person sending invite', example: 'Jane Smith', required: true },
        { name: 'teamName', description: 'Team name', example: 'Engineering', required: true },
        { name: 'inviteLink', description: 'Invitation acceptance URL', example: 'https://app.com/invite/xyz', required: true }
      ],
      isActive: true,
      isDefault: true,
      createdAt: '2024-01-15T00:00:00Z',
      updatedAt: '2024-06-20T14:00:00Z',
      createdBy: 'system',
      lastEditedBy: 'admin'
    },
    {
      id: '4',
      name: 'Weekly Digest',
      slug: 'weekly-digest',
      subject: 'Your Weekly Summary - {{dateRange}}',
      htmlBody: '<h1>Weekly Summary</h1><p>Here\'s what happened this week...</p>',
      textBody: 'Here\'s your weekly summary...',
      category: 'reports',
      variables: [
        { name: 'dateRange', description: 'Date range for digest', example: 'Jan 1 - Jan 7, 2024', required: true },
        { name: 'queryCount', description: 'Number of queries', example: '42', required: false },
        { name: 'documentCount', description: 'Number of documents', example: '15', required: false }
      ],
      isActive: true,
      isDefault: true,
      createdAt: '2024-02-01T00:00:00Z',
      updatedAt: '2024-07-01T09:00:00Z',
      createdBy: 'system',
      lastEditedBy: 'admin'
    },
    {
      id: '5',
      name: 'Security Alert',
      slug: 'security-alert',
      subject: '[Action Required] Security Alert for Your Account',
      htmlBody: '<h1>Security Alert</h1><p>We detected unusual activity on your account.</p>',
      textBody: 'We detected unusual activity on your account.',
      category: 'alerts',
      variables: [
        { name: 'alertType', description: 'Type of security alert', example: 'Suspicious login', required: true },
        { name: 'ipAddress', description: 'IP address of activity', example: '192.168.1.1', required: false },
        { name: 'location', description: 'Geographic location', example: 'New York, US', required: false }
      ],
      isActive: true,
      isDefault: true,
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-05-10T11:00:00Z',
      createdBy: 'system',
      lastEditedBy: 'admin'
    }
  ]
}

function generateMockSystemConfiguration(): SystemConfiguration {
  return {
    general: {
      siteName: 'AEGIS RAG',
      siteUrl: 'https://aegis.example.com',
      adminEmail: 'admin@example.com',
      supportEmail: 'support@example.com',
      defaultLanguage: 'en',
      defaultTimezone: 'UTC',
      dateFormat: 'YYYY-MM-DD',
      timeFormat: 'HH:mm:ss',
      logo: null,
      favicon: null,
      theme: 'system'
    },
    security: {
      passwordMinLength: 12,
      passwordRequireUppercase: true,
      passwordRequireLowercase: true,
      passwordRequireNumbers: true,
      passwordRequireSpecial: true,
      passwordExpiryDays: 90,
      maxLoginAttempts: 5,
      lockoutDurationMinutes: 30,
      sessionTimeoutMinutes: 60,
      mfaRequired: false,
      mfaGracePeriodDays: 7,
      ipWhitelist: [],
      ipBlacklist: [],
      allowedOrigins: ['https://aegis.example.com']
    },
    authentication: {
      localAuthEnabled: true,
      ssoEnabled: false,
      ssoProviders: [],
      selfRegistrationEnabled: false,
      emailVerificationRequired: true,
      inviteOnlyMode: true,
      defaultRole: 'Viewer',
      apiKeyAuthEnabled: true,
      jwtExpiryMinutes: 60,
      refreshTokenExpiryDays: 30
    },
    storage: {
      provider: 'local',
      maxFileSizeMb: 100,
      allowedFileTypes: ['.pdf', '.docx', '.doc', '.txt', '.md', '.csv', '.xlsx'],
      maxStoragePerUserGb: 10,
      maxStoragePerWorkspaceGb: 50,
      retentionDays: 365,
      compressionEnabled: true,
      encryptionEnabled: true
    },
    llm: {
      provider: 'openai',
      model: 'gpt-4-turbo',
      apiEndpoint: 'https://api.openai.com/v1',
      maxTokens: 4096,
      temperature: 0.7,
      topP: 0.9,
      frequencyPenalty: 0,
      presencePenalty: 0,
      timeout: 60,
      retryAttempts: 3,
      streamingEnabled: true
    },
    notifications: {
      smtpEnabled: true,
      smtpHost: 'smtp.example.com',
      smtpPort: 587,
      smtpUsername: 'notifications@example.com',
      smtpSecure: true,
      fromEmail: 'noreply@aegis.example.com',
      fromName: 'AEGIS',
      webhooksEnabled: true,
      slackEnabled: false,
      teamsEnabled: false
    },
    limits: {
      maxUsersPerTeam: 100,
      maxTeamsPerOrg: 50,
      maxWorkspacesPerTeam: 25,
      maxDocumentsPerWorkspace: 1000,
      maxQueriesPerMinute: 60,
      maxQueriesPerDay: 10000,
      maxConcurrentSessions: 5,
      maxApiKeysPerUser: 10,
      maxWebhooksPerWorkspace: 10,
      maxFileUploadsPerDay: 100
    },
    featureFlags: [],
    maintenance: {
      enabled: false,
      message: 'The system is currently undergoing maintenance. Please check back later.',
      allowAdminAccess: true,
      scheduledMaintenances: []
    }
  }
}

function generateMockFeatureFlags(): FeatureFlag[] {
  return [
    {
      id: '1',
      name: 'New Chat Interface',
      key: 'new-chat-ui',
      description: 'Enable the redesigned chat interface with improved UX',
      enabled: true,
      enabledForRoles: ['Admin', 'SystemAdmin'],
      enabledForUsers: [],
      enabledForTeams: [],
      rolloutPercentage: 100,
      createdAt: '2024-06-01T00:00:00Z',
      updatedAt: '2024-07-15T10:00:00Z'
    },
    {
      id: '2',
      name: 'Advanced Analytics',
      key: 'advanced-analytics',
      description: 'Enable advanced analytics dashboard with custom reports',
      enabled: true,
      enabledForRoles: ['Analyst', 'Admin', 'SystemAdmin'],
      enabledForUsers: [],
      enabledForTeams: [],
      rolloutPercentage: 100,
      createdAt: '2024-05-15T00:00:00Z',
      updatedAt: '2024-07-01T09:00:00Z'
    },
    {
      id: '3',
      name: 'Document OCR',
      key: 'document-ocr',
      description: 'Enable OCR processing for scanned documents and images',
      enabled: false,
      enabledForRoles: [],
      enabledForUsers: [],
      enabledForTeams: [],
      rolloutPercentage: 0,
      createdAt: '2024-07-01T00:00:00Z',
      updatedAt: '2024-07-01T00:00:00Z'
    },
    {
      id: '4',
      name: 'Multi-Language Support',
      key: 'multi-language',
      description: 'Enable multi-language document processing and queries',
      enabled: true,
      enabledForRoles: [],
      enabledForUsers: [],
      enabledForTeams: [],
      rolloutPercentage: 50,
      createdAt: '2024-06-15T00:00:00Z',
      updatedAt: '2024-07-10T14:00:00Z'
    },
    {
      id: '5',
      name: 'Beta: Voice Input',
      key: 'voice-input',
      description: 'Enable voice-to-text for query input (beta)',
      enabled: false,
      enabledForRoles: ['SystemAdmin'],
      enabledForUsers: [],
      enabledForTeams: [],
      rolloutPercentage: 0,
      createdAt: '2024-07-15T00:00:00Z',
      updatedAt: '2024-07-15T00:00:00Z'
    },
    {
      id: '6',
      name: 'API v2',
      key: 'api-v2',
      description: 'Enable API version 2 endpoints with enhanced features',
      enabled: true,
      enabledForRoles: [],
      enabledForUsers: [],
      enabledForTeams: [],
      rolloutPercentage: 25,
      createdAt: '2024-07-01T00:00:00Z',
      updatedAt: '2024-07-20T08:00:00Z'
    }
  ]
}
