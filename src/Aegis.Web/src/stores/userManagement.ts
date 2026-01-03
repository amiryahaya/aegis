import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { UserRole } from '@/types/user'
import type {
  ManagedUser,
  ManagedTeam,
  TeamMember,
  UserFilters,
  TeamFilters,
  CreateManagedUserRequest,
  UpdateManagedUserRequest,
  CreateTeamRequest,
  UpdateTeamRequest,
  AddTeamMemberRequest,
  UserManagementStats,
  TeamManagementStats,
  UserInvitation,
  CreateInvitationRequest,
  UserActivityEntry,
  UserSession,
  UserAction,
  BulkUserAction,
  TeamMemberRole
} from '@/types'

export const useUserManagementStore = defineStore('userManagement', () => {
  // State
  const users = ref<ManagedUser[]>([])
  const teams = ref<ManagedTeam[]>([])
  const invitations = ref<UserInvitation[]>([])
  const selectedUser = ref<ManagedUser | null>(null)
  const selectedTeam = ref<ManagedTeam | null>(null)
  const userActivities = ref<UserActivityEntry[]>([])
  const userSessions = ref<UserSession[]>([])
  const userStats = ref<UserManagementStats | null>(null)
  const teamStats = ref<TeamManagementStats | null>(null)
  const userFilters = ref<UserFilters>({})
  const teamFilters = ref<TeamFilters>({})
  const isLoading = ref(false)
  const isLoadingUser = ref(false)
  const isLoadingTeam = ref(false)
  const error = ref<string | null>(null)
  const page = ref(1)
  const pageSize = ref(20)
  const totalUsers = ref(0)
  const totalTeams = ref(0)

  // Computed
  const totalUserPages = computed(() => Math.ceil(totalUsers.value / pageSize.value))
  const totalTeamPages = computed(() => Math.ceil(totalTeams.value / pageSize.value))

  const filteredUsers = computed(() => {
    let result = [...users.value]

    if (userFilters.value.search) {
      const search = userFilters.value.search.toLowerCase()
      result = result.filter(
        u =>
          u.name.toLowerCase().includes(search) ||
          u.email.toLowerCase().includes(search)
      )
    }

    if (userFilters.value.status && userFilters.value.status !== 'all') {
      result = result.filter(u => {
        switch (userFilters.value.status) {
          case 'active':
            return u.isActive && !u.isLocked && u.emailVerified
          case 'inactive':
            return !u.isActive
          case 'locked':
            return u.isLocked
          case 'pending':
            return !u.emailVerified
          default:
            return true
        }
      })
    }

    if (userFilters.value.role) {
      result = result.filter(u => u.role === userFilters.value.role)
    }

    if (userFilters.value.teamId) {
      result = result.filter(u => u.teamId === userFilters.value.teamId)
    }

    if (userFilters.value.mfaEnabled !== null && userFilters.value.mfaEnabled !== undefined) {
      result = result.filter(u => u.mfaEnabled === userFilters.value.mfaEnabled)
    }

    return result
  })

  const filteredTeams = computed(() => {
    let result = [...teams.value]

    if (teamFilters.value.search) {
      const search = teamFilters.value.search.toLowerCase()
      result = result.filter(
        t =>
          t.name.toLowerCase().includes(search) ||
          t.description?.toLowerCase().includes(search)
      )
    }

    if (teamFilters.value.isActive !== null && teamFilters.value.isActive !== undefined) {
      result = result.filter(t => t.isActive === teamFilters.value.isActive)
    }

    return result
  })

  // Actions
  async function fetchUsers(): Promise<void> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 500))
      users.value = generateMockUsers(50)
      totalUsers.value = users.value.length
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch users'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function fetchUserById(userId: string): Promise<ManagedUser | null> {
    isLoadingUser.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      const user = users.value.find(u => u.id === userId)
      if (user) {
        selectedUser.value = user
        return user
      }
      return null
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch user'
      throw err
    } finally {
      isLoadingUser.value = false
    }
  }

  async function createUser(request: CreateManagedUserRequest): Promise<ManagedUser> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 500))
      const newUser: ManagedUser = {
        id: crypto.randomUUID(),
        email: request.email,
        name: request.name,
        role: request.role,
        teamId: request.teamId,
        isActive: true,
        isLocked: false,
        emailVerified: !request.sendWelcomeEmail,
        mfaEnabled: false,
        createdAt: new Date().toISOString(),
        loginCount: 0,
        failedLoginAttempts: 0,
        queryCount: 0,
        sessionCount: 0,
        workspaceCount: 0,
        documentCount: 0,
        permissions: []
      }
      users.value.unshift(newUser)
      totalUsers.value++
      return newUser
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to create user'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function updateUser(userId: string, request: UpdateManagedUserRequest): Promise<ManagedUser | null> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      const index = users.value.findIndex(u => u.id === userId)
      if (index !== -1) {
        const updateData = {
          ...users.value[index],
          ...request,
          teamId: request.teamId === null ? undefined : request.teamId,
          updatedAt: new Date().toISOString()
        }
        users.value[index] = updateData
        if (selectedUser.value?.id === userId) {
          selectedUser.value = users.value[index]
        }
        return users.value[index]
      }
      return null
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to update user'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function deleteUser(userId: string): Promise<boolean> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      const index = users.value.findIndex(u => u.id === userId)
      if (index !== -1) {
        users.value.splice(index, 1)
        totalUsers.value--
        if (selectedUser.value?.id === userId) {
          selectedUser.value = null
        }
        return true
      }
      return false
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to delete user'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function performUserAction(userId: string, action: UserAction): Promise<boolean> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      const user = users.value.find(u => u.id === userId)
      if (!user) return false

      switch (action) {
        case 'activate':
          user.isActive = true
          break
        case 'deactivate':
          user.isActive = false
          break
        case 'lock':
          user.isLocked = true
          break
        case 'unlock':
          user.isLocked = false
          user.failedLoginAttempts = 0
          break
        case 'resetPassword':
          // Trigger password reset email
          break
        case 'resendVerification':
          // Resend verification email
          break
        case 'enableMfa':
          user.mfaEnabled = true
          break
        case 'disableMfa':
          user.mfaEnabled = false
          break
        case 'delete':
          return deleteUser(userId)
        case 'impersonate':
          // Start impersonation session
          break
      }

      user.updatedAt = new Date().toISOString()
      if (selectedUser.value?.id === userId) {
        selectedUser.value = { ...user }
      }
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to perform action'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function performBulkUserAction(bulkAction: BulkUserAction): Promise<number> {
    isLoading.value = true
    error.value = null

    try {
      let successCount = 0
      for (const userId of bulkAction.userIds) {
        const success = await performUserAction(userId, bulkAction.action)
        if (success) successCount++
      }
      return successCount
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to perform bulk action'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function fetchUserActivities(userId: string): Promise<void> {
    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      userActivities.value = generateMockUserActivities(userId, 20)
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch activities'
      throw err
    }
  }

  async function fetchUserSessions(userId: string): Promise<void> {
    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      userSessions.value = generateMockUserSessions(userId, 5)
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch sessions'
      throw err
    }
  }

  async function terminateUserSession(userId: string, sessionId: string): Promise<boolean> {
    try {
      await new Promise(resolve => setTimeout(resolve, 200))
      const index = userSessions.value.findIndex(s => s.id === sessionId && s.userId === userId)
      if (index !== -1) {
        userSessions.value.splice(index, 1)
        return true
      }
      return false
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to terminate session'
      throw err
    }
  }

  // Team actions
  async function fetchTeams(): Promise<void> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 500))
      teams.value = generateMockTeams(15)
      totalTeams.value = teams.value.length
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch teams'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function fetchTeamById(teamId: string): Promise<ManagedTeam | null> {
    isLoadingTeam.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      const team = teams.value.find(t => t.id === teamId)
      if (team) {
        team.members = generateMockTeamMembers(teamId, team.memberCount)
        selectedTeam.value = team
        return team
      }
      return null
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch team'
      throw err
    } finally {
      isLoadingTeam.value = false
    }
  }

  async function createTeam(request: CreateTeamRequest): Promise<ManagedTeam> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 500))
      const newTeam: ManagedTeam = {
        id: crypto.randomUUID(),
        name: request.name,
        description: request.description,
        ownerId: 'current-user-id',
        ownerName: 'Current User',
        memberCount: 1,
        workspaceCount: 0,
        isActive: true,
        createdAt: new Date().toISOString(),
        settings: {
          allowMemberInvites: true,
          defaultMemberRole: UserRole.Contributor,
          maxMembers: 50,
          workspaceLimit: 10,
          queryRateLimit: 1000,
          storageQuotaGb: 100,
          ...request.settings
        }
      }
      teams.value.unshift(newTeam)
      totalTeams.value++
      return newTeam
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to create team'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function updateTeam(teamId: string, request: UpdateTeamRequest): Promise<ManagedTeam | null> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      const index = teams.value.findIndex(t => t.id === teamId)
      if (index !== -1) {
        teams.value[index] = {
          ...teams.value[index],
          ...request,
          settings: request.settings
            ? { ...teams.value[index].settings, ...request.settings }
            : teams.value[index].settings,
          updatedAt: new Date().toISOString()
        }
        if (selectedTeam.value?.id === teamId) {
          selectedTeam.value = teams.value[index]
        }
        return teams.value[index]
      }
      return null
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to update team'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function deleteTeam(teamId: string): Promise<boolean> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      const index = teams.value.findIndex(t => t.id === teamId)
      if (index !== -1) {
        teams.value.splice(index, 1)
        totalTeams.value--
        if (selectedTeam.value?.id === teamId) {
          selectedTeam.value = null
        }
        return true
      }
      return false
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to delete team'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function addTeamMember(teamId: string, request: AddTeamMemberRequest): Promise<TeamMember | null> {
    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      const team = teams.value.find(t => t.id === teamId)
      if (team) {
        const user = users.value.find(u => u.id === request.userId)
        if (user) {
          const member: TeamMember = {
            id: crypto.randomUUID(),
            userId: user.id,
            userName: user.name,
            userEmail: user.email,
            avatarUrl: user.avatarUrl,
            role: request.role,
            joinedAt: new Date().toISOString(),
            isOwner: false
          }
          team.members = team.members || []
          team.members.push(member)
          team.memberCount++
          return member
        }
      }
      return null
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to add member'
      throw err
    }
  }

  async function removeTeamMember(teamId: string, memberId: string): Promise<boolean> {
    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      const team = teams.value.find(t => t.id === teamId)
      if (team && team.members) {
        const index = team.members.findIndex(m => m.id === memberId)
        if (index !== -1) {
          team.members.splice(index, 1)
          team.memberCount--
          return true
        }
      }
      return false
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to remove member'
      throw err
    }
  }

  async function updateTeamMemberRole(teamId: string, memberId: string, role: TeamMemberRole): Promise<boolean> {
    try {
      await new Promise(resolve => setTimeout(resolve, 200))
      const team = teams.value.find(t => t.id === teamId)
      if (team && team.members) {
        const member = team.members.find(m => m.id === memberId)
        if (member) {
          member.role = role
          return true
        }
      }
      return false
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to update member role'
      throw err
    }
  }

  // Invitation actions
  async function fetchInvitations(): Promise<void> {
    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      invitations.value = generateMockInvitations(10)
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch invitations'
      throw err
    }
  }

  async function createInvitation(request: CreateInvitationRequest): Promise<UserInvitation> {
    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      const invitation: UserInvitation = {
        id: crypto.randomUUID(),
        email: request.email,
        role: request.role,
        teamId: request.teamId,
        invitedBy: 'current-user-id',
        invitedByName: 'Current User',
        status: 'pending',
        createdAt: new Date().toISOString(),
        expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString()
      }
      invitations.value.unshift(invitation)
      return invitation
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to create invitation'
      throw err
    }
  }

  async function cancelInvitation(invitationId: string): Promise<boolean> {
    try {
      await new Promise(resolve => setTimeout(resolve, 200))
      const invitation = invitations.value.find(i => i.id === invitationId)
      if (invitation && invitation.status === 'pending') {
        invitation.status = 'cancelled'
        return true
      }
      return false
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to cancel invitation'
      throw err
    }
  }

  async function resendInvitation(invitationId: string): Promise<boolean> {
    try {
      await new Promise(resolve => setTimeout(resolve, 200))
      const invitation = invitations.value.find(i => i.id === invitationId)
      if (invitation && invitation.status === 'pending') {
        invitation.expiresAt = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString()
        return true
      }
      return false
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to resend invitation'
      throw err
    }
  }

  // Stats
  async function fetchUserStats(): Promise<void> {
    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      userStats.value = generateMockUserStats()
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch stats'
      throw err
    }
  }

  async function fetchTeamStats(): Promise<void> {
    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      teamStats.value = generateMockTeamStats()
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch team stats'
      throw err
    }
  }

  // Filter actions
  function setUserFilters(filters: UserFilters): void {
    userFilters.value = { ...userFilters.value, ...filters }
  }

  function clearUserFilters(): void {
    userFilters.value = {}
  }

  function setTeamFilters(filters: TeamFilters): void {
    teamFilters.value = { ...teamFilters.value, ...filters }
  }

  function clearTeamFilters(): void {
    teamFilters.value = {}
  }

  function selectUser(user: ManagedUser | null): void {
    selectedUser.value = user
  }

  function selectTeam(team: ManagedTeam | null): void {
    selectedTeam.value = team
  }

  function clearError(): void {
    error.value = null
  }

  return {
    // State
    users,
    teams,
    invitations,
    selectedUser,
    selectedTeam,
    userActivities,
    userSessions,
    userStats,
    teamStats,
    userFilters,
    teamFilters,
    isLoading,
    isLoadingUser,
    isLoadingTeam,
    error,
    page,
    pageSize,
    totalUsers,
    totalTeams,
    // Computed
    totalUserPages,
    totalTeamPages,
    filteredUsers,
    filteredTeams,
    // User actions
    fetchUsers,
    fetchUserById,
    createUser,
    updateUser,
    deleteUser,
    performUserAction,
    performBulkUserAction,
    fetchUserActivities,
    fetchUserSessions,
    terminateUserSession,
    // Team actions
    fetchTeams,
    fetchTeamById,
    createTeam,
    updateTeam,
    deleteTeam,
    addTeamMember,
    removeTeamMember,
    updateTeamMemberRole,
    // Invitation actions
    fetchInvitations,
    createInvitation,
    cancelInvitation,
    resendInvitation,
    // Stats
    fetchUserStats,
    fetchTeamStats,
    // Filter actions
    setUserFilters,
    clearUserFilters,
    setTeamFilters,
    clearTeamFilters,
    selectUser,
    selectTeam,
    clearError
  }
})

