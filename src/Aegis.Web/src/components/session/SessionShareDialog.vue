<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useSessionStore } from '@/stores/session'
import { useToast } from '@/composables/useToast'
import type { Session } from '@/types'
import {
  XMarkIcon,
  UserIcon,
  UsersIcon,
  GlobeAltIcon,
  LinkIcon,
  ClipboardDocumentIcon,
  CheckIcon,
  ChevronUpDownIcon
} from '@heroicons/vue/24/outline'
import {
  Dialog,
  DialogPanel,
  DialogTitle,
  TransitionRoot,
  TransitionChild,
  RadioGroup,
  RadioGroupOption,
  Listbox,
  ListboxButton,
  ListboxOptions,
  ListboxOption
} from '@headlessui/vue'

const props = defineProps<{
  session: Session | null
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'shared'): void
}>()

const sessionStore = useSessionStore()
const toast = useToast()

const loading = ref(false)
const shareType = ref<'user' | 'team' | 'public'>('user')
const userEmail = ref('')
const teamId = ref('')
const permission = ref<'ReadOnly' | 'Comment' | 'Edit'>('ReadOnly')
const expiresIn = ref<string>('never')
const shareLink = ref<string | null>(null)
const copied = ref(false)

const permissions = [
  { value: 'ReadOnly', label: 'View Only', description: 'Can view the conversation' },
  { value: 'Comment', label: 'Comment', description: 'Can view and add comments' },
  { value: 'Edit', label: 'Edit', description: 'Can continue the conversation' }
] as const

const expirationOptions = [
  { value: 'never', label: 'Never expires' },
  { value: '1h', label: '1 hour' },
  { value: '24h', label: '24 hours' },
  { value: '7d', label: '7 days' },
  { value: '30d', label: '30 days' }
]

const shareTypes = [
  { value: 'user', label: 'User', description: 'Share with a specific user', icon: UserIcon },
  { value: 'team', label: 'Team', description: 'Share with an entire team', icon: UsersIcon },
  { value: 'public', label: 'Public Link', description: 'Anyone with the link can access', icon: GlobeAltIcon }
] as const

// Reset form when dialog opens/closes
watch(() => props.open, (isOpen) => {
  if (!isOpen) {
    shareType.value = 'user'
    userEmail.value = ''
    teamId.value = ''
    permission.value = 'ReadOnly'
    expiresIn.value = 'never'
    shareLink.value = null
    copied.value = false
  }
})

const canShare = computed(() => {
  if (shareType.value === 'user') return userEmail.value.trim().length > 0
  if (shareType.value === 'team') return teamId.value.trim().length > 0
  return true // public always valid
})

function calculateExpiresAt(): string | undefined {
  if (expiresIn.value === 'never') return undefined

  const now = new Date()
  switch (expiresIn.value) {
    case '1h': now.setHours(now.getHours() + 1); break
    case '24h': now.setHours(now.getHours() + 24); break
    case '7d': now.setDate(now.getDate() + 7); break
    case '30d': now.setDate(now.getDate() + 30); break
  }
  return now.toISOString()
}

async function handleShare() {
  if (!props.session || !canShare.value) return

  loading.value = true
  try {
    const request = {
      shareWithUserId: shareType.value === 'user' ? userEmail.value.trim() : undefined,
      shareWithTeamId: shareType.value === 'team' ? teamId.value.trim() : undefined,
      isPublic: shareType.value === 'public',
      permission: permission.value,
      expiresAt: calculateExpiresAt()
    }

    const success = await sessionStore.shareSession(props.session.id, request)

    if (success) {
      if (shareType.value === 'public') {
        // Generate a public share link
        shareLink.value = `${window.location.origin}/shared/session/${props.session.id}`
        toast.success('Public link created', 'Anyone with this link can access the session')
      } else {
        toast.success('Session shared', `Session has been shared successfully`)
        emit('shared')
        emit('close')
      }
    } else {
      toast.error('Share failed', sessionStore.error || 'Failed to share session')
    }
  } catch (error) {
    toast.error('Error', 'An unexpected error occurred')
  } finally {
    loading.value = false
  }
}

