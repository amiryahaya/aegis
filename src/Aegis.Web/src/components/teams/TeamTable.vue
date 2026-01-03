<script setup lang="ts">
import {
  CheckCircleIcon,
  XCircleIcon,
  ChevronUpIcon,
  ChevronDownIcon,
  UserGroupIcon
} from '@heroicons/vue/24/solid'
import { EllipsisVerticalIcon } from '@heroicons/vue/24/outline'
import { Menu, MenuButton, MenuItems, MenuItem } from '@headlessui/vue'
import type { ManagedTeam } from '@/types'

defineProps<{
  teams: ManagedTeam[]
  isLoading?: boolean
  sortBy?: string
  sortDescending?: boolean
  selectedIds?: string[]
}>()

const emit = defineEmits<{
  sort: [field: string]
  select: [team: ManagedTeam]
  action: [teamId: string, action: TeamAction]
  toggleSelect: [teamId: string]
  selectAll: [selected: boolean]
}>()

type TeamAction = 'edit' | 'activate' | 'deactivate' | 'manageMembers' | 'delete'

function formatDate(dateStr: string | undefined): string {
  if (!dateStr) return 'Never'
  const date = new Date(dateStr)
  const now = new Date()
  const diffMs = now.getTime() - date.getTime()
  const diffDays = Math.floor(diffMs / (24 * 60 * 60 * 1000))

  if (diffDays === 0) return 'Today'
  if (diffDays === 1) return 'Yesterday'
  if (diffDays < 7) return `${diffDays}d ago`
  if (diffDays < 30) return `${Math.floor(diffDays / 7)}w ago`
  return date.toLocaleDateString()
}

const columns = [
  { key: 'name', label: 'Team', sortable: true },
  { key: 'memberCount', label: 'Members', sortable: true },
  { key: 'workspaceCount', label: 'Workspaces', sortable: true },
  { key: 'ownerName', label: 'Owner', sortable: true },
  { key: 'status', label: 'Status', sortable: false },
  { key: 'createdAt', label: 'Created', sortable: true },
  { key: 'actions', label: '', sortable: false }
]

const teamActions: { action: TeamAction; label: string; condition?: (team: ManagedTeam) => boolean }[] = [
  { action: 'edit', label: 'Edit Team' },
  { action: 'manageMembers', label: 'Manage Members' },
  { action: 'activate', label: 'Activate', condition: (t) => !t.isActive },
  { action: 'deactivate', label: 'Deactivate', condition: (t) => t.isActive },
  { action: 'delete', label: 'Delete Team' }
]
</script>