// Mock data generators
function generateMockUsers(count: number): ManagedUser[] {
  const roles: UserRole[] = [UserRole.Viewer, UserRole.Contributor, UserRole.Analyst, UserRole.Admin, UserRole.SystemAdmin]
  const firstNames = ['John', 'Jane', 'Michael', 'Emily', 'David', 'Sarah', 'James', 'Emma', 'Robert', 'Olivia']
  const lastNames = ['Smith', 'Johnson', 'Williams', 'Brown', 'Jones', 'Miller', 'Davis', 'Garcia', 'Wilson', 'Taylor']
  const domains = ['example.com', 'company.io', 'org.net', 'mail.com']

  const users: ManagedUser[] = []

  for (let i = 0; i < count; i++) {
    const firstName = firstNames[Math.floor(Math.random() * firstNames.length)]
    const lastName = lastNames[Math.floor(Math.random() * lastNames.length)]
    const name = `${firstName} ${lastName}`
    const email = `${firstName.toLowerCase()}.${lastName.toLowerCase()}${i}@${domains[Math.floor(Math.random() * domains.length)]}`
    const role = roles[Math.floor(Math.random() * roles.length)]
    const isActive = Math.random() > 0.1
    const isLocked = Math.random() < 0.05
    const emailVerified = Math.random() > 0.1
    const mfaEnabled = Math.random() > 0.7

    users.push({
      id: crypto.randomUUID(),
      email,
      name,
      role,
      teamId: Math.random() > 0.3 ? crypto.randomUUID() : undefined,
      teamName: Math.random() > 0.3 ? `Team ${Math.floor(Math.random() * 10) + 1}` : undefined,
      isActive,
      isLocked,
      emailVerified,
      mfaEnabled,
      createdAt: new Date(Date.now() - Math.floor(Math.random() * 365 * 24 * 60 * 60 * 1000)).toISOString(),
      lastLoginAt: isActive ? new Date(Date.now() - Math.floor(Math.random() * 30 * 24 * 60 * 60 * 1000)).toISOString() : undefined,
      lastActivityAt: isActive ? new Date(Date.now() - Math.floor(Math.random() * 7 * 24 * 60 * 60 * 1000)).toISOString() : undefined,
      loginCount: Math.floor(Math.random() * 500) + 1,
      failedLoginAttempts: isLocked ? Math.floor(Math.random() * 5) + 5 : Math.floor(Math.random() * 3),
      queryCount: Math.floor(Math.random() * 1000),
      sessionCount: Math.floor(Math.random() * 100),
      workspaceCount: Math.floor(Math.random() * 10),
      documentCount: Math.floor(Math.random() * 50),
      permissions: []
    })
  }

  return users.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
}

