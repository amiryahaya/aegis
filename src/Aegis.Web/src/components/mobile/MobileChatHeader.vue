<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import {
  ArrowLeftIcon,
  EllipsisVerticalIcon,
  PencilIcon
} from '@heroicons/vue/24/outline'
import { Menu, MenuButton, MenuItem, MenuItems } from '@headlessui/vue'

interface Props {
  title: string
  subtitle?: string
  showBack?: boolean
  isEditing?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  showBack: true,
  isEditing: false
})

const emit = defineEmits<{
  back: []
  edit: []
  'save-title': [title: string]
  delete: []
  export: [format: string]
}>()

const router = useRouter()
const editValue = defineModel<string>('editTitle', { default: '' })

function handleBack() {
  emit('back')
  router.back()
}

const displayTitle = computed(() => {
  if (props.title.length > 25) {
    return props.title.slice(0, 25) + '...'
  }
  return props.title
})
</script>

<template>
  <header class="sticky top-0 z-30 flex items-center justify-between bg-white px-3 py-2 shadow-sm dark:bg-gray-800 safe-area-top md:hidden">
    <!-- Back button -->
    <button
      v-if="showBack"
      class="flex h-10 w-10 items-center justify-center rounded-full text-gray-600 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700"
      @click="handleBack"
    >
      <ArrowLeftIcon class="h-5 w-5" />
    </button>
    <div v-else class="w-10" />

    <!-- Title -->
    <div class="flex-1 text-center">
      <template v-if="isEditing">
        <input
          v-model="editValue"
          class="w-full rounded-lg border border-gray-300 px-2 py-1 text-center text-sm font-semibold dark:border-gray-600 dark:bg-gray-700"
          @keydown.enter="emit('save-title', editValue)"
          @keydown.escape="emit('edit')"
        />
      </template>
      <template v-else>
        <button
          class="inline-flex items-center gap-1"
          @click="emit('edit')"
        >
          <h1 class="text-base font-semibold text-gray-900 dark:text-white">
            {{ displayTitle }}
          </h1>
          <PencilIcon class="h-3 w-3 text-gray-400" />
        </button>
        <p v-if="subtitle" class="text-xs text-gray-500 dark:text-gray-400">
          {{ subtitle }}
        </p>
      </template>
    </div>

    <!-- Menu -->
    <Menu as="div" class="relative">
      <MenuButton
        class="flex h-10 w-10 items-center justify-center rounded-full text-gray-600 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700"
      >
        <EllipsisVerticalIcon class="h-5 w-5" />
      </MenuButton>

      <transition
        enter-active-class="transition ease-out duration-100"
        enter-from-class="transform opacity-0 scale-95"
        enter-to-class="transform opacity-100 scale-100"
        leave-active-class="transition ease-in duration-75"
        leave-from-class="transform opacity-100 scale-100"
        leave-to-class="transform opacity-0 scale-95"
      >
        <MenuItems
          class="absolute right-0 mt-2 w-48 origin-top-right rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800 dark:ring-gray-700"
        >
          <MenuItem v-slot="{ active }">
            <button
              class="flex w-full items-center gap-2 px-4 py-2.5 text-sm"
              :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
              @click="emit('export', 'Markdown')"
            >
              Export as Markdown
            </button>
          </MenuItem>
          <MenuItem v-slot="{ active }">
            <button
              class="flex w-full items-center gap-2 px-4 py-2.5 text-sm"
              :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
              @click="emit('export', 'Json')"
            >
              Export as JSON
            </button>
          </MenuItem>
          <MenuItem v-slot="{ active }">
            <button
              class="flex w-full items-center gap-2 px-4 py-2.5 text-sm text-red-600 dark:text-red-400"
              :class="active ? 'bg-red-50 dark:bg-red-900/30' : ''"
              @click="emit('delete')"
            >
              Delete session
            </button>
          </MenuItem>
        </MenuItems>
      </transition>
    </Menu>
  </header>
</template>

<style scoped>
.safe-area-top {
  padding-top: max(0.5rem, env(safe-area-inset-top, 0px));
}
</style>