<template>
  <div class="overflow-hidden rounded-lg border dark:border-gray-700">
    <div class="overflow-x-auto">
      <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
        <thead class="bg-gray-50 dark:bg-gray-800">
          <tr>
            <!-- Checkbox -->
            <th scope="col" class="w-12 px-4 py-3">
              <input
                type="checkbox"
                class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
                :checked="selectedIds && selectedIds.length === teams.length && teams.length > 0"
                :indeterminate="selectedIds && selectedIds.length > 0 && selectedIds.length < teams.length"
                @change="emit('selectAll', ($event.target as HTMLInputElement).checked)"
              />
            </th>
            <th
              v-for="col in columns"
              :key="col.key"
              scope="col"
              class="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400"
              :class="{ 'cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-700': col.sortable }"
              @click="col.sortable && emit('sort', col.key)"
            >
              <div class="flex items-center gap-1">
                {{ col.label }}
                <template v-if="col.sortable && sortBy === col.key">
                  <ChevronUpIcon v-if="!sortDescending" class="h-4 w-4" />
                  <ChevronDownIcon v-else class="h-4 w-4" />
                </template>
              </div>
            </th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-200 bg-white dark:divide-gray-700 dark:bg-gray-900">
          <!-- Loading state -->
          <template v-if="isLoading">
            <tr v-for="i in 10" :key="i">
              <td :colspan="columns.length + 1" class="px-4 py-3">
                <div class="h-10 animate-pulse rounded bg-gray-200 dark:bg-gray-700" />
              </td>
            </tr>
          </template>

          <!-- Empty state -->
          <tr v-else-if="teams.length === 0">
            <td :colspan="columns.length + 1" class="px-4 py-12 text-center">
              <UserGroupIcon class="mx-auto h-12 w-12 text-gray-400" />
              <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">No teams found</p>
            </td>
          </tr>

          <!-- Data rows -->
          <tr
            v-for="team in teams"
            v-else
            :key="team.id"
            class="hover:bg-gray-50 dark:hover:bg-gray-800"
          >
            <!-- Checkbox -->
            <td class="w-12 px-4 py-3">
              <input
                type="checkbox"
                class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
                :checked="selectedIds?.includes(team.id)"
                @change="emit('toggleSelect', team.id)"
              />
            </td>

            <!-- Team -->
            <td class="whitespace-nowrap px-4 py-3">
              <button
                class="flex items-center gap-3 text-left"
                @click="emit('select', team)"
              >
                <div
                  class="flex h-10 w-10 items-center justify-center rounded-lg bg-aegis-100 text-aegis-700 dark:bg-aegis-900/30 dark:text-aegis-400"
                >
                  <UserGroupIcon class="h-5 w-5" />
                </div>
                <div>
                  <span class="font-medium text-gray-900 dark:text-white">
                    {{ team.name }}
                  </span>
                  <p v-if="team.description" class="text-sm text-gray-500 dark:text-gray-400 truncate max-w-xs">
                    {{ team.description }}
                  </p>
                </div>
              </button>
            </td>

            <!-- Members -->
            <td class="whitespace-nowrap px-4 py-3">
              <div class="flex items-center gap-1.5">
                <UserGroupIcon class="h-4 w-4 text-gray-400" />
                <span class="text-sm text-gray-600 dark:text-gray-300">
                  {{ team.memberCount }}
                </span>
                <span class="text-xs text-gray-400">
                  / {{ team.settings.maxMembers }}
                </span>
              </div>
            </td>

            <!-- Workspaces -->
            <td class="whitespace-nowrap px-4 py-3 text-sm text-gray-600 dark:text-gray-300">
              {{ team.workspaceCount }}
              <span class="text-xs text-gray-400">
                / {{ team.settings.workspaceLimit }}
              </span>
            </td>

            <!-- Owner -->
            <td class="whitespace-nowrap px-4 py-3 text-sm text-gray-500 dark:text-gray-400">
              {{ team.ownerName }}
            </td>

            <!-- Status -->
            <td class="whitespace-nowrap px-4 py-3">
              <div class="flex items-center gap-1.5">
                <component
                  :is="team.isActive ? CheckCircleIcon : XCircleIcon"
                  class="h-4 w-4"
                  :class="team.isActive ? 'text-green-500' : 'text-gray-400'"
                />
                <span
                  class="text-sm capitalize"
                  :class="team.isActive ? 'text-green-600 dark:text-green-400' : 'text-gray-500 dark:text-gray-400'"
                >
                  {{ team.isActive ? 'Active' : 'Inactive' }}
                </span>
              </div>
            </td>

            <!-- Created -->
            <td class="whitespace-nowrap px-4 py-3 text-sm text-gray-500 dark:text-gray-400">
              {{ formatDate(team.createdAt) }}
            </td>

            <!-- Actions -->
            <td class="whitespace-nowrap px-4 py-3 text-right">
              <Menu as="div" class="relative inline-block text-left">
                <MenuButton
                  class="rounded-lg p-1.5 text-gray-400 hover:bg-gray-100 hover:text-gray-600 dark:hover:bg-gray-700"
                >
                  <EllipsisVerticalIcon class="h-5 w-5" />
                </MenuButton>
                <transition
                  enter-active-class="transition duration-100 ease-out"
                  enter-from-class="transform scale-95 opacity-0"
                  enter-to-class="transform scale-100 opacity-100"
                  leave-active-class="transition duration-75 ease-in"
                  leave-from-class="transform scale-100 opacity-100"
                  leave-to-class="transform scale-95 opacity-0"
                >
                  <MenuItems
                    class="absolute right-0 z-10 mt-2 w-48 origin-top-right rounded-lg bg-white shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800 dark:ring-gray-700"
                  >
                    <div class="py-1">
                      <template v-for="action in teamActions" :key="action.action">
                        <MenuItem
                          v-if="!action.condition || action.condition(team)"
                          v-slot="{ active }"
                        >
                          <button
                            class="block w-full px-4 py-2 text-left text-sm"
                            :class="[
                              active ? 'bg-gray-100 dark:bg-gray-700' : '',
                              action.action === 'delete' ? 'text-red-600 dark:text-red-400' : 'text-gray-700 dark:text-gray-300'
                            ]"
                            @click="emit('action', team.id, action.action)"
                          >
                            {{ action.label }}
                          </button>
                        </MenuItem>
                      </template>
                    </div>
                  </MenuItems>
                </transition>
              </Menu>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