function generateMockTeams(count: number): ManagedTeam[] {
  const teamNames = ['Engineering', 'Product', 'Design', 'Marketing', 'Sales', 'Support', 'Research', 'Operations', 'Finance', 'Legal', 'HR', 'Analytics', 'Security', 'DevOps', 'QA']

  const teams: ManagedTeam[] = []

  for (let i = 0; i < count; i++) {
    const name = i < teamNames.length ? teamNames[i] : `Team ${i + 1}`
    const memberCount = Math.floor(Math.random() * 20) + 2

    teams.push({
      id: crypto.randomUUID(),
      name,
      description: `The ${name} team handles all ${name.toLowerCase()}-related tasks and projects.`,
      ownerId: crypto.randomUUID(),
      ownerName: `${name} Lead`,
      memberCount,
      workspaceCount: Math.floor(Math.random() * 10) + 1,
      isActive: Math.random() > 0.1,
      createdAt: new Date(Date.now() - Math.floor(Math.random() * 365 * 24 * 60 * 60 * 1000)).toISOString(),
      settings: {
        allowMemberInvites: Math.random() > 0.3,
        defaultMemberRole: UserRole.Contributor,
        maxMembers: 50,
        workspaceLimit: 10,
        queryRateLimit: 1000,
        storageQuotaGb: 100
      }
    })
  }

  return teams.sort((a, b) => a.name.localeCompare(b.name))
}

