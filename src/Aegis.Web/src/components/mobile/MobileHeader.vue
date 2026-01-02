<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import {
  ChevronLeftIcon,
  EllipsisVerticalIcon
} from '@heroicons/vue/24/outline'

interface Props {
  title?: string
  showBack?: boolean
  showMenu?: boolean
  transparent?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  title: '',
  showBack: false,
  showMenu: false,
  transparent: false
})

const emit = defineEmits<{
  back: []
  menu: []
}>()

const router = useRouter()

function handleBack() {
  emit('back')
  router.back()
}

const headerClass = computed(() => {
  return props.transparent
    ? 'bg-transparent'
    : 'bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700'
})
</script>

<template>
  <header
    class="sticky top-0 z-40 flex items-center justify-between h-14 px-4 safe-area-top md:hidden"
    :class="headerClass"
  >
    <!-- Left section -->
    <div class="flex items-center min-w-[48px]">
      <button
        v-if="showBack"
        class="flex items-center justify-center w-10 h-10 -ml-2 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700"
        @click="handleBack"
      >
        <ChevronLeftIcon class="w-6 h-6 text-gray-600 dark:text-gray-300" />
      </button>
    </div>

    <!-- Title -->
    <div class="flex-1 text-center">
      <h1
        v-if="title"
        class="text-lg font-semibold text-gray-900 dark:text-white truncate"
      >
        {{ title }}
      </h1>
      <slot name="title" />
    </div>

    <!-- Right section -->
    <div class="flex items-center min-w-[48px] justify-end">
      <button
        v-if="showMenu"
        class="flex items-center justify-center w-10 h-10 -mr-2 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700"
        @click="emit('menu')"
      >
        <EllipsisVerticalIcon class="w-6 h-6 text-gray-600 dark:text-gray-300" />
      </button>
      <slot name="actions" />
    </div>
  </header>
</template>

<style scoped>
/* Safe area for devices with notch (iPhone X+) */
.safe-area-top {
  padding-top: env(safe-area-inset-top, 0);
}
</style>
