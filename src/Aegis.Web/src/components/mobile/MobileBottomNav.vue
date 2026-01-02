<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  HomeIcon,
  ChatBubbleLeftRightIcon,
  FolderIcon,
  MagnifyingGlassIcon,
  UserCircleIcon
} from '@heroicons/vue/24/outline'
import {
  HomeIcon as HomeIconSolid,
  ChatBubbleLeftRightIcon as ChatIconSolid,
  FolderIcon as FolderIconSolid,
  MagnifyingGlassIcon as SearchIconSolid,
  UserCircleIcon as UserIconSolid
} from '@heroicons/vue/24/solid'

const route = useRoute()
const router = useRouter()

interface NavItem {
  name: string
  href: string
  icon: typeof HomeIcon
  activeIcon: typeof HomeIconSolid
  match: (path: string) => boolean
}

const navItems: NavItem[] = [
  {
    name: 'Home',
    href: '/',
    icon: HomeIcon,
    activeIcon: HomeIconSolid,
    match: (path) => path === '/'
  },
  {
    name: 'Chat',
    href: '/chat',
    icon: ChatBubbleLeftRightIcon,
    activeIcon: ChatIconSolid,
    match: (path) => path.startsWith('/chat')
  },
  {
    name: 'Search',
    href: '/search',
    icon: MagnifyingGlassIcon,
    activeIcon: SearchIconSolid,
    match: (path) => path === '/search'
  },
  {
    name: 'Workspaces',
    href: '/workspaces',
    icon: FolderIcon,
    activeIcon: FolderIconSolid,
    match: (path) => path.startsWith('/workspaces')
  },
  {
    name: 'Profile',
    href: '/profile',
    icon: UserCircleIcon,
    activeIcon: UserIconSolid,
    match: (path) => path === '/profile' || path === '/settings'
  }
]

const currentPath = computed(() => route.path)

function isActive(item: NavItem): boolean {
  return item.match(currentPath.value)
}

function navigate(href: string) {
  router.push(href)
}
</script>

<template>
  <nav
    class="fixed bottom-0 left-0 right-0 z-50 bg-white border-t border-gray-200 dark:bg-gray-800 dark:border-gray-700 md:hidden safe-area-bottom"
  >
    <div class="flex items-center justify-around h-16">
      <button
        v-for="item in navItems"
        :key="item.name"
        class="flex flex-col items-center justify-center flex-1 h-full px-2 transition-colors"
        :class="[
          isActive(item)
            ? 'text-aegis-600 dark:text-aegis-400'
            : 'text-gray-500 dark:text-gray-400'
        ]"
        @click="navigate(item.href)"
      >
        <component
          :is="isActive(item) ? item.activeIcon : item.icon"
          class="w-6 h-6"
        />
        <span class="mt-1 text-xs font-medium">{{ item.name }}</span>
      </button>
    </div>
  </nav>
</template>

<style scoped>
/* Safe area for devices with home indicator (iPhone X+) */
.safe-area-bottom {
  padding-bottom: env(safe-area-inset-bottom, 0);
}
</style>