function generateMockTeamMembers(_teamId: string, count: number): TeamMember[] {
  const roles: TeamMemberRole[] = ['member', 'moderator', 'admin', 'owner']
  const members: TeamMember[] = []

  for (let i = 0; i < count; i++) {
    const isOwner = i === 0
    members.push({
      id: crypto.randomUUID(),
      userId: crypto.randomUUID(),
      userName: `Member ${i + 1}`,
      userEmail: `member${i + 1}@example.com`,
      role: isOwner ? 'owner' : roles[Math.floor(Math.random() * 3)],
      joinedAt: new Date(Date.now() - Math.floor(Math.random() * 180 * 24 * 60 * 60 * 1000)).toISOString(),
      lastActiveAt: new Date(Date.now() - Math.floor(Math.random() * 7 * 24 * 60 * 60 * 1000)).toISOString(),
      isOwner
    })
  }

  return members
}

function generateMockUserActivities(userId: string, count: number): UserActivityEntry[] {
  const actions = ['Login', 'Query', 'Upload', 'Share', 'Export', 'Settings', 'API Call', 'View']
  const resourceTypes = ['Session', 'Document', 'Workspace', 'DataSource', 'ApiKey', 'Team']
  const activities: UserActivityEntry[] = []

  for (let i = 0; i < count; i++) {
    activities.push({
      id: crypto.randomUUID(),
      userId,
      action: actions[Math.floor(Math.random() * actions.length)],
      resourceType: resourceTypes[Math.floor(Math.random() * resourceTypes.length)],
      resourceId: crypto.randomUUID().slice(0, 8),
      resourceName: `Resource ${i + 1}`,
      ipAddress: `192.168.${Math.floor(Math.random() * 255)}.${Math.floor(Math.random() * 255)}`,
      timestamp: new Date(Date.now() - Math.floor(Math.random() * 7 * 24 * 60 * 60 * 1000)).toISOString(),
      success: Math.random() > 0.1
    })
  }

  return activities.sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
}

