<script setup lang="ts">
import { ref, computed } from 'vue'
import { PlusIcon, XMarkIcon } from '@heroicons/vue/24/outline'

interface FabAction {
  id: string
  icon: typeof PlusIcon
  label: string
  color?: string
}

interface Props {
  actions?: FabAction[]
  icon?: typeof PlusIcon
  color?: string
  position?: 'bottom-right' | 'bottom-center' | 'bottom-left'
  offset?: number
}

const props = withDefaults(defineProps<Props>(), {
  actions: () => [],
  icon: () => PlusIcon,
  color: 'aegis',
  position: 'bottom-right',
  offset: 80 // Offset for bottom nav
})

const emit = defineEmits<{
  click: []
  action: [id: string]
}>()

const isExpanded = ref(false)

const positionClass = computed(() => {
  switch (props.position) {
    case 'bottom-left':
      return 'left-4'
    case 'bottom-center':
      return 'left-1/2 -translate-x-1/2'
    default:
      return 'right-4'
  }
})

const buttonColorClass = computed(() => {
  switch (props.color) {
    case 'red':
      return 'bg-red-600 hover:bg-red-700 focus:ring-red-500'
    case 'blue':
      return 'bg-blue-600 hover:bg-blue-700 focus:ring-blue-500'
    case 'green':
      return 'bg-green-600 hover:bg-green-700 focus:ring-green-500'
    default:
      return 'bg-aegis-600 hover:bg-aegis-700 focus:ring-aegis-500'
  }
})

function handleClick() {
  if (props.actions.length > 0) {
    isExpanded.value = !isExpanded.value
  } else {
    emit('click')
  }
}

function handleAction(actionId: string) {
  isExpanded.value = false
  emit('action', actionId)
}

function handleBackdropClick() {
  isExpanded.value = false
}
</script>

<template>
  <div class="md:hidden">
    <!-- Backdrop when expanded -->
    <transition
      enter-active-class="transition-opacity duration-200"
      enter-from-class="opacity-0"
      enter-to-class="opacity-100"
      leave-active-class="transition-opacity duration-200"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div
        v-if="isExpanded"
        class="fixed inset-0 bg-black/30 z-40"
        @click="handleBackdropClick"
      />
    </transition>

    <!-- FAB container -->
    <div
      class="fixed z-50"
      :class="positionClass"
      :style="{ bottom: `${offset + 16}px` }"
    >
      <!-- Action buttons -->
      <transition-group
        enter-active-class="transition-all duration-200 ease-out"
        enter-from-class="opacity-0 scale-50 translate-y-4"
        enter-to-class="opacity-100 scale-100 translate-y-0"
        leave-active-class="transition-all duration-150 ease-in"
        leave-from-class="opacity-100 scale-100 translate-y-0"
        leave-to-class="opacity-0 scale-50 translate-y-4"
        tag="div"
        class="flex flex-col items-end gap-3 mb-3"
      >
        <div
          v-for="(action, index) in actions"
          v-show="isExpanded"
          :key="action.id"
          class="flex items-center gap-3"
          :style="{ transitionDelay: `${index * 50}ms` }"
        >
          <span
            class="px-3 py-1.5 text-sm font-medium text-gray-700 bg-white rounded-lg shadow-lg dark:bg-gray-800 dark:text-gray-200"
          >
            {{ action.label }}
          </span>
          <button
            class="flex items-center justify-center w-12 h-12 text-white rounded-full shadow-lg transition-transform hover:scale-110"
            :class="action.color ? `bg-${action.color}-600` : buttonColorClass"
            @click="handleAction(action.id)"
          >
            <component :is="action.icon" class="w-5 h-5" />
          </button>
        </div>
      </transition-group>

      <!-- Main FAB button -->
      <button
        class="flex items-center justify-center w-14 h-14 text-white rounded-full shadow-lg transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-offset-2"
        :class="[
          buttonColorClass,
          { 'rotate-45': isExpanded && actions.length > 0 }
        ]"
        @click="handleClick"
      >
        <component
          :is="isExpanded && actions.length > 0 ? XMarkIcon : icon"
          class="w-6 h-6 transition-transform duration-200"
          :class="{ 'rotate-[-45deg]': isExpanded && actions.length > 0 }"
        />
      </button>
    </div>
  </div>
</template>
