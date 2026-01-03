<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import {
  Dialog,
  DialogPanel,
  DialogTitle,
  TransitionRoot,
  TransitionChild,
  Listbox,
  ListboxButton,
  ListboxOptions,
  ListboxOption,
  Switch
} from '@headlessui/vue'
import { XMarkIcon, ChevronUpDownIcon, CheckIcon } from '@heroicons/vue/24/outline'
import { UserRole } from '@/types/user'
import type { ManagedUser, ManagedTeam, CreateManagedUserRequest, UpdateManagedUserRequest } from '@/types'
import { ROLE_DEFINITIONS, getRoleColor } from '@/types/userManagement'

const props = defineProps<{
  isOpen: boolean
  user?: ManagedUser | null
  teams: ManagedTeam[]
}>()

const emit = defineEmits<{
  close: []
  save: [data: CreateManagedUserRequest | UpdateManagedUserRequest]
}>()

const isEditMode = computed(() => !!props.user)

// Form state
const name = ref('')
const email = ref('')
const password = ref('')
const confirmPassword = ref('')
const role = ref<UserRole>(UserRole.Contributor)
const teamId = ref<string | null>(null)
const sendWelcomeEmail = ref(true)
const requirePasswordChange = ref(true)
const isActive = ref(true)

// Validation
const errors = ref<Record<string, string>>({})

watch(() => props.user, (user) => {
  if (user) {
    name.value = user.name
    email.value = user.email
    role.value = user.role
    teamId.value = user.teamId || null
    isActive.value = user.isActive
  } else {
    resetForm()
  }
}, { immediate: true })

watch(() => props.isOpen, (open) => {
  if (open && !props.user) {
    resetForm()
  }
})

function resetForm() {
  name.value = ''
  email.value = ''
  password.value = ''
  confirmPassword.value = ''
  role.value = UserRole.Contributor
  teamId.value = null
  sendWelcomeEmail.value = true
  requirePasswordChange.value = true
  isActive.value = true
  errors.value = {}
}

function validate(): boolean {
  errors.value = {}

  if (!name.value.trim()) {
    errors.value.name = 'Name is required'
  }

  if (!email.value.trim()) {
    errors.value.email = 'Email is required'
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value)) {
    errors.value.email = 'Invalid email format'
  }

  if (!isEditMode.value) {
    if (!password.value) {
      errors.value.password = 'Password is required'
    } else if (password.value.length < 8) {
      errors.value.password = 'Password must be at least 8 characters'
    }

    if (password.value !== confirmPassword.value) {
      errors.value.confirmPassword = 'Passwords do not match'
    }
  }

  return Object.keys(errors.value).length === 0
}

function handleSubmit() {
  if (!validate()) return

  if (isEditMode.value) {
    const updateData: UpdateManagedUserRequest = {
      name: name.value,
      role: role.value,
      teamId: teamId.value,
      isActive: isActive.value
    }
    emit('save', updateData)
  } else {
    const createData: CreateManagedUserRequest = {
      name: name.value,
      email: email.value,
      password: password.value,
      role: role.value,
      teamId: teamId.value || undefined,
      sendWelcomeEmail: sendWelcomeEmail.value,
      requirePasswordChange: requirePasswordChange.value
    }
    emit('save', createData)
  }
}

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

const selectedTeam = computed(() => props.teams.find(t => t.id === teamId.value))
</script>

