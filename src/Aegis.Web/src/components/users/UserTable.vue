<script setup lang="ts">
import {
  CheckCircleIcon,
  XCircleIcon,
  LockClosedIcon,
  ClockIcon,
  ChevronUpIcon,
  ChevronDownIcon,
  ShieldCheckIcon
} from '@heroicons/vue/24/solid'
import { EllipsisVerticalIcon } from '@heroicons/vue/24/outline'
import { Menu, MenuButton, MenuItems, MenuItem } from '@headlessui/vue'
import type { ManagedUser, UserAction } from '@/types'
import { getRoleColor, formatUserStatus } from '@/types/userManagement'

defineProps<{
  users: ManagedUser[]
  isLoading?: boolean
  sortBy?: string
  sortDescending?: boolean
  selectedIds?: string[]
}>()

const emit = defineEmits<{
  sort: [field: string]
  select: [user: ManagedUser]
  action: [userId: string, action: UserAction]
  toggleSelect: [userId: string]
  selectAll: [selected: boolean]
}>()

function getRoleClasses(color: string): string {
  switch (color) {
    case 'gray':
      return 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300'
    case 'blue':
      return 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400'
    case 'green':
      return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400'
    case 'purple':
      return 'bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-400'
    case 'red':
      return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400'
    default:
      return 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300'
  }
}

function getStatusIcon(user: ManagedUser) {
  if (user.isLocked) return LockClosedIcon
  if (!user.emailVerified) return ClockIcon
  if (!user.isActive) return XCircleIcon
  return CheckCircleIcon
}

function getStatusColor(user: ManagedUser): string {
  if (user.isLocked) return 'text-red-500'
  if (!user.emailVerified) return 'text-yellow-500'
  if (!user.isActive) return 'text-gray-400'
  return 'text-green-500'
}

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
  { key: 'name', label: 'User', sortable: true },
  { key: 'role', label: 'Role', sortable: true },
  { key: 'team', label: 'Team', sortable: true },
  { key: 'status', label: 'Status', sortable: false },
  { key: 'lastLoginAt', label: 'Last Active', sortable: true },
  { key: 'actions', label: '', sortable: false }
]

const userActions: { action: UserAction; label: string; condition?: (user: ManagedUser) => boolean }[] = [
  { action: 'resetPassword', label: 'Reset Password' },
  { action: 'activate', label: 'Activate', condition: (u) => !u.isActive },
  { action: 'deactivate', label: 'Deactivate', condition: (u) => u.isActive },
  { action: 'lock', label: 'Lock Account', condition: (u) => !u.isLocked },
  { action: 'unlock', label: 'Unlock Account', condition: (u) => u.isLocked },
  { action: 'enableMfa', label: 'Enable MFA', condition: (u) => !u.mfaEnabled },
  { action: 'disableMfa', label: 'Disable MFA', condition: (u) => u.mfaEnabled },
  { action: 'resendVerification', label: 'Resend Verification', condition: (u) => !u.emailVerified },
  { action: 'delete', label: 'Delete User' }
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
                :checked="selectedIds && selectedIds.length === users.length && users.length > 0"
                :indeterminate="selectedIds && selectedIds.length > 0 && selectedIds.length < users.length"
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
          <tr v-else-if="users.length === 0">
            <td :colspan="columns.length + 1" class="px-4 py-12 text-center">
              <p class="text-sm text-gray-500 dark:text-gray-400">No users found</p>
            </td>
          </tr>

          <!-- Data rows -->
          <tr
            v-for="user in users"
            v-else
            :key="user.id"
            class="hover:bg-gray-50 dark:hover:bg-gray-800"
          >
            <!-- Checkbox -->
            <td class="w-12 px-4 py-3">
              <input
                type="checkbox"
                class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
                :checked="selectedIds?.includes(user.id)"
                @change="emit('toggleSelect', user.id)"
              />
            </td>

            <!-- User -->
            <td class="whitespace-nowrap px-4 py-3">
              <button
                class="flex items-center gap-3 text-left"
                @click="emit('select', user)"
              >
                <div
                  class="flex h-10 w-10 items-center justify-center rounded-full bg-aegis-100 text-aegis-700 dark:bg-aegis-900/30 dark:text-aegis-400"
                >
                  {{ user.name.charAt(0).toUpperCase() }}
                </div>
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-medium text-gray-900 dark:text-white">
                      {{ user.name }}
                    </span>
                    <ShieldCheckIcon
                      v-if="user.mfaEnabled"
                      class="h-4 w-4 text-green-500"
                      title="MFA Enabled"
                    />
                  </div>
                  <span class="text-sm text-gray-500 dark:text-gray-400">
                    {{ user.email }}
                  </span>
                </div>
              </button>
            </td>

            <!-- Role -->
            <td class="whitespace-nowrap px-4 py-3">
              <span
                class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium"
                :class="getRoleClasses(getRoleColor(user.role))"
              >
                {{ user.role }}
              </span>
            </td>

            <!-- Team -->
            <td class="whitespace-nowrap px-4 py-3 text-sm text-gray-500 dark:text-gray-400">
              {{ user.teamName || '-' }}
            </td>

            <!-- Status -->
            <td class="whitespace-nowrap px-4 py-3">
              <div class="flex items-center gap-1.5">
                <component
                  :is="getStatusIcon(user)"
                  class="h-4 w-4"
                  :class="getStatusColor(user)"
                />
                <span class="text-sm capitalize" :class="getStatusColor(user)">
                  {{ formatUserStatus(user) }}
                </span>
              </div>
            </td>

            <!-- Last Active -->
            <td class="whitespace-nowrap px-4 py-3 text-sm text-gray-500 dark:text-gray-400">
              {{ formatDate(user.lastLoginAt) }}
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
                      <template v-for="action in userActions" :key="action.action">
                        <MenuItem
                          v-if="!action.condition || action.condition(user)"
                          v-slot="{ active }"
                        >
                          <button
                            class="block w-full px-4 py-2 text-left text-sm"
                            :class="[
                              active ? 'bg-gray-100 dark:bg-gray-700' : '',
                              action.action === 'delete' ? 'text-red-600 dark:text-red-400' : 'text-gray-700 dark:text-gray-300'
                            ]"
                            @click="emit('action', user.id, action.action)"
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