async function copyLink() {
  if (!shareLink.value) return

  try {
    await navigator.clipboard.writeText(shareLink.value)
    copied.value = true
    toast.success('Copied', 'Link copied to clipboard')
    setTimeout(() => {
      copied.value = false
    }, 2000)
  } catch {
    toast.error('Copy failed', 'Could not copy to clipboard')
  }
}
</script>

<template>
  <TransitionRoot appear :show="open" as="template">
    <Dialog as="div" class="relative z-50" @close="emit('close')">
      <TransitionChild
        enter="ease-out duration-300"
        enter-from="opacity-0"
        enter-to="opacity-100"
        leave="ease-in duration-200"
        leave-from="opacity-100"
        leave-to="opacity-0"
      >
        <div class="fixed inset-0 bg-black/30 backdrop-blur-sm" />
      </TransitionChild>

      <div class="fixed inset-0 overflow-y-auto">
        <div class="flex min-h-full items-center justify-center p-4">
          <TransitionChild
            enter="ease-out duration-300"
            enter-from="opacity-0 scale-95"
            enter-to="opacity-100 scale-100"
            leave="ease-in duration-200"
            leave-from="opacity-100 scale-100"
            leave-to="opacity-0 scale-95"
          >
            <DialogPanel class="w-full max-w-md transform overflow-hidden rounded-2xl bg-white p-6 shadow-xl transition-all dark:bg-gray-800">
              <div class="flex items-center justify-between mb-4">
                <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-white">
                  Share Session
                </DialogTitle>
                <button
                  class="text-gray-400 hover:text-gray-500 dark:hover:text-gray-300"
                  @click="emit('close')"
                >
                  <XMarkIcon class="h-5 w-5" />
                </button>
              </div>

              <!-- Session Info -->
              <div v-if="session" class="mb-6 p-3 bg-gray-50 rounded-lg dark:bg-gray-700/50">
                <p class="font-medium text-gray-900 dark:text-white truncate">
                  {{ session.title }}
                </p>
                <p class="text-sm text-gray-500 dark:text-gray-400">
                  {{ session.turnCount }} turns
                </p>
              </div>

              <!-- Share Type Selection -->
              <div class="mb-6">
                <label class="label mb-2">Share with</label>
                <RadioGroup v-model="shareType" class="space-y-2">
                  <RadioGroupOption
                    v-for="type in shareTypes"
                    :key="type.value"
                    :value="type.value"
                    v-slot="{ checked }"
                    class="cursor-pointer"
                  >
                    <div
                      class="flex items-center gap-3 p-3 rounded-lg border transition-colors"
                      :class="checked
                        ? 'border-aegis-500 bg-aegis-50 dark:border-aegis-400 dark:bg-aegis-900/20'
                        : 'border-gray-200 hover:bg-gray-50 dark:border-gray-600 dark:hover:bg-gray-700/50'"
                    >
                      <component
                        :is="type.icon"
                        class="h-5 w-5"
                        :class="checked ? 'text-aegis-600 dark:text-aegis-400' : 'text-gray-400'"
                      />
                      <div class="flex-1">
                        <p
                          class="font-medium"
                          :class="checked ? 'text-aegis-700 dark:text-aegis-300' : 'text-gray-900 dark:text-white'"
                        >
                          {{ type.label }}
                        </p>
                        <p class="text-sm text-gray-500 dark:text-gray-400">
                          {{ type.description }}
                        </p>
                      </div>
                      <div
                        v-if="checked"
                        class="w-5 h-5 rounded-full bg-aegis-600 flex items-center justify-center"
                      >
                        <CheckIcon class="h-3 w-3 text-white" />
                      </div>
                    </div>
                  </RadioGroupOption>
                </RadioGroup>
              </div>

              <!-- User Email Input -->
              <div v-if="shareType === 'user'" class="mb-4">
                <label class="label">Email Address</label>
                <input
                  v-model="userEmail"
                  type="email"
                  class="input w-full"
                  placeholder="user@example.com"
                />
              </div>

              <!-- Team ID Input -->
              <div v-if="shareType === 'team'" class="mb-4">
                <label class="label">Team ID</label>
                <input
                  v-model="teamId"
                  type="text"
                  class="input w-full"
                  placeholder="Enter team ID"
                />
              </div>

              <!-- Permission Selection -->
              <div class="mb-4">
                <label class="label">Permission Level</label>
                <Listbox v-model="permission">
                  <div class="relative mt-1">
                    <ListboxButton class="input w-full text-left flex items-center justify-between">
                      <span>{{ permissions.find(p => p.value === permission)?.label }}</span>
                      <ChevronUpDownIcon class="h-4 w-4 text-gray-400" />
                    </ListboxButton>
                    <ListboxOptions class="absolute z-10 mt-1 w-full bg-white rounded-md shadow-lg border border-gray-200 dark:bg-gray-800 dark:border-gray-700">
                      <ListboxOption
                        v-for="perm in permissions"
                        :key="perm.value"
                        :value="perm.value"
                        v-slot="{ selected, active }"
                      >
                        <div
                          class="cursor-pointer select-none px-4 py-3"
                          :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
                        >
                          <div class="flex items-center justify-between">
                            <span class="font-medium text-gray-900 dark:text-white">{{ perm.label }}</span>
                            <CheckIcon v-if="selected" class="h-4 w-4 text-aegis-600" />
                          </div>
                          <p class="text-sm text-gray-500 dark:text-gray-400">{{ perm.description }}</p>
                        </div>
                      </ListboxOption>
                    </ListboxOptions>
                  </div>
                </Listbox>
              </div>

              <!-- Expiration Selection -->
              <div class="mb-6">
                <label class="label">Expires</label>
                <Listbox v-model="expiresIn">
                  <div class="relative mt-1">
                    <ListboxButton class="input w-full text-left flex items-center justify-between">
                      <span>{{ expirationOptions.find(o => o.value === expiresIn)?.label }}</span>
                      <ChevronUpDownIcon class="h-4 w-4 text-gray-400" />
                    </ListboxButton>
                    <ListboxOptions class="absolute z-10 mt-1 w-full bg-white rounded-md shadow-lg border border-gray-200 dark:bg-gray-800 dark:border-gray-700">
                      <ListboxOption
                        v-for="option in expirationOptions"
                        :key="option.value"
                        :value="option.value"
                        v-slot="{ selected, active }"
                      >
                        <div
                          class="cursor-pointer select-none px-4 py-2 flex items-center justify-between"
                          :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
                        >
                          <span class="text-gray-900 dark:text-white">{{ option.label }}</span>
                          <CheckIcon v-if="selected" class="h-4 w-4 text-aegis-600" />
                        </div>
                      </ListboxOption>
                    </ListboxOptions>
                  </div>
                </Listbox>
              </div>

              <!-- Share Link (for public shares) -->
              <div v-if="shareLink" class="mb-6">
                <label class="label">Share Link</label>
                <div class="flex gap-2">
                  <div class="flex-1 flex items-center gap-2 px-3 py-2 bg-gray-100 rounded-lg dark:bg-gray-700">
                    <LinkIcon class="h-4 w-4 text-gray-400 shrink-0" />
                    <span class="text-sm text-gray-900 dark:text-white truncate">
                      {{ shareLink }}
                    </span>
                  </div>
                  <button
                    class="btn-secondary shrink-0"
                    @click="copyLink"
                  >
                    <CheckIcon v-if="copied" class="h-4 w-4 text-green-500" />
                    <ClipboardDocumentIcon v-else class="h-4 w-4" />
                  </button>
                </div>
              </div>

              <!-- Actions -->
              <div class="flex justify-end gap-3">
                <button
                  class="btn-ghost"
                  @click="emit('close')"
                >
                  {{ shareLink ? 'Done' : 'Cancel' }}
                </button>
                <button
                  v-if="!shareLink"
                  class="btn-primary"
                  :disabled="!canShare || loading"
                  @click="handleShare"
                >
                  <span v-if="loading">Sharing...</span>
                  <span v-else>Share</span>
                </button>
              </div>
            </DialogPanel>
          </TransitionChild>
        </div>
      </div>
    </Dialog>
  </TransitionRoot>
</template>
