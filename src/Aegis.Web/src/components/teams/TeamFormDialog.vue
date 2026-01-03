<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import {
  Dialog,
  DialogPanel,
  DialogTitle,
  TransitionChild,
  TransitionRoot,
  Listbox,
  ListboxButton,
  ListboxOptions,
  ListboxOption,
  Switch
} from '@headlessui/vue'
import {
  XMarkIcon,
  ChevronUpDownIcon,
  CheckIcon
} from '@heroicons/vue/24/outline'
import type { ManagedTeam, CreateTeamRequest, UpdateTeamRequest, TeamSettings } from '@/types'
import { UserRole } from '@/types/user'
import { ROLE_DEFINITIONS } from '@/types/userManagement'

const props = defineProps<{
  isOpen: boolean
  team?: ManagedTeam | null
}>()

const emit = defineEmits<{
  close: []
  create: [data: CreateTeamRequest]
  update: [teamId: string, data: UpdateTeamRequest]
}>()

const isEditMode = computed(() => !!props.team)

// Form state
const name = ref('')
const description = ref('')
const isActive = ref(true)
const allowMemberInvites = ref(true)
const defaultMemberRole = ref<UserRole>(UserRole.Viewer)
const maxMembers = ref(25)
const workspaceLimit = ref(10)
const queryRateLimit = ref(100)
const storageQuotaGb = ref(50)

const availableRoles = computed(() => ROLE_DEFINITIONS.filter(r => r.role !== UserRole.SystemAdmin))

// Reset form when dialog opens/closes or team changes
watch([() => props.isOpen, () => props.team], ([open, team]) => {
  if (open) {
    if (team) {
      // Edit mode - populate form
      name.value = team.name
      description.value = team.description || ''
      isActive.value = team.isActive
      allowMemberInvites.value = team.settings.allowMemberInvites
      defaultMemberRole.value = team.settings.defaultMemberRole
      maxMembers.value = team.settings.maxMembers
      workspaceLimit.value = team.settings.workspaceLimit
      queryRateLimit.value = team.settings.queryRateLimit
      storageQuotaGb.value = team.settings.storageQuotaGb
    } else {
      // Create mode - reset form
      name.value = ''
      description.value = ''
      isActive.value = true
      allowMemberInvites.value = true
      defaultMemberRole.value = UserRole.Viewer
      maxMembers.value = 25
      workspaceLimit.value = 10
      queryRateLimit.value = 100
      storageQuotaGb.value = 50
    }
  }
}, { immediate: true })

const isValid = computed(() => name.value.trim().length >= 2)