<template>
  <TransitionRoot appear :show="isOpen" as="template">
    <Dialog as="div" class="relative z-50" @close="emit('close')">
      <TransitionChild
        as="template"
        enter="duration-300 ease-out"
        enter-from="opacity-0"
        enter-to="opacity-100"
        leave="duration-200 ease-in"
        leave-from="opacity-100"
        leave-to="opacity-0"
      >
        <div class="fixed inset-0 bg-black/25 dark:bg-black/50" />
      </TransitionChild>

      <div class="fixed inset-0 overflow-y-auto">
        <div class="flex min-h-full items-center justify-center p-4">
          <TransitionChild
            as="template"
            enter="duration-300 ease-out"
            enter-from="opacity-0 scale-95"
            enter-to="opacity-100 scale-100"
            leave="duration-200 ease-in"
            leave-from="opacity-100 scale-100"
            leave-to="opacity-0 scale-95"
          >
            <DialogPanel
              class="w-full max-w-lg transform overflow-hidden rounded-xl bg-white shadow-xl transition-all dark:bg-gray-800"
            >
              <!-- Header -->
              <div class="flex items-center justify-between border-b px-6 py-4 dark:border-gray-700">
                <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-white">
                  {{ isEditMode ? 'Edit User' : 'Create User' }}
                </DialogTitle>
                <button
                  type="button"
                  class="rounded-lg p-1.5 text-gray-400 hover:bg-gray-100 hover:text-gray-500 dark:hover:bg-gray-700"
                  @click="emit('close')"
                >
                  <XMarkIcon class="h-5 w-5" />
                </button>
              </div>

              <!-- Form -->
              <form class="p-6" @submit.prevent="handleSubmit">
                <div class="space-y-4">
                  <!-- Name -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                      Full Name
                    </label>
                    <input
                      v-model="name"
                      type="text"
                      class="mt-1 block w-full rounded-lg border-gray-300 shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                      :class="{ 'border-red-500': errors.name }"
                    />
                    <p v-if="errors.name" class="mt-1 text-sm text-red-600">{{ errors.name }}</p>
                  </div>

                  <!-- Email -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                      Email Address
                    </label>
                    <input
                      v-model="email"
                      type="email"
                      class="mt-1 block w-full rounded-lg border-gray-300 shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                      :class="{ 'border-red-500': errors.email }"
                    />
                    <p v-if="errors.email" class="mt-1 text-sm text-red-600">{{ errors.email }}</p>
                  </div>

                  <!-- Password (only for create) -->
                  <template v-if="!isEditMode">
                    <div>
                      <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                        Password
                      </label>
                      <input
                        v-model="password"
                        type="password"
                        class="mt-1 block w-full rounded-lg border-gray-300 shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                        :class="{ 'border-red-500': errors.password }"
                      />
                      <p v-if="errors.password" class="mt-1 text-sm text-red-600">{{ errors.password }}</p>
                    </div>

                    <div>
                      <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                        Confirm Password
                      </label>
                      <input
                        v-model="confirmPassword"
                        type="password"
                        class="mt-1 block w-full rounded-lg border-gray-300 shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                        :class="{ 'border-red-500': errors.confirmPassword }"
                      />
                      <p v-if="errors.confirmPassword" class="mt-1 text-sm text-red-600">{{ errors.confirmPassword }}</p>
                    </div>
                  </template>

                  <!-- Role -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                      Role
                    </label>
                    <Listbox v-model="role">
                      <div class="relative mt-1">
                        <ListboxButton
                          class="relative w-full cursor-pointer rounded-lg border border-gray-300 bg-white py-2 pl-3 pr-10 text-left shadow-sm focus:border-aegis-500 focus:outline-none focus:ring-1 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                        >
                          <span class="flex items-center gap-2">
                            <span
                              class="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium"
                              :class="getRoleClasses(getRoleColor(role))"
                            >
                              {{ role }}
                            </span>
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
                            class="absolute z-10 mt-1 max-h-60 w-full overflow-auto rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800"
                          >
                            <ListboxOption
                              v-for="roleDef in ROLE_DEFINITIONS"
                              :key="roleDef.role"
                              v-slot="{ active, selected }"
                              :value="roleDef.role"
                            >
                              <li
                                class="relative cursor-pointer select-none py-2 pl-10 pr-4"
                                :class="[active ? 'bg-aegis-100 dark:bg-aegis-900/30' : '']"
                              >
                                <div class="flex flex-col">
                                  <span
                                    class="inline-flex w-fit items-center rounded-full px-2 py-0.5 text-xs font-medium"
                                    :class="getRoleClasses(roleDef.color)"
                                  >
                                    {{ roleDef.label }}
                                  </span>
                                  <span class="mt-1 text-xs text-gray-500 dark:text-gray-400">
                                    {{ roleDef.description }}
                                  </span>
                                </div>
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

                  <!-- Team -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                      Team (Optional)
                    </label>
                    <Listbox v-model="teamId">
                      <div class="relative mt-1">
                        <ListboxButton
                          class="relative w-full cursor-pointer rounded-lg border border-gray-300 bg-white py-2 pl-3 pr-10 text-left shadow-sm focus:border-aegis-500 focus:outline-none focus:ring-1 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                        >
                          <span class="block truncate">
                            {{ selectedTeam?.name || 'No team' }}
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
                            class="absolute z-10 mt-1 max-h-60 w-full overflow-auto rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800"
                          >
                            <ListboxOption v-slot="{ active }" :value="null">
                              <li
                                class="relative cursor-pointer select-none py-2 pl-10 pr-4 text-sm"
                                :class="[active ? 'bg-aegis-100 dark:bg-aegis-900/30' : '', 'text-gray-500 dark:text-gray-400']"
                              >
                                No team
                              </li>
                            </ListboxOption>
                            <ListboxOption
                              v-for="team in teams"
                              :key="team.id"
                              v-slot="{ active, selected }"
                              :value="team.id"
                            >
                              <li
                                class="relative cursor-pointer select-none py-2 pl-10 pr-4 text-sm"
                                :class="[
                                  active ? 'bg-aegis-100 dark:bg-aegis-900/30' : '',
                                  'text-gray-900 dark:text-white'
                                ]"
                              >
                                <span :class="['block truncate', selected ? 'font-medium' : 'font-normal']">
                                  {{ team.name }}
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

                  <!-- Options (only for create) -->
                  <template v-if="!isEditMode">
                    <div class="space-y-3 rounded-lg border p-4 dark:border-gray-700">
                      <div class="flex items-center justify-between">
                        <div>
                          <p class="text-sm font-medium text-gray-700 dark:text-gray-300">Send welcome email</p>
                          <p class="text-xs text-gray-500 dark:text-gray-400">User will receive an email with login instructions</p>
                        </div>
                        <Switch
                          v-model="sendWelcomeEmail"
                          :class="sendWelcomeEmail ? 'bg-aegis-600' : 'bg-gray-200 dark:bg-gray-700'"
                          class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors"
                        >
                          <span
                            :class="sendWelcomeEmail ? 'translate-x-6' : 'translate-x-1'"
                            class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                          />
                        </Switch>
                      </div>
                      <div class="flex items-center justify-between">
                        <div>
                          <p class="text-sm font-medium text-gray-700 dark:text-gray-300">Require password change</p>
                          <p class="text-xs text-gray-500 dark:text-gray-400">User must change password on first login</p>
                        </div>
                        <Switch
                          v-model="requirePasswordChange"
                          :class="requirePasswordChange ? 'bg-aegis-600' : 'bg-gray-200 dark:bg-gray-700'"
                          class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors"
                        >
                          <span
                            :class="requirePasswordChange ? 'translate-x-6' : 'translate-x-1'"
                            class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                          />
                        </Switch>
                      </div>
                    </div>
                  </template>

                  <!-- Active status (only for edit) -->
                  <div v-if="isEditMode" class="flex items-center justify-between rounded-lg border p-4 dark:border-gray-700">
                    <div>
                      <p class="text-sm font-medium text-gray-700 dark:text-gray-300">Account active</p>
                      <p class="text-xs text-gray-500 dark:text-gray-400">Inactive users cannot log in</p>
                    </div>
                    <Switch
                      v-model="isActive"
                      :class="isActive ? 'bg-aegis-600' : 'bg-gray-200 dark:bg-gray-700'"
                      class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors"
                    >
                      <span
                        :class="isActive ? 'translate-x-6' : 'translate-x-1'"
                        class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                      />
                    </Switch>
                  </div>
                </div>

                <!-- Actions -->
                <div class="mt-6 flex justify-end gap-3">
                  <button type="button" class="btn-secondary" @click="emit('close')">
                    Cancel
                  </button>
                  <button type="submit" class="btn-primary">
                    {{ isEditMode ? 'Save Changes' : 'Create User' }}
                  </button>
                </div>
              </form>
            </DialogPanel>
          </TransitionChild>
        </div>
      </div>
    </Dialog>
  </TransitionRoot>
</template>
