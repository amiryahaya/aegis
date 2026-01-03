<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import {
  Dialog,
  DialogPanel,
  DialogTitle,
  TransitionChild,
  TransitionRoot,
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
  XMarkIcon,
  UserGroupIcon,
  FolderIcon,
  ChartBarIcon,
  Cog6ToothIcon,
  UserPlusIcon,
  TrashIcon,
  ChevronUpDownIcon,
  CheckIcon
} from '@heroicons/vue/24/outline'
import type { ManagedTeam, TeamMemberRole, ManagedUser } from '@/types'
import { getTeamMemberRoleColor } from '@/types/userManagement'

const props = defineProps<{
  team: ManagedTeam | null
  isOpen: boolean
  availableUsers?: ManagedUser[]
}>()

const emit = defineEmits<{
  close: []
  update: [team: ManagedTeam]
  addMember: [teamId: string, userId: string, role: TeamMemberRole]
  removeMember: [teamId: string, memberId: string]
  updateMemberRole: [teamId: string, memberId: string, role: TeamMemberRole]
}>()

const selectedTab = ref(0)
const showAddMember = ref(false)
const selectedUserId = ref<string | null>(null)
const selectedRole = ref<TeamMemberRole>('member')

const memberRoles: { value: TeamMemberRole; label: string }[] = [
  { value: 'member', label: 'Member' },
  { value: 'moderator', label: 'Moderator' },
  { value: 'admin', label: 'Admin' }
]

const availableUsersForTeam = computed(() => {
  if (!props.availableUsers || !props.team?.members) return []
  const memberIds = props.team.members.map(m => m.userId)
  return props.availableUsers.filter(u => !memberIds.includes(u.id))
})

watch(() => props.isOpen, (open) => {
  if (!open) {
    selectedTab.value = 0
    showAddMember.value = false
    selectedUserId.value = null
    selectedRole.value = 'member'
  }
})

function handleAddMember() {
  if (props.team && selectedUserId.value) {
    emit('addMember', props.team.id, selectedUserId.value, selectedRole.value)
    showAddMember.value = false
    selectedUserId.value = null
    selectedRole.value = 'member'
  }
}

function handleRemoveMember(memberId: string) {
  if (props.team) {
    emit('removeMember', props.team.id, memberId)
  }
}

function formatDate(dateStr: string | undefined): string {
  if (!dateStr) return 'Never'
  return new Date(dateStr).toLocaleDateString()
}

function getRoleClasses(color: string): string {
  switch (color) {
    case 'purple':
      return 'bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-400'
    case 'red':
      return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400'
    case 'blue':
      return 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400'
    default:
      return 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300'
  }
}
</script>

