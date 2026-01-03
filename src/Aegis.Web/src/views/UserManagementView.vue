<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import {
  TabGroup,
  TabList,
  Tab,
  TabPanels,
  TabPanel,
  Listbox,
  ListboxButton,
  ListboxOptions,
  ListboxOption
} from '@headlessui/vue'
import {
  UserGroupIcon,
  UsersIcon,
  MagnifyingGlassIcon,
  PlusIcon,
  ChevronUpDownIcon,
  CheckIcon,
  ArrowPathIcon,
  ShieldCheckIcon,
  LockClosedIcon,
  ClockIcon,
  TrashIcon,
  ArchiveBoxIcon
} from '@heroicons/vue/24/outline'
import { useUserManagementStore } from '@/stores/userManagement'
import UserTable from '@/components/users/UserTable.vue'
import UserDetailPanel from '@/components/users/UserDetailPanel.vue'
import UserFormDialog from '@/components/users/UserFormDialog.vue'
import TeamTable from '@/components/teams/TeamTable.vue'
import TeamDetailPanel from '@/components/teams/TeamDetailPanel.vue'
import TeamFormDialog from '@/components/teams/TeamFormDialog.vue'
import type { ManagedUser, ManagedTeam, UserFilters, TeamFilters, UserAction, UserStatus, CreateManagedUserRequest, UpdateManagedUserRequest, CreateTeamRequest, UpdateTeamRequest, TeamMemberRole } from '@/types'
import { UserRole } from '@/types/user'

const store = useUserManagementStore()

// Tab state
const selectedTab = ref(0)

// User state
const userSearch = ref('')
const userStatusFilter = ref<UserStatus>('all')
const userRoleFilter = ref<UserRole | null>(null)
const userTeamFilter = ref<string | null>(null)
const selectedUserIds = ref<string[]>([])
const selectedUser = ref<ManagedUser | null>(null)
const showUserDetail = ref(false)
const showUserForm = ref(false)
const editingUser = ref<ManagedUser | null>(null)
const userSortBy = ref('name')
const userSortDescending = ref(false)

// Team state
const teamSearch = ref('')
const teamActiveFilter = ref<boolean | null>(null)
const selectedTeamIds = ref<string[]>([])
const selectedTeam = ref<ManagedTeam | null>(null)
const showTeamDetail = ref(false)
const showTeamForm = ref(false)
const editingTeam = ref<ManagedTeam | null>(null)
const teamSortBy = ref('name')
const teamSortDescending = ref(false)

// Status options
const statusOptions: { value: UserStatus; label: string }[] = [
  { value: 'all', label: 'All Status' },
  { value: 'active', label: 'Active' },
  { value: 'inactive', label: 'Inactive' },
  { value: 'locked', label: 'Locked' },
  { value: 'pending', label: 'Pending' }
]

// Role options
const roleOptions: { value: UserRole | null; label: string }[] = [
  { value: null, label: 'All Roles' },
  { value: UserRole.Viewer, label: 'Viewer' },
  { value: UserRole.Contributor, label: 'Contributor' },
  { value: UserRole.Analyst, label: 'Analyst' },
  { value: UserRole.Admin, label: 'Admin' },
  { value: UserRole.SystemAdmin, label: 'System Admin' }
]

// Computed filters
const userFilters = computed<UserFilters>(() => ({
  search: userSearch.value || undefined,
  status: userStatusFilter.value,
  role: userRoleFilter.value,
  teamId: userTeamFilter.value
}))

const teamFilters = computed<TeamFilters>(() => ({
  search: teamSearch.value || undefined,
  isActive: teamActiveFilter.value
}))

