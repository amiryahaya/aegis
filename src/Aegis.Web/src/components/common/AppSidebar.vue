<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Dialog, DialogPanel, TransitionChild, TransitionRoot } from '@headlessui/vue'
import {
  XMarkIcon,
  HomeIcon,
  ChatBubbleLeftRightIcon,
  ClockIcon,
  PlusIcon,
  FolderIcon,
  Cog6ToothIcon,
  ShieldCheckIcon
} from '@heroicons/vue/24/outline'
import { useSessionStore } from '@/stores/session'
import { useAuthStore } from '@/stores/auth'
import { SessionType } from '@/types'

defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  close: []
}>()

const route = useRoute()
const router = useRouter()
const sessionStore = useSessionStore()
const authStore = useAuthStore()

const navigation = computed(() => {
  const items = [
    { name: 'Dashboard', href: '/', icon: HomeIcon, current: route.path === '/' },
    { name: 'Chat', href: '/chat', icon: ChatBubbleLeftRightIcon, current: route.path.startsWith('/chat') },
    { name: 'Sessions', href: '/sessions', icon: ClockIcon, current: route.path === '/sessions' },
    { name: 'Workspaces', href: '/workspaces', icon: FolderIcon, current: route.path.startsWith('/workspaces') }
  ]
  return items
})

const bottomNavigation = computed(() => {
  const items = [
    { name: 'Settings', href: '/settings', icon: Cog6ToothIcon, current: route.path === '/settings' }
  ]
  if (authStore.isAdmin) {
    items.push({ name: 'Admin', href: '/admin', icon: ShieldCheckIcon, current: route.path === '/admin' })
  }
  return items
})

async function createNewSession() {
  if (!authStore.user) return

  const session = await sessionStore.createSession({
    userId: authStore.user.id,
    title: 'New Conversation',
    type: SessionType.QuickQuery
  })

  if (session) {
    router.push(`/chat/${session.id}`)
    emit('close')
  }
}
</script>