<template>
  <TransitionRoot as="template" :show="isOpen">
    <Dialog as="div" class="relative z-50" @close="emit('close')">
      <TransitionChild
        as="template"
        enter="ease-out duration-300"
        enter-from="opacity-0"
        enter-to="opacity-100"
        leave="ease-in duration-200"
        leave-from="opacity-100"
        leave-to="opacity-0"
      >
        <div class="fixed inset-0 bg-black/50 backdrop-blur-sm" />
      </TransitionChild>

      <div class="fixed inset-0 overflow-y-auto">
        <div class="flex min-h-full items-center justify-center p-4">
          <TransitionChild
            as="template"
            enter="ease-out duration-300"
            enter-from="opacity-0 scale-95"
            enter-to="opacity-100 scale-100"
            leave="ease-in duration-200"
            leave-from="opacity-100 scale-100"
            leave-to="opacity-0 scale-95"
          >
            <DialogPanel
              class="w-full max-w-3xl transform overflow-hidden rounded-2xl bg-white p-6 shadow-xl transition-all dark:bg-gray-900"
            >
              <!-- Header -->
              <div class="mb-6 flex items-start justify-between">
                <div class="flex items-center gap-4">
                  <div
                    class="flex h-14 w-14 items-center justify-center rounded-xl bg-aegis-100 dark:bg-aegis-900/30"
                  >
                    <UserGroupIcon class="h-7 w-7 text-aegis-600 dark:text-aegis-400" />
                  </div>
                  <div>
                    <DialogTitle class="text-xl font-semibold text-gray-900 dark:text-white">
                      {{ team?.name }}
                    </DialogTitle>
                    <p v-if="team?.description" class="mt-1 text-sm text-gray-500 dark:text-gray-400">
                      {{ team.description }}
                    </p>
                  </div>
                </div>
                <button
                  class="rounded-lg p-2 text-gray-400 hover:bg-gray-100 hover:text-gray-600 dark:hover:bg-gray-800"
                  @click="emit('close')"
                >
                  <XMarkIcon class="h-5 w-5" />
                </button>
              </div>

              <!-- Stats -->
              <div class="mb-6 grid grid-cols-4 gap-4">
                <div class="rounded-lg bg-gray-50 p-4 dark:bg-gray-800">
                  <div class="flex items-center gap-2">
                    <UserGroupIcon class="h-5 w-5 text-blue-500" />
                    <span class="text-sm text-gray-500 dark:text-gray-400">Members</span>
                  </div>
                  <p class="mt-1 text-2xl font-semibold text-gray-900 dark:text-white">
                    {{ team?.memberCount || 0 }}
                    <span class="text-sm font-normal text-gray-400">/ {{ team?.settings.maxMembers }}</span>
                  </p>
                </div>
                <div class="rounded-lg bg-gray-50 p-4 dark:bg-gray-800">
                  <div class="flex items-center gap-2">
                    <FolderIcon class="h-5 w-5 text-green-500" />
                    <span class="text-sm text-gray-500 dark:text-gray-400">Workspaces</span>
                  </div>
                  <p class="mt-1 text-2xl font-semibold text-gray-900 dark:text-white">
                    {{ team?.workspaceCount || 0 }}
                    <span class="text-sm font-normal text-gray-400">/ {{ team?.settings.workspaceLimit }}</span>
                  </p>
                </div>
                <div class="rounded-lg bg-gray-50 p-4 dark:bg-gray-800">
                  <div class="flex items-center gap-2">
                    <ChartBarIcon class="h-5 w-5 text-purple-500" />
                    <span class="text-sm text-gray-500 dark:text-gray-400">Query Rate</span>
                  </div>
                  <p class="mt-1 text-2xl font-semibold text-gray-900 dark:text-white">
                    {{ team?.settings.queryRateLimit || 0 }}
                    <span class="text-sm font-normal text-gray-400">/min</span>
                  </p>
                </div>
                <div class="rounded-lg bg-gray-50 p-4 dark:bg-gray-800">
                  <div class="flex items-center gap-2">
                    <Cog6ToothIcon class="h-5 w-5 text-orange-500" />
                    <span class="text-sm text-gray-500 dark:text-gray-400">Storage</span>
                  </div>
                  <p class="mt-1 text-2xl font-semibold text-gray-900 dark:text-white">
                    {{ team?.settings.storageQuotaGb || 0 }}
                    <span class="text-sm font-normal text-gray-400">GB</span>
                  </p>
                </div>
              </div>

              <!-- Tabs -->
              <TabGroup :selected-index="selectedTab" @change="selectedTab = $event">
                <TabList class="flex space-x-1 rounded-xl bg-gray-100 p-1 dark:bg-gray-800">
                  <Tab
                    v-slot="{ selected }"
                    as="template"
                  >
                    <button
                      :class="[
                        'w-full rounded-lg py-2.5 text-sm font-medium leading-5',
                        'focus:outline-none focus:ring-2 focus:ring-aegis-500 focus:ring-offset-2 dark:focus:ring-offset-gray-900',
                        selected
                          ? 'bg-white text-aegis-700 shadow dark:bg-gray-700 dark:text-aegis-400'
                          : 'text-gray-500 hover:bg-white/50 hover:text-gray-700 dark:text-gray-400 dark:hover:bg-gray-700/50'
                      ]"
                    >
                      Members
                    </button>
                  </Tab>
                  <Tab
                    v-slot="{ selected }"
                    as="template"
                  >
                    <button
                      :class="[
                        'w-full rounded-lg py-2.5 text-sm font-medium leading-5',
                        'focus:outline-none focus:ring-2 focus:ring-aegis-500 focus:ring-offset-2 dark:focus:ring-offset-gray-900',
                        selected
                          ? 'bg-white text-aegis-700 shadow dark:bg-gray-700 dark:text-aegis-400'
                          : 'text-gray-500 hover:bg-white/50 hover:text-gray-700 dark:text-gray-400 dark:hover:bg-gray-700/50'
                      ]"
                    >
                      Settings
                    </button>
                  </Tab>
                </TabList>

                <TabPanels class="mt-4">
                  <!-- Members Tab -->
                  <TabPanel class="space-y-4">
                    <!-- Add Member -->
                    <div class="flex items-center justify-between">
                      <h3 class="text-sm font-medium text-gray-900 dark:text-white">
                        Team Members ({{ team?.members?.length || 0 }})
                      </h3>
                      <button
                        v-if="!showAddMember"
                        class="inline-flex items-center gap-1.5 rounded-lg bg-aegis-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-aegis-700"
                        @click="showAddMember = true"
                      >
                        <UserPlusIcon class="h-4 w-4" />
                        Add Member
                      </button>
                    </div>

                    <!-- Add Member Form -->
                    <div v-if="showAddMember" class="flex items-end gap-3 rounded-lg bg-gray-50 p-4 dark:bg-gray-800">
                      <div class="flex-1">
                        <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">User</label>
                        <Listbox v-model="selectedUserId">
                          <div class="relative mt-1">
                            <ListboxButton
                              class="relative w-full cursor-pointer rounded-lg border border-gray-300 bg-white py-2 pl-3 pr-10 text-left focus:border-aegis-500 focus:outline-none focus:ring-1 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 sm:text-sm"
                            >
                              <span v-if="selectedUserId" class="block truncate">
                                {{ availableUsersForTeam.find(u => u.id === selectedUserId)?.name }}
                              </span>
                              <span v-else class="block truncate text-gray-400">Select a user</span>
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
                                class="absolute z-10 mt-1 max-h-60 w-full overflow-auto rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-700 sm:text-sm"
                              >
                                <ListboxOption
                                  v-for="user in availableUsersForTeam"
                                  :key="user.id"
                                  v-slot="{ active, selected }"
                                  :value="user.id"
                                  as="template"
                                >
                                  <li
                                    :class="[
                                      active ? 'bg-aegis-100 text-aegis-900 dark:bg-aegis-900/30 dark:text-aegis-400' : 'text-gray-900 dark:text-gray-100',
                                      'relative cursor-pointer select-none py-2 pl-10 pr-4'
                                    ]"
                                  >
                                    <span :class="[selected ? 'font-medium' : 'font-normal', 'block truncate']">
                                      {{ user.name }}
                                      <span class="text-gray-500 dark:text-gray-400">{{ user.email }}</span>
                                    </span>
                                    <span
                                      v-if="selected"
                                      class="absolute inset-y-0 left-0 flex items-center pl-3 text-aegis-600"
                                    >
                                      <CheckIcon class="h-5 w-5" />
                                    </span>
                                  </li>
                                </ListboxOption>
                              </ListboxOptions>
                            </transition>
                          </div>
                        </Listbox>
                      </div>
                      <div class="w-36">
                        <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">Role</label>
                        <Listbox v-model="selectedRole">
                          <div class="relative mt-1">
                            <ListboxButton
                              class="relative w-full cursor-pointer rounded-lg border border-gray-300 bg-white py-2 pl-3 pr-10 text-left focus:border-aegis-500 focus:outline-none focus:ring-1 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 sm:text-sm"
                            >
                              <span class="block truncate capitalize">{{ selectedRole }}</span>
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
                                class="absolute z-10 mt-1 w-full overflow-auto rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-700 sm:text-sm"
                              >
                                <ListboxOption
                                  v-for="role in memberRoles"
                                  :key="role.value"
                                  v-slot="{ active, selected }"
                                  :value="role.value"
                                  as="template"
                                >
                                  <li
                                    :class="[
                                      active ? 'bg-aegis-100 text-aegis-900 dark:bg-aegis-900/30 dark:text-aegis-400' : 'text-gray-900 dark:text-gray-100',
                                      'relative cursor-pointer select-none py-2 pl-10 pr-4'
                                    ]"
                                  >
                                    <span :class="[selected ? 'font-medium' : 'font-normal', 'block truncate']">
                                      {{ role.label }}
                                    </span>
                                    <span
                                      v-if="selected"
                                      class="absolute inset-y-0 left-0 flex items-center pl-3 text-aegis-600"
                                    >
                                      <CheckIcon class="h-5 w-5" />
                                    </span>
                                  </li>
                                </ListboxOption>
                              </ListboxOptions>
                            </transition>
                          </div>
                        </Listbox>
                      </div>
                      <button
                        class="rounded-lg bg-aegis-600 px-4 py-2 text-sm font-medium text-white hover:bg-aegis-700 disabled:opacity-50"
                        :disabled="!selectedUserId"
                        @click="handleAddMember"
                      >
                        Add
                      </button>
                      <button
                        class="rounded-lg border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-600 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
                        @click="showAddMember = false"
                      >
                        Cancel
                      </button>
                    </div>

                    <!-- Members List -->
                    <div class="max-h-80 overflow-y-auto rounded-lg border dark:border-gray-700">
                      <div
                        v-for="member in team?.members"
                        :key="member.id"
                        class="flex items-center justify-between border-b p-4 last:border-b-0 dark:border-gray-700"
                      >
                        <div class="flex items-center gap-3">
                          <div
                            v-if="member.avatarUrl"
                            class="h-10 w-10 rounded-full bg-cover bg-center"
                            :style="{ backgroundImage: `url(${member.avatarUrl})` }"
                          />
                          <div
                            v-else
                            class="flex h-10 w-10 items-center justify-center rounded-full bg-aegis-100 text-aegis-700 dark:bg-aegis-900/30 dark:text-aegis-400"
                          >
                            {{ member.userName.charAt(0).toUpperCase() }}
                          </div>
                          <div>
                            <div class="flex items-center gap-2">
                              <span class="font-medium text-gray-900 dark:text-white">
                                {{ member.userName }}
                              </span>
                              <span
                                v-if="member.isOwner"
                                class="rounded-full bg-purple-100 px-2 py-0.5 text-xs font-medium text-purple-800 dark:bg-purple-900/30 dark:text-purple-400"
                              >
                                Owner
                              </span>
                            </div>
                            <span class="text-sm text-gray-500 dark:text-gray-400">
                              {{ member.userEmail }}
                            </span>
                          </div>
                        </div>
                        <div class="flex items-center gap-3">
                          <span class="text-xs text-gray-400">
                            Joined {{ formatDate(member.joinedAt) }}
                          </span>
                          <span
                            v-if="!member.isOwner"
                            :class="[
                              'rounded-full px-2.5 py-0.5 text-xs font-medium capitalize',
                              getRoleClasses(getTeamMemberRoleColor(member.role))
                            ]"
                          >
                            {{ member.role }}
                          </span>
                          <button
                            v-if="!member.isOwner"
                            class="rounded-lg p-1.5 text-gray-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-900/20"
                            title="Remove member"
                            @click="handleRemoveMember(member.id)"
                          >
                            <TrashIcon class="h-4 w-4" />
                          </button>
                        </div>
                      </div>
                      <div v-if="!team?.members?.length" class="p-8 text-center text-gray-500 dark:text-gray-400">
                        No members in this team
                      </div>
                    </div>
                  </TabPanel>

                  <!-- Settings Tab -->
                  <TabPanel class="space-y-4">
                    <div class="rounded-lg border p-4 dark:border-gray-700">
                      <h3 class="mb-4 text-sm font-medium text-gray-900 dark:text-white">Team Settings</h3>
                      <dl class="grid grid-cols-2 gap-4">
                        <div>
                          <dt class="text-sm text-gray-500 dark:text-gray-400">Allow Member Invites</dt>
                          <dd class="mt-1 text-sm font-medium text-gray-900 dark:text-white">
                            {{ team?.settings.allowMemberInvites ? 'Yes' : 'No' }}
                          </dd>
                        </div>
                        <div>
                          <dt class="text-sm text-gray-500 dark:text-gray-400">Default Member Role</dt>
                          <dd class="mt-1 text-sm font-medium text-gray-900 dark:text-white capitalize">
                            {{ team?.settings.defaultMemberRole }}
                          </dd>
                        </div>
                        <div>
                          <dt class="text-sm text-gray-500 dark:text-gray-400">Max Members</dt>
                          <dd class="mt-1 text-sm font-medium text-gray-900 dark:text-white">
                            {{ team?.settings.maxMembers }}
                          </dd>
                        </div>
                        <div>
                          <dt class="text-sm text-gray-500 dark:text-gray-400">Workspace Limit</dt>
                          <dd class="mt-1 text-sm font-medium text-gray-900 dark:text-white">
                            {{ team?.settings.workspaceLimit }}
                          </dd>
                        </div>
                        <div>
                          <dt class="text-sm text-gray-500 dark:text-gray-400">Query Rate Limit</dt>
                          <dd class="mt-1 text-sm font-medium text-gray-900 dark:text-white">
                            {{ team?.settings.queryRateLimit }}/min
                          </dd>
                        </div>
                        <div>
                          <dt class="text-sm text-gray-500 dark:text-gray-400">Storage Quota</dt>
                          <dd class="mt-1 text-sm font-medium text-gray-900 dark:text-white">
                            {{ team?.settings.storageQuotaGb }} GB
                          </dd>
                        </div>
                      </dl>
                    </div>

                    <div class="rounded-lg border p-4 dark:border-gray-700">
                      <h3 class="mb-4 text-sm font-medium text-gray-900 dark:text-white">Team Information</h3>
                      <dl class="grid grid-cols-2 gap-4">
                        <div>
                          <dt class="text-sm text-gray-500 dark:text-gray-400">Created</dt>
                          <dd class="mt-1 text-sm font-medium text-gray-900 dark:text-white">
                            {{ formatDate(team?.createdAt) }}
                          </dd>
                        </div>
                        <div>
                          <dt class="text-sm text-gray-500 dark:text-gray-400">Last Updated</dt>
                          <dd class="mt-1 text-sm font-medium text-gray-900 dark:text-white">
                            {{ formatDate(team?.updatedAt) }}
                          </dd>
                        </div>
                        <div>
                          <dt class="text-sm text-gray-500 dark:text-gray-400">Owner</dt>
                          <dd class="mt-1 text-sm font-medium text-gray-900 dark:text-white">
                            {{ team?.ownerName }}
                          </dd>
                        </div>
                        <div>
                          <dt class="text-sm text-gray-500 dark:text-gray-400">Status</dt>
                          <dd class="mt-1">
                            <span
                              :class="[
                                'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium',
                                team?.isActive
                                  ? 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400'
                                  : 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300'
                              ]"
                            >
                              {{ team?.isActive ? 'Active' : 'Inactive' }}
                            </span>
                          </dd>
                        </div>
                      </dl>
                    </div>
                  </TabPanel>
                </TabPanels>
              </TabGroup>
            </DialogPanel>
          </TransitionChild>
        </div>
      </div>
    </Dialog>
  </TransitionRoot>
</template>
