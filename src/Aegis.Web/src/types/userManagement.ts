// User Management Types for Admin UI
import type { UserRole } from './user'

// Extended user details for admin management
export interface ManagedUser {
  id: string
  email: string
  name: string
  role: UserRole
  teamId?: string
  teamName?: string
  avatarUrl?: string
  isActive: boolean
  isLocked: boolean
  emailVerified: boolean
  mfaEnabled: boolean
  createdAt: string
  updatedAt?: string
  lastLoginAt?: string
  lastActivityAt?: string
  loginCount: number
  failedLoginAttempts: number
  queryCount: number
  sessionCount: number
  workspaceCount: number
  documentCount: number
  permissions: UserPermission[]
  metadata?: Record<string, string>
}

// User permission for granular access control
export interface UserPermission {
  resource: string
  actions: PermissionAction[]
}

export type PermissionAction = 'create' | 'read' | 'update' | 'delete' | 'share' | 'admin'

// User status for filtering
export type UserStatus = 'all' | 'active' | 'inactive' | 'locked' | 'pending'

// User filters for list view
export interface UserFilters {
  search?: string
  status?: UserStatus
  role?: UserRole | null
  teamId?: string | null
  mfaEnabled?: boolean | null
  emailVerified?: boolean | null
  createdAfter?: string
  createdBefore?: string
  lastActiveAfter?: string
}

// Create managed user request (for admin)
export interface CreateManagedUserRequest {
  email: string
  name: string
  password: string
  role: UserRole
  teamId?: string
  sendWelcomeEmail?: boolean
  requirePasswordChange?: boolean
}

// Update managed user request (for admin)
export interface UpdateManagedUserRequest {
  name?: string
  email?: string
  role?: UserRole
  teamId?: string | null
  isActive?: boolean
  permissions?: UserPermission[]
}

// User action types
export type UserAction =
  | 'activate'
  | 'deactivate'
  | 'lock'
  | 'unlock'
  | 'resetPassword'
  | 'resendVerification'
  | 'enableMfa'
  | 'disableMfa'
  | 'delete'
  | 'impersonate'

// Bulk user action
export interface BulkUserAction {
  action: UserAction
  userIds: string[]
  reason?: string
}

// User activity log entry
export interface UserActivityEntry {
  id: string
  userId: string
  action: string
  resourceType: string
  resourceId?: string
  resourceName?: string
  ipAddress?: string
  userAgent?: string
  location?: string
  timestamp: string
  success: boolean
  details?: Record<string, unknown>
}

// User session info
export interface UserSession {
  id: string
  userId: string
  deviceType: 'desktop' | 'mobile' | 'tablet' | 'unknown'
  browser: string
  os: string
  ipAddress: string
  location?: string
  createdAt: string
  lastActiveAt: string
  expiresAt: string
  isCurrent: boolean
}

// Team management types
export interface ManagedTeam {
  id: string
  name: string
  description?: string
  ownerId: string
  ownerName: string
  memberCount: number
  workspaceCount: number
  isActive: boolean
  createdAt: string
  updatedAt?: string
  settings: TeamSettings
  members?: TeamMember[]
}

export interface TeamSettings {
  allowMemberInvites: boolean
  defaultMemberRole: UserRole
  maxMembers: number
  workspaceLimit: number
  queryRateLimit: number
  storageQuotaGb: number
}

export interface TeamMember {
  id: string
  userId: string
  userName: string
  userEmail: string
  avatarUrl?: string
  role: TeamMemberRole
  joinedAt: string
  lastActiveAt?: string
  isOwner: boolean
}

export type TeamMemberRole = 'member' | 'moderator' | 'admin' | 'owner'

// Team filters
export interface TeamFilters {
  search?: string
  isActive?: boolean | null
  minMembers?: number
  maxMembers?: number
  createdAfter?: string
  createdBefore?: string
}

// Create team request
export interface CreateTeamRequest {
  name: string
  description?: string
  settings?: Partial<TeamSettings>
}

// Update team request
export interface UpdateTeamRequest {
  name?: string
  description?: string
  isActive?: boolean
  settings?: Partial<TeamSettings>
}