function handleSubmit() {
  if (!isValid.value) return

  const settings: Partial<TeamSettings> = {
    allowMemberInvites: allowMemberInvites.value,
    defaultMemberRole: defaultMemberRole.value,
    maxMembers: maxMembers.value,
    workspaceLimit: workspaceLimit.value,
    queryRateLimit: queryRateLimit.value,
    storageQuotaGb: storageQuotaGb.value
  }

  if (isEditMode.value && props.team) {
    emit('update', props.team.id, {
      name: name.value,
      description: description.value || undefined,
      isActive: isActive.value,
      settings
    })
  } else {
    emit('create', {
      name: name.value,
      description: description.value || undefined,
      settings
    })
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
              class="w-full max-w-lg transform overflow-hidden rounded-2xl bg-white p-6 shadow-xl transition-all dark:bg-gray-900"
            >
              <!-- Header -->
              <div class="mb-6 flex items-center justify-between">
                <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-white">
                  {{ isEditMode ? 'Edit Team' : 'Create Team' }}
                </DialogTitle>
                <button
                  class="rounded-lg p-2 text-gray-400 hover:bg-gray-100 hover:text-gray-600 dark:hover:bg-gray-800"
                  @click="emit('close')"
                >
                  <XMarkIcon class="h-5 w-5" />
                </button>
              </div>

              <form @submit.prevent="handleSubmit" class="space-y-6">
                <!-- Basic Info -->
                <div class="space-y-4">
                  <h3 class="text-sm font-medium text-gray-900 dark:text-white">Basic Information</h3>

                  <div>
                    <label for="name" class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                      Team Name *
                    </label>
                    <input
                      id="name"
                      v-model="name"
                      type="text"
                      required
                      class="mt-1 block w-full rounded-lg border-gray-300 shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-800 dark:text-white sm:text-sm"
                      placeholder="e.g., Engineering Team"
                    />
                  </div>

                  <div>
                    <label for="description" class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                      Description
                    </label>
                    <textarea
                      id="description"
                      v-model="description"
                      rows="2"
                      class="mt-1 block w-full rounded-lg border-gray-300 shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-800 dark:text-white sm:text-sm"
                      placeholder="Brief description of the team"
                    />
                  </div>

                  <!-- Active status (edit mode only) -->
                  <div v-if="isEditMode" class="flex items-center justify-between">
                    <div>
                      <label class="text-sm font-medium text-gray-700 dark:text-gray-300">Active Status</label>
                      <p class="text-xs text-gray-500 dark:text-gray-400">Inactive teams cannot access workspaces</p>
                    </div>
                    <Switch
                      v-model="isActive"
                      :class="[
                        isActive ? 'bg-aegis-600' : 'bg-gray-200 dark:bg-gray-700',
                        'relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-aegis-500 focus:ring-offset-2 dark:focus:ring-offset-gray-900'
                      ]"
                    >
                      <span
                        :class="[
                          isActive ? 'translate-x-5' : 'translate-x-0',
                          'pointer-events-none relative inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out'
                        ]"
                      />
                    </Switch>
                  </div>
                </div>

                <!-- Settings -->
                <div class="space-y-4">
                  <h3 class="text-sm font-medium text-gray-900 dark:text-white">Team Settings</h3>

                  <!-- Allow Member Invites -->
                  <div class="flex items-center justify-between">
                    <div>
                      <label class="text-sm font-medium text-gray-700 dark:text-gray-300">Allow Member Invites</label>
                      <p class="text-xs text-gray-500 dark:text-gray-400">Let members invite others to the team</p>
                    </div>
                    <Switch
                      v-model="allowMemberInvites"
                      :class="[
                        allowMemberInvites ? 'bg-aegis-600' : 'bg-gray-200 dark:bg-gray-700',
                        'relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-aegis-500 focus:ring-offset-2 dark:focus:ring-offset-gray-900'
                      ]"
                    >
                      <span
                        :class="[
                          allowMemberInvites ? 'translate-x-5' : 'translate-x-0',
                          'pointer-events-none relative inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out'
                        ]"
                      />
                    </Switch>
                  </div>

                  <!-- Default Member Role -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                      Default Member Role
                    </label>
                    <Listbox v-model="defaultMemberRole">
                      <div class="relative mt-1">
                        <ListboxButton
                          class="relative w-full cursor-pointer rounded-lg border border-gray-300 bg-white py-2 pl-3 pr-10 text-left shadow-sm focus:border-aegis-500 focus:outline-none focus:ring-1 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-800 sm:text-sm"
                        >
                          <span class="block truncate text-gray-900 dark:text-white">
                            {{ availableRoles.find(r => r.role === defaultMemberRole)?.label || defaultMemberRole }}
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
                              v-for="role in availableRoles"
                              :key="role.role"
                              v-slot="{ active, selected }"
                              :value="role.role"
                              as="template"
                            >
                              <li
                                :class="[
                                  active ? 'bg-aegis-100 text-aegis-900 dark:bg-aegis-900/30 dark:text-aegis-400' : 'text-gray-900 dark:text-gray-100',
                                  'relative cursor-pointer select-none py-2 pl-10 pr-4'
                                ]"
                              >
                                <div>
                                  <span :class="[selected ? 'font-medium' : 'font-normal', 'block']">
                                    {{ role.label }}
                                  </span>
                                  <span class="text-xs text-gray-500 dark:text-gray-400">
                                    {{ role.description }}
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

                  <!-- Limits -->
                  <div class="grid grid-cols-2 gap-4">
                    <div>
                      <label for="maxMembers" class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                        Max Members
                      </label>
                      <input
                        id="maxMembers"
                        v-model.number="maxMembers"
                        type="number"
                        min="1"
                        max="1000"
                        class="mt-1 block w-full rounded-lg border-gray-300 shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-800 dark:text-white sm:text-sm"
                      />
                    </div>
                    <div>
                      <label for="workspaceLimit" class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                        Workspace Limit
                      </label>
                      <input
                        id="workspaceLimit"
                        v-model.number="workspaceLimit"
                        type="number"
                        min="1"
                        max="100"
                        class="mt-1 block w-full rounded-lg border-gray-300 shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-800 dark:text-white sm:text-sm"
                      />
                    </div>
                    <div>
                      <label for="queryRateLimit" class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                        Query Rate (/min)
                      </label>
                      <input
                        id="queryRateLimit"
                        v-model.number="queryRateLimit"
                        type="number"
                        min="10"
                        max="10000"
                        class="mt-1 block w-full rounded-lg border-gray-300 shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-800 dark:text-white sm:text-sm"
                      />
                    </div>
                    <div>
                      <label for="storageQuotaGb" class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                        Storage (GB)
                      </label>
                      <input
                        id="storageQuotaGb"
                        v-model.number="storageQuotaGb"
                        type="number"
                        min="1"
                        max="10000"
                        class="mt-1 block w-full rounded-lg border-gray-300 shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-800 dark:text-white sm:text-sm"
                      />
                    </div>
                  </div>
                </div>

                <!-- Actions -->
                <div class="flex justify-end gap-3 border-t pt-4 dark:border-gray-700">
                  <button
                    type="button"
                    class="rounded-lg border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-600 dark:bg-gray-800 dark:text-gray-300 dark:hover:bg-gray-700"
                    @click="emit('close')"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    class="rounded-lg bg-aegis-600 px-4 py-2 text-sm font-medium text-white hover:bg-aegis-700 disabled:cursor-not-allowed disabled:opacity-50"
                    :disabled="!isValid"
                  >
                    {{ isEditMode ? 'Save Changes' : 'Create Team' }}
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