<template>
  <!-- Mobile sidebar -->
  <TransitionRoot as="template" :show="open">
    <Dialog as="div" class="relative z-50 lg:hidden" @close="emit('close')">
      <TransitionChild
        as="template"
        enter="transition-opacity ease-linear duration-300"
        enter-from="opacity-0"
        enter-to="opacity-100"
        leave="transition-opacity ease-linear duration-300"
        leave-from="opacity-100"
        leave-to="opacity-0"
      >
        <div class="fixed inset-0 bg-gray-900/80" />
      </TransitionChild>

      <div class="fixed inset-0 flex">
        <TransitionChild
          as="template"
          enter="transition ease-in-out duration-300 transform"
          enter-from="-translate-x-full"
          enter-to="translate-x-0"
          leave="transition ease-in-out duration-300 transform"
          leave-from="translate-x-0"
          leave-to="-translate-x-full"
        >
          <DialogPanel class="relative mr-16 flex w-full max-w-xs flex-1">
            <TransitionChild
              as="template"
              enter="ease-in-out duration-300"
              enter-from="opacity-0"
              enter-to="opacity-100"
              leave="ease-in-out duration-300"
              leave-from="opacity-100"
              leave-to="opacity-0"
            >
              <div class="absolute left-full top-0 flex w-16 justify-center pt-5">
                <button type="button" class="-m-2.5 p-2.5" @click="emit('close')">
                  <XMarkIcon class="h-6 w-6 text-white" />
                </button>
              </div>
            </TransitionChild>

            <!-- Sidebar content -->
            <div class="flex grow flex-col gap-y-5 overflow-y-auto bg-white px-6 pb-4 dark:bg-gray-800">
              <!-- Logo -->
              <div class="flex h-16 shrink-0 items-center">
                <span class="text-xl font-bold text-aegis-600 dark:text-aegis-400">AEGIS</span>
              </div>

              <!-- New chat button -->
              <button
                class="btn-primary w-full gap-2"
                @click="createNewSession"
              >
                <PlusIcon class="h-5 w-5" />
                New Chat
              </button>

              <!-- Navigation -->
              <nav class="flex flex-1 flex-col">
                <ul role="list" class="flex flex-1 flex-col gap-y-1">
                  <li v-for="item in navigation" :key="item.name">
                    <RouterLink
                      :to="item.href"
                      :class="[
                        item.current ? 'sidebar-link-active' : 'sidebar-link-inactive'
                      ]"
                      @click="emit('close')"
                    >
                      <component :is="item.icon" class="h-5 w-5 shrink-0" />
                      {{ item.name }}
                    </RouterLink>
                  </li>
                </ul>

                <!-- Bottom navigation -->
                <ul role="list" class="mt-auto border-t border-gray-200 pt-4 dark:border-gray-700">
                  <li v-for="item in bottomNavigation" :key="item.name">
                    <RouterLink
                      :to="item.href"
                      :class="[
                        item.current ? 'sidebar-link-active' : 'sidebar-link-inactive'
                      ]"
                      @click="emit('close')"
                    >
                      <component :is="item.icon" class="h-5 w-5 shrink-0" />
                      {{ item.name }}
                    </RouterLink>
                  </li>
                </ul>
              </nav>
            </div>
          </DialogPanel>
        </TransitionChild>
      </div>
    </Dialog>
  </TransitionRoot>

  <!-- Desktop sidebar -->
  <div
    class="hidden lg:fixed lg:inset-y-0 lg:z-50 lg:flex lg:flex-col transition-all duration-300"
    :class="open ? 'lg:w-64' : 'lg:w-20'"
  >
    <div class="flex grow flex-col gap-y-5 overflow-y-auto border-r border-gray-200 bg-white px-4 pb-4 dark:border-gray-700 dark:bg-gray-800">
      <!-- Logo -->
      <div class="flex h-16 shrink-0 items-center justify-center">
        <span v-if="open" class="text-xl font-bold text-aegis-600 dark:text-aegis-400">AEGIS</span>
        <span v-else class="text-xl font-bold text-aegis-600 dark:text-aegis-400">A</span>
      </div>

      <!-- New chat button -->
      <button
        class="btn-primary gap-2"
        :class="open ? 'w-full' : 'w-12 h-12 p-0'"
        @click="createNewSession"
      >
        <PlusIcon class="h-5 w-5" />
        <span v-if="open">New Chat</span>
      </button>

      <!-- Navigation -->
      <nav class="flex flex-1 flex-col">
        <ul role="list" class="flex flex-1 flex-col gap-y-1">
          <li v-for="item in navigation" :key="item.name">
            <RouterLink
              :to="item.href"
              :class="[
                item.current ? 'sidebar-link-active' : 'sidebar-link-inactive',
                !open && 'justify-center'
              ]"
              :title="!open ? item.name : undefined"
            >
              <component :is="item.icon" class="h-5 w-5 shrink-0" />
              <span v-if="open">{{ item.name }}</span>
            </RouterLink>
          </li>
        </ul>

        <!-- Bottom navigation -->
        <ul role="list" class="mt-auto border-t border-gray-200 pt-4 dark:border-gray-700">
          <li v-for="item in bottomNavigation" :key="item.name">
            <RouterLink
              :to="item.href"
              :class="[
                item.current ? 'sidebar-link-active' : 'sidebar-link-inactive',
                !open && 'justify-center'
              ]"
              :title="!open ? item.name : undefined"
            >
              <component :is="item.icon" class="h-5 w-5 shrink-0" />
              <span v-if="open">{{ item.name }}</span>
            </RouterLink>
          </li>
        </ul>
      </nav>
    </div>
  </div>

  <!-- Spacer for desktop sidebar -->
  <div
    class="hidden lg:block transition-all duration-300"
    :class="open ? 'lg:w-64' : 'lg:w-20'"
  />
</template>