// Sorted and filtered users
const sortedUsers = computed(() => {
  const users = [...store.users]
  users.sort((a, b) => {
    let comparison = 0
    switch (userSortBy.value) {
      case 'name':
        comparison = a.name.localeCompare(b.name)
        break
      case 'role':
        comparison = a.role.localeCompare(b.role)
        break
      case 'team':
        comparison = (a.teamName || '').localeCompare(b.teamName || '')
        break
      case 'lastLoginAt':
        comparison = new Date(a.lastLoginAt || 0).getTime() - new Date(b.lastLoginAt || 0).getTime()
        break
      default:
        comparison = a.name.localeCompare(b.name)
    }
    return userSortDescending.value ? -comparison : comparison
  })
  return users
})

// Sorted and filtered teams
const sortedTeams = computed(() => {
  const teams = [...store.teams]
  teams.sort((a, b) => {
    let comparison = 0
    switch (teamSortBy.value) {
      case 'name':
        comparison = a.name.localeCompare(b.name)
        break
      case 'memberCount':
        comparison = a.memberCount - b.memberCount
        break
      case 'workspaceCount':
        comparison = a.workspaceCount - b.workspaceCount
        break
      case 'ownerName':
        comparison = a.ownerName.localeCompare(b.ownerName)
        break
      case 'createdAt':
        comparison = new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
        break
      default:
        comparison = a.name.localeCompare(b.name)
    }
    return teamSortDescending.value ? -comparison : comparison
  })
  return teams
})

// Fetch data on mount
onMounted(async () => {
  await Promise.all([
    store.fetchUsers(),
    store.fetchTeams(),
    store.fetchUserStats(),
    store.fetchTeamStats()
  ])
})

// Watch for filter changes - set filters and refetch
watch(userFilters, async (filters) => {
  store.setUserFilters(filters)
}, { deep: true })

watch(teamFilters, async (filters) => {
  store.setTeamFilters(filters)
}, { deep: true })

// User handlers
function handleUserSort(field: string) {
  if (userSortBy.value === field) {
    userSortDescending.value = !userSortDescending.value
  } else {
    userSortBy.value = field
    userSortDescending.value = false
  }
}

function handleSelectUser(user: ManagedUser) {
  selectedUser.value = user
  showUserDetail.value = true
}

async function handleUserAction(userId: string, action: UserAction) {
  await store.performUserAction(userId, action)
}

function handleToggleSelectUser(userId: string) {
  const index = selectedUserIds.value.indexOf(userId)
  if (index === -1) {
    selectedUserIds.value.push(userId)
  } else {
    selectedUserIds.value.splice(index, 1)
  }
}

function handleSelectAllUsers(selected: boolean) {
  if (selected) {
    selectedUserIds.value = store.users.map(u => u.id)
  } else {
    selectedUserIds.value = []
  }
}

function handleCreateUser() {
  editingUser.value = null
  showUserForm.value = true
}

function handleEditUser(user: ManagedUser) {
  editingUser.value = user
  showUserForm.value = true
}

async function handleSaveUser(data: CreateManagedUserRequest | UpdateManagedUserRequest) {
  if (editingUser.value) {
    await store.updateUser(editingUser.value.id, data as UpdateManagedUserRequest)
  } else {
    await store.createUser(data as CreateManagedUserRequest)
  }
  showUserForm.value = false
  editingUser.value = null
}

async function handleBulkUserAction(action: UserAction) {
  if (selectedUserIds.value.length === 0) return
  await store.performBulkUserAction({
    action,
    userIds: selectedUserIds.value
  })
  selectedUserIds.value = []
}

// Team handlers
function handleTeamSort(field: string) {
  if (teamSortBy.value === field) {
    teamSortDescending.value = !teamSortDescending.value
  } else {
    teamSortBy.value = field
    teamSortDescending.value = false
  }
}

function handleSelectTeam(team: ManagedTeam) {
  selectedTeam.value = team
  showTeamDetail.value = true
}