function generateMockUserSessions(userId: string, count: number): UserSession[] {
  const browsers = ['Chrome', 'Firefox', 'Safari', 'Edge']
  const oses = ['macOS', 'Windows', 'Linux', 'iOS', 'Android']
  const deviceTypes: Array<'desktop' | 'mobile' | 'tablet'> = ['desktop', 'mobile', 'tablet']
  const sessions: UserSession[] = []

  for (let i = 0; i < count; i++) {
    sessions.push({
      id: crypto.randomUUID(),
      userId,
      deviceType: deviceTypes[Math.floor(Math.random() * deviceTypes.length)],
      browser: browsers[Math.floor(Math.random() * browsers.length)],
      os: oses[Math.floor(Math.random() * oses.length)],
      ipAddress: `192.168.${Math.floor(Math.random() * 255)}.${Math.floor(Math.random() * 255)}`,
      location: Math.random() > 0.5 ? 'San Francisco, CA' : undefined,
      createdAt: new Date(Date.now() - Math.floor(Math.random() * 7 * 24 * 60 * 60 * 1000)).toISOString(),
      lastActiveAt: new Date(Date.now() - Math.floor(Math.random() * 24 * 60 * 60 * 1000)).toISOString(),
      expiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
      isCurrent: i === 0
    })
  }

  return sessions.sort((a, b) => new Date(b.lastActiveAt).getTime() - new Date(a.lastActiveAt).getTime())
}