// Add team member request
export interface AddTeamMemberRequest {
  userId: string
  role: TeamMemberRole
}

// User management statistics
export interface UserManagementStats {
  totalUsers: number
  activeUsers: number
  inactiveUsers: number
  lockedUsers: number
  pendingUsers: number
  mfaEnabledUsers: number
  usersByRole: Record<UserRole, number>
  newUsersLast7d: number
  newUsersLast30d: number
  activeUsersLast24h: number
  activeUsersLast7d: number
}

// Team management statistics
export interface TeamManagementStats {
  totalTeams: number
  activeTeams: number
  inactiveTeams: number
  totalMembers: number
  averageMembersPerTeam: number
  teamsCreatedLast30d: number
}

// Invitation types
export interface UserInvitation {
  id: string
  email: string
  role: UserRole
  teamId?: string
  teamName?: string
  invitedBy: string
  invitedByName: string
  status: InvitationStatus
  createdAt: string
  expiresAt: string
  acceptedAt?: string
}

export type InvitationStatus = 'pending' | 'accepted' | 'expired' | 'cancelled'

// Create invitation request
export interface CreateInvitationRequest {
  email: string
  role: UserRole
  teamId?: string
  message?: string
}

// Role definitions for UI display
export interface RoleDefinition {
  role: UserRole
  label: string
  description: string
  permissions: string[]
  color: string
}

export const ROLE_DEFINITIONS: RoleDefinition[] = [
  {
    role: 'Viewer' as UserRole,
    label: 'Viewer',
    description: 'Can view workspaces and run queries',
    permissions: ['View workspaces', 'Run queries', 'View documents'],
    color: 'gray'
  },
  {
    role: 'Contributor' as UserRole,
    label: 'Contributor',
    description: 'Can upload documents and manage data sources',
    permissions: ['All Viewer permissions', 'Upload documents', 'Manage data sources'],
    color: 'blue'
  },
  {
    role: 'Analyst' as UserRole,
    label: 'Analyst',
    description: 'Can create workspaces and manage sessions',
    permissions: ['All Contributor permissions', 'Create workspaces', 'Share sessions', 'Export data'],
    color: 'green'
  },
  {
    role: 'Admin' as UserRole,
    label: 'Admin',
    description: 'Can manage team members and settings',
    permissions: ['All Analyst permissions', 'Manage team members', 'Configure settings', 'View audit logs'],
    color: 'purple'
  },
  {
    role: 'SystemAdmin' as UserRole,
    label: 'System Admin',
    description: 'Full system access',
    permissions: ['All permissions', 'System configuration', 'User management', 'API key management'],
    color: 'red'
  }
]

// Utility functions
export function getRoleLabel(role: UserRole): string {
  const def = ROLE_DEFINITIONS.find(r => r.role === role)
  return def?.label || role
}

export function getRoleColor(role: UserRole): string {
  const def = ROLE_DEFINITIONS.find(r => r.role === role)
  return def?.color || 'gray'
}

export function getRoleDescription(role: UserRole): string {
  const def = ROLE_DEFINITIONS.find(r => r.role === role)
  return def?.description || ''
}

export function getUserStatusColor(status: UserStatus): string {
  switch (status) {
    case 'active':
      return 'green'
    case 'inactive':
      return 'gray'
    case 'locked':
      return 'red'
    case 'pending':
      return 'yellow'
    default:
      return 'gray'
  }
}

export function getInvitationStatusColor(status: InvitationStatus): string {
  switch (status) {
    case 'pending':
      return 'yellow'
    case 'accepted':
      return 'green'
    case 'expired':
      return 'gray'
    case 'cancelled':
      return 'red'
    default:
      return 'gray'
  }
}

export function formatUserStatus(user: ManagedUser): UserStatus {
  if (user.isLocked) return 'locked'
  if (!user.emailVerified) return 'pending'
  if (!user.isActive) return 'inactive'
  return 'active'
}

export function getTeamMemberRoleColor(role: TeamMemberRole): string {
  switch (role) {
    case 'owner':
      return 'purple'
    case 'admin':
      return 'red'
    case 'moderator':
      return 'blue'
    case 'member':
      return 'gray'
    default:
      return 'gray'
  }
}