async function handleTeamAction(teamId: string, action: string) {
  switch (action) {
    case 'edit':
      const team = store.teams.find(t => t.id === teamId)
      if (team) {
        editingTeam.value = team
        showTeamForm.value = true
      }
      break
    case 'activate':
      await store.updateTeam(teamId, { isActive: true })
      break
    case 'deactivate':
      await store.updateTeam(teamId, { isActive: false })
      break
    case 'manageMembers':
      const teamToManage = store.teams.find(t => t.id === teamId)
      if (teamToManage) {
        selectedTeam.value = teamToManage
        showTeamDetail.value = true
      }
      break
    case 'delete':
      await store.deleteTeam(teamId)
      break
  }
}

function handleToggleSelectTeam(teamId: string) {
  const index = selectedTeamIds.value.indexOf(teamId)
  if (index === -1) {
    selectedTeamIds.value.push(teamId)
  } else {
    selectedTeamIds.value.splice(index, 1)
  }
}

function handleSelectAllTeams(selected: boolean) {
  if (selected) {
    selectedTeamIds.value = store.teams.map(t => t.id)
  } else {
    selectedTeamIds.value = []
  }
}

function handleCreateTeam() {
  editingTeam.value = null
  showTeamForm.value = true
}

async function handleSaveTeam(data: CreateTeamRequest | UpdateTeamRequest, teamId?: string) {
  if (teamId) {
    await store.updateTeam(teamId, data as UpdateTeamRequest)
  } else {
    await store.createTeam(data as CreateTeamRequest)
  }
  showTeamForm.value = false
  editingTeam.value = null
}

async function handleAddTeamMember(teamId: string, userId: string, role: TeamMemberRole) {
  await store.addTeamMember(teamId, { userId, role })
  // Refresh team to get updated members
  const team = await store.fetchTeamById(teamId)
  if (team) {
    selectedTeam.value = team
  }
}

async function handleRemoveTeamMember(teamId: string, memberId: string) {
  await store.removeTeamMember(teamId, memberId)
  // Refresh team
  const team = await store.fetchTeamById(teamId)
  if (team) {
    selectedTeam.value = team
  }
}

async function handleRefresh() {
  await Promise.all([
    store.fetchUsers(),
    store.fetchTeams()
  ])
}
</script>