function generateMockInvitations(count: number): UserInvitation[] {
  const statuses: Array<'pending' | 'accepted' | 'expired' | 'cancelled'> = ['pending', 'accepted', 'expired', 'cancelled']
  const roles: UserRole[] = [UserRole.Viewer, UserRole.Contributor, UserRole.Analyst]
  const invitations: UserInvitation[] = []

  for (let i = 0; i < count; i++) {
    const status = statuses[Math.floor(Math.random() * statuses.length)]
    const createdAt = new Date(Date.now() - Math.floor(Math.random() * 30 * 24 * 60 * 60 * 1000))

    invitations.push({
      id: crypto.randomUUID(),
      email: `invited${i + 1}@example.com`,
      role: roles[Math.floor(Math.random() * roles.length)],
      teamId: Math.random() > 0.5 ? crypto.randomUUID() : undefined,
      teamName: Math.random() > 0.5 ? `Team ${Math.floor(Math.random() * 5) + 1}` : undefined,
      invitedBy: crypto.randomUUID(),
      invitedByName: 'Admin User',
      status,
      createdAt: createdAt.toISOString(),
      expiresAt: new Date(createdAt.getTime() + 7 * 24 * 60 * 60 * 1000).toISOString(),
      acceptedAt: status === 'accepted' ? new Date(createdAt.getTime() + Math.floor(Math.random() * 3 * 24 * 60 * 60 * 1000)).toISOString() : undefined
    })
  }

  return invitations.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
}

function generateMockUserStats(): UserManagementStats {
  return {
    totalUsers: 156,
    activeUsers: 142,
    inactiveUsers: 8,
    lockedUsers: 3,
    pendingUsers: 3,
    mfaEnabledUsers: 89,
    usersByRole: {
      [UserRole.Viewer]: 45,
      [UserRole.Contributor]: 62,
      [UserRole.Analyst]: 35,
      [UserRole.Admin]: 12,
      [UserRole.SystemAdmin]: 2
    },
    newUsersLast7d: 8,
    newUsersLast30d: 23,
    activeUsersLast24h: 67,
    activeUsersLast7d: 134
  }
}

function generateMockTeamStats(): TeamManagementStats {
  return {
    totalTeams: 15,
    activeTeams: 14,
    inactiveTeams: 1,
    totalMembers: 156,
    averageMembersPerTeam: 10.4,
    teamsCreatedLast30d: 2
  }
}