<template>
  <div class="min-h-screen bg-gray-50 dark:bg-gray-950">
    <div class="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
      <!-- Header -->
      <div class="mb-8">
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">User Management</h1>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Manage users, teams, and permissions
        </p>
      </div>

      <!-- Stats -->
      <div class="mb-8 grid grid-cols-2 gap-4 sm:grid-cols-4 lg:grid-cols-6">
        <div class="rounded-lg bg-white p-4 shadow dark:bg-gray-900">
          <div class="flex items-center gap-2">
            <UsersIcon class="h-5 w-5 text-blue-500" />
            <span class="text-sm text-gray-500 dark:text-gray-400">Total Users</span>
          </div>
          <p class="mt-1 text-2xl font-semibold text-gray-900 dark:text-white">
            {{ store.userStats?.totalUsers ?? 0 }}
          </p>
        </div>
        <div class="rounded-lg bg-white p-4 shadow dark:bg-gray-900">
          <div class="flex items-center gap-2">
            <CheckIcon class="h-5 w-5 text-green-500" />
            <span class="text-sm text-gray-500 dark:text-gray-400">Active</span>
          </div>
          <p class="mt-1 text-2xl font-semibold text-gray-900 dark:text-white">
            {{ store.userStats?.activeUsers ?? 0 }}
          </p>
        </div>
        <div class="rounded-lg bg-white p-4 shadow dark:bg-gray-900">
          <div class="flex items-center gap-2">
            <LockClosedIcon class="h-5 w-5 text-red-500" />
            <span class="text-sm text-gray-500 dark:text-gray-400">Locked</span>
          </div>
          <p class="mt-1 text-2xl font-semibold text-gray-900 dark:text-white">
            {{ store.userStats?.lockedUsers ?? 0 }}
          </p>
        </div>
        <div class="rounded-lg bg-white p-4 shadow dark:bg-gray-900">
          <div class="flex items-center gap-2">
            <ClockIcon class="h-5 w-5 text-yellow-500" />
            <span class="text-sm text-gray-500 dark:text-gray-400">Pending</span>
          </div>
          <p class="mt-1 text-2xl font-semibold text-gray-900 dark:text-white">
            {{ store.userStats?.pendingUsers ?? 0 }}
          </p>
        </div>
        <div class="rounded-lg bg-white p-4 shadow dark:bg-gray-900">
          <div class="flex items-center gap-2">
            <ShieldCheckIcon class="h-5 w-5 text-purple-500" />
            <span class="text-sm text-gray-500 dark:text-gray-400">MFA Enabled</span>
          </div>
          <p class="mt-1 text-2xl font-semibold text-gray-900 dark:text-white">
            {{ store.userStats?.mfaEnabledUsers ?? 0 }}
          </p>
        </div>
        <div class="rounded-lg bg-white p-4 shadow dark:bg-gray-900">
          <div class="flex items-center gap-2">
            <UserGroupIcon class="h-5 w-5 text-aegis-500" />
            <span class="text-sm text-gray-500 dark:text-gray-400">Teams</span>
          </div>
          <p class="mt-1 text-2xl font-semibold text-gray-900 dark:text-white">
            {{ store.teamStats?.totalTeams ?? 0 }}
          </p>
        </div>
      </div>

      <!-- Tabs -->
      <TabGroup :selected-index="selectedTab" @change="selectedTab = $event">
        <div class="mb-6 flex items-center justify-between">
          <TabList class="flex space-x-1 rounded-xl bg-gray-100 p-1 dark:bg-gray-800">
            <Tab v-slot="{ selected }" as="template">
              <button
                :class="[
                  'flex items-center gap-2 rounded-lg px-4 py-2.5 text-sm font-medium leading-5',
                  'focus:outline-none focus:ring-2 focus:ring-aegis-500 focus:ring-offset-2 dark:focus:ring-offset-gray-900',
                  selected
                    ? 'bg-white text-aegis-700 shadow dark:bg-gray-700 dark:text-aegis-400'
                    : 'text-gray-500 hover:bg-white/50 hover:text-gray-700 dark:text-gray-400 dark:hover:bg-gray-700/50'
                ]"
              >
                <UsersIcon class="h-4 w-4" />
                Users
              </button>
            </Tab>
            <Tab v-slot="{ selected }" as="template">
              <button
                :class="[
                  'flex items-center gap-2 rounded-lg px-4 py-2.5 text-sm font-medium leading-5',
                  'focus:outline-none focus:ring-2 focus:ring-aegis-500 focus:ring-offset-2 dark:focus:ring-offset-gray-900',
                  selected
                    ? 'bg-white text-aegis-700 shadow dark:bg-gray-700 dark:text-aegis-400'
                    : 'text-gray-500 hover:bg-white/50 hover:text-gray-700 dark:text-gray-400 dark:hover:bg-gray-700/50'
                ]"
              >
                <UserGroupIcon class="h-4 w-4" />
                Teams
              </button>
            </Tab>
          </TabList>

          <button
            class="rounded-lg p-2 text-gray-400 hover:bg-gray-100 hover:text-gray-600 dark:hover:bg-gray-800"
            title="Refresh"
            @click="handleRefresh"
          >
            <ArrowPathIcon class="h-5 w-5" :class="{ 'animate-spin': store.isLoading }" />
          </button>
        </div>

        <TabPanels>
          <!-- Users Tab -->
          <TabPanel>
            <div class="rounded-xl bg-white shadow dark:bg-gray-900">
              <!-- Filters -->
              <div class="border-b p-4 dark:border-gray-700">
                <div class="flex flex-wrap items-center gap-4">
                  <!-- Search -->
                  <div class="relative flex-1">
                    <MagnifyingGlassIcon class="absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-gray-400" />
                    <input
                      v-model="userSearch"
                      type="text"
                      placeholder="Search users..."
                      class="w-full rounded-lg border-gray-300 pl-10 focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-800 dark:text-white sm:text-sm"
                    />
                  </div>

                  <!-- Status Filter -->
                  <Listbox v-model="userStatusFilter">
                    <div class="relative w-40">
                      <ListboxButton
                        class="relative w-full cursor-pointer rounded-lg border border-gray-300 bg-white py-2 pl-3 pr-10 text-left focus:border-aegis-500 focus:outline-none focus:ring-1 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-800 sm:text-sm"
                      >
                        <span class="block truncate text-gray-900 dark:text-white">
                          {{ statusOptions.find(s => s.value === userStatusFilter)?.label }}
                        </span>
                        <span class="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-2">
                          <ChevronUpDownIcon class="h-5 w-5 text-gray-400" />
                        </span>
                      </ListboxButton>
                      <transition
                        leave-active-class="transition duration-100 ease-in"
                        leave-from-class="opacity-100"
                        leave-to-class="opacity-0"
                      >
                        <ListboxOptions
                          class="absolute z-10 mt-1 max-h-60 w-full overflow-auto rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800 sm:text-sm"
                        >
                          <ListboxOption
                            v-for="option in statusOptions"
                            :key="option.value"
                            v-slot="{ active, selected }"
                            :value="option.value"
                            as="template"
                          >
                            <li
                              :class="[
                                active ? 'bg-aegis-100 text-aegis-900 dark:bg-aegis-900/30 dark:text-aegis-400' : 'text-gray-900 dark:text-gray-100',
                                'relative cursor-pointer select-none py-2 pl-10 pr-4'
                              ]"
                            >
                              <span :class="[selected ? 'font-medium' : 'font-normal', 'block truncate']">
                                {{ option.label }}
                              </span>
                              <span v-if="selected" class="absolute inset-y-0 left-0 flex items-center pl-3 text-aegis-600">
                                <CheckIcon class="h-5 w-5" />
                              </span>
                            </li>
                          </ListboxOption>
                        </ListboxOptions>
                      </transition>
                    </div>
                  </Listbox>

                  <!-- Role Filter -->
                  <Listbox v-model="userRoleFilter">
                    <div class="relative w-40">
                      <ListboxButton
                        class="relative w-full cursor-pointer rounded-lg border border-gray-300 bg-white py-2 pl-3 pr-10 text-left focus:border-aegis-500 focus:outline-none focus:ring-1 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-800 sm:text-sm"
                      >
                        <span class="block truncate text-gray-900 dark:text-white">
                          {{ roleOptions.find(r => r.value === userRoleFilter)?.label }}
                        </span>
                        <span class="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-2">
                          <ChevronUpDownIcon class="h-5 w-5 text-gray-400" />
                        </span>
                      </ListboxButton>
                      <transition
                        leave-active-class="transition duration-100 ease-in"
                        leave-from-class="opacity-100"
                        leave-to-class="opacity-0"
                      >
                        <ListboxOptions
                          class="absolute z-10 mt-1 max-h-60 w-full overflow-auto rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800 sm:text-sm"
                        >
                          <ListboxOption
                            v-for="option in roleOptions"
                            :key="option.value ?? 'all'"
                            v-slot="{ active, selected }"
                            :value="option.value"
                            as="template"
                          >
                            <li
                              :class="[
                                active ? 'bg-aegis-100 text-aegis-900 dark:bg-aegis-900/30 dark:text-aegis-400' : 'text-gray-900 dark:text-gray-100',
                                'relative cursor-pointer select-none py-2 pl-10 pr-4'
                              ]"
                            >
                              <span :class="[selected ? 'font-medium' : 'font-normal', 'block truncate']">
                                {{ option.label }}
                              </span>
                              <span v-if="selected" class="absolute inset-y-0 left-0 flex items-center pl-3 text-aegis-600">
                                <CheckIcon class="h-5 w-5" />
                              </span>
                            </li>
                          </ListboxOption>
                        </ListboxOptions>
                      </transition>
                    </div>
                  </Listbox>

                  <!-- Create User Button -->
                  <button
                    class="inline-flex items-center gap-2 rounded-lg bg-aegis-600 px-4 py-2 text-sm font-medium text-white hover:bg-aegis-700"
                    @click="handleCreateUser"
                  >
                    <PlusIcon class="h-4 w-4" />
                    Add User
                  </button>
                </div>

                <!-- Bulk Actions -->
                <div
                  v-if="selectedUserIds.length > 0"
                  class="mt-4 flex items-center gap-4 rounded-lg bg-aegis-50 p-3 dark:bg-aegis-900/20"
                >
                  <span class="text-sm font-medium text-aegis-700 dark:text-aegis-400">
                    {{ selectedUserIds.length }} selected
                  </span>
                  <div class="flex gap-2">
                    <button
                      class="inline-flex items-center gap-1.5 rounded-lg bg-white px-3 py-1.5 text-sm font-medium text-gray-700 shadow-sm hover:bg-gray-50 dark:bg-gray-800 dark:text-gray-300 dark:hover:bg-gray-700"
                      @click="handleBulkUserAction('activate')"
                    >
                      <CheckIcon class="h-4 w-4 text-green-500" />
                      Activate
                    </button>
                    <button
                      class="inline-flex items-center gap-1.5 rounded-lg bg-white px-3 py-1.5 text-sm font-medium text-gray-700 shadow-sm hover:bg-gray-50 dark:bg-gray-800 dark:text-gray-300 dark:hover:bg-gray-700"
                      @click="handleBulkUserAction('deactivate')"
                    >
                      <ArchiveBoxIcon class="h-4 w-4 text-gray-500" />
                      Deactivate
                    </button>
                    <button
                      class="inline-flex items-center gap-1.5 rounded-lg bg-white px-3 py-1.5 text-sm font-medium text-red-600 shadow-sm hover:bg-red-50 dark:bg-gray-800 dark:hover:bg-red-900/20"
                      @click="handleBulkUserAction('delete')"
                    >
                      <TrashIcon class="h-4 w-4" />
                      Delete
                    </button>
                  </div>
                  <button
                    class="ml-auto text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400"
                    @click="selectedUserIds = []"
                  >
                    Clear selection
                  </button>
                </div>
              </div>

              <!-- User Table -->
              <UserTable
                :users="sortedUsers"
                :is-loading="store.isLoading"
                :sort-by="userSortBy"
                :sort-descending="userSortDescending"
                :selected-ids="selectedUserIds"
                @sort="handleUserSort"
                @select="handleSelectUser"
                @action="handleUserAction"
                @toggle-select="handleToggleSelectUser"
                @select-all="handleSelectAllUsers"
              />

              <!-- Pagination placeholder -->
              <div class="border-t p-4 dark:border-gray-700">
                <p class="text-sm text-gray-500 dark:text-gray-400">
                  Showing {{ store.users.length }} users
                </p>
              </div>
            </div>
          </TabPanel>

          <!-- Teams Tab -->
          <TabPanel>
            <div class="rounded-xl bg-white shadow dark:bg-gray-900">
              <!-- Filters -->
              <div class="border-b p-4 dark:border-gray-700">
                <div class="flex flex-wrap items-center gap-4">
                  <!-- Search -->
                  <div class="relative flex-1">
                    <MagnifyingGlassIcon class="absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-gray-400" />
                    <input
                      v-model="teamSearch"
                      type="text"
                      placeholder="Search teams..."
                      class="w-full rounded-lg border-gray-300 pl-10 focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-800 dark:text-white sm:text-sm"
                    />
                  </div>

                  <!-- Active Filter -->
                  <Listbox v-model="teamActiveFilter">
                    <div class="relative w-40">
                      <ListboxButton
                        class="relative w-full cursor-pointer rounded-lg border border-gray-300 bg-white py-2 pl-3 pr-10 text-left focus:border-aegis-500 focus:outline-none focus:ring-1 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-800 sm:text-sm"
                      >
                        <span class="block truncate text-gray-900 dark:text-white">
                          {{ teamActiveFilter === null ? 'All Teams' : teamActiveFilter ? 'Active' : 'Inactive' }}
                        </span>
                        <span class="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-2">
                          <ChevronUpDownIcon class="h-5 w-5 text-gray-400" />
                        </span>
                      </ListboxButton>
                      <transition
                        leave-active-class="transition duration-100 ease-in"
                        leave-from-class="opacity-100"
                        leave-to-class="opacity-0"
                      >
                        <ListboxOptions
                          class="absolute z-10 mt-1 w-full overflow-auto rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800 sm:text-sm"
                        >
                          <ListboxOption
                            v-for="option in [{ value: null, label: 'All Teams' }, { value: true, label: 'Active' }, { value: false, label: 'Inactive' }]"
                            :key="String(option.value)"
                            v-slot="{ active, selected }"
                            :value="option.value"
                            as="template"
                          >
                            <li
                              :class="[
                                active ? 'bg-aegis-100 text-aegis-900 dark:bg-aegis-900/30 dark:text-aegis-400' : 'text-gray-900 dark:text-gray-100',
                                'relative cursor-pointer select-none py-2 pl-10 pr-4'
                              ]"
                            >
                              <span :class="[selected ? 'font-medium' : 'font-normal', 'block truncate']">
                                {{ option.label }}
                              </span>
                              <span v-if="selected" class="absolute inset-y-0 left-0 flex items-center pl-3 text-aegis-600">
                                <CheckIcon class="h-5 w-5" />
                              </span>
                            </li>
                          </ListboxOption>
                        </ListboxOptions>
                      </transition>
                    </div>
                  </Listbox>

                  <!-- Create Team Button -->
                  <button
                    class="inline-flex items-center gap-2 rounded-lg bg-aegis-600 px-4 py-2 text-sm font-medium text-white hover:bg-aegis-700"
                    @click="handleCreateTeam"
                  >
                    <PlusIcon class="h-4 w-4" />
                    Add Team
                  </button>
                </div>
              </div>

              <!-- Team Table -->
              <TeamTable
                :teams="sortedTeams"
                :is-loading="store.isLoading"
                :sort-by="teamSortBy"
                :sort-descending="teamSortDescending"
                :selected-ids="selectedTeamIds"
                @sort="handleTeamSort"
                @select="handleSelectTeam"
                @action="handleTeamAction"
                @toggle-select="handleToggleSelectTeam"
                @select-all="handleSelectAllTeams"
              />

              <!-- Pagination placeholder -->
              <div class="border-t p-4 dark:border-gray-700">
                <p class="text-sm text-gray-500 dark:text-gray-400">
                  Showing {{ store.teams.length }} teams
                </p>
              </div>
            </div>
          </TabPanel>
        </TabPanels>
      </TabGroup>
    </div>

    <!-- User Detail Panel -->
    <UserDetailPanel
      :user="selectedUser"
      :is-open="showUserDetail"
      :teams="store.teams"
      @close="showUserDetail = false"
      @edit="handleEditUser"
      @action="handleUserAction"
    />

    <!-- User Form Dialog -->
    <UserFormDialog
      :is-open="showUserForm"
      :user="editingUser"
      :teams="store.teams"
      @close="showUserForm = false; editingUser = null"
      @save="handleSaveUser"
    />

    <!-- Team Detail Panel -->
    <TeamDetailPanel
      :team="selectedTeam"
      :is-open="showTeamDetail"
      :available-users="store.users"
      @close="showTeamDetail = false"
      @add-member="handleAddTeamMember"
      @remove-member="handleRemoveTeamMember"
    />

    <!-- Team Form Dialog -->
    <TeamFormDialog
      :is-open="showTeamForm"
      :team="editingTeam"
      @close="showTeamForm = false; editingTeam = null"
      @create="(data) => handleSaveTeam(data)"
      @update="(teamId, data) => handleSaveTeam(data, teamId)"
    />
  </div>
</template>
