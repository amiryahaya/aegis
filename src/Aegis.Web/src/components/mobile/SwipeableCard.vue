<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { TrashIcon, ArchiveBoxIcon, ShareIcon } from '@heroicons/vue/24/outline'

interface Props {
  showDelete?: boolean
  showArchive?: boolean
  showShare?: boolean
  actionWidth?: number
}

const props = withDefaults(defineProps<Props>(), {
  showDelete: true,
  showArchive: false,
  showShare: false,
  actionWidth: 80
})

const emit = defineEmits<{
  delete: []
  archive: []
  share: []
}>()

const cardRef = ref<HTMLElement | null>(null)
const translateX = ref(0)
const isAnimating = ref(false)
const isSwiping = ref(false)

let startX = 0
let startY = 0
let currentX = 0
let isHorizontalSwipe: boolean | null = null

// Calculate max swipe distance based on visible actions
const maxSwipe = computed(() => {
  let count = 0
  if (props.showDelete) count++
  if (props.showArchive) count++
  if (props.showShare) count++
  return count * props.actionWidth
})

// Whether actions are revealed
const isOpen = computed(() => translateX.value < -props.actionWidth / 2)

function handleTouchStart(e: TouchEvent) {
  if (isAnimating.value) return

  const touch = e.touches[0]
  startX = touch.clientX
  startY = touch.clientY
  currentX = translateX.value
  isHorizontalSwipe = null
  isSwiping.value = true
}

function handleTouchMove(e: TouchEvent) {
  if (!isSwiping.value) return

  const touch = e.touches[0]
  const diffX = touch.clientX - startX
  const diffY = touch.clientY - startY

  // Determine swipe direction on first significant move
  if (isHorizontalSwipe === null && (Math.abs(diffX) > 5 || Math.abs(diffY) > 5)) {
    isHorizontalSwipe = Math.abs(diffX) > Math.abs(diffY)
  }

  if (!isHorizontalSwipe) return

  e.preventDefault()

  let newX = currentX + diffX

  // Apply resistance at edges
  if (newX > 0) {
    newX = newX * 0.3 // Resistance when pulling right
  } else if (newX < -maxSwipe.value) {
    const overflow = newX + maxSwipe.value
    newX = -maxSwipe.value + overflow * 0.3
  }

  translateX.value = newX
}

function handleTouchEnd() {
  if (!isSwiping.value) return
  isSwiping.value = false

  // Snap to open or closed
  isAnimating.value = true

  if (translateX.value < -maxSwipe.value / 2) {
    // Snap open
    translateX.value = -maxSwipe.value
  } else {
    // Snap closed
    translateX.value = 0
  }

  setTimeout(() => {
    isAnimating.value = false
  }, 200)
}

function close() {
  isAnimating.value = true
  translateX.value = 0
  setTimeout(() => {
    isAnimating.value = false
  }, 200)
}

function handleAction(action: 'delete' | 'archive' | 'share') {
  close()
  if (action === 'delete') emit('delete')
  else if (action === 'archive') emit('archive')
  else if (action === 'share') emit('share')
}

// Close when clicking outside
function handleClickOutside(e: MouseEvent) {
  if (cardRef.value && !cardRef.value.contains(e.target as Node) && isOpen.value) {
    close()
  }
}

onMounted(() => {
  document.addEventListener('click', handleClickOutside)
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside)
})

defineExpose({ close })
</script>

<template>
  <div class="relative overflow-hidden" ref="cardRef">
    <!-- Action buttons (behind the card) -->
    <div
      class="absolute inset-y-0 right-0 flex items-stretch"
      :style="{ width: `${maxSwipe}px` }"
    >
      <button
        v-if="showShare"
        class="flex items-center justify-center flex-1 bg-blue-500 text-white"
        @click="handleAction('share')"
      >
        <ShareIcon class="w-5 h-5" />
      </button>
      <button
        v-if="showArchive"
        class="flex items-center justify-center flex-1 bg-yellow-500 text-white"
        @click="handleAction('archive')"
      >
        <ArchiveBoxIcon class="w-5 h-5" />
      </button>
      <button
        v-if="showDelete"
        class="flex items-center justify-center flex-1 bg-red-500 text-white"
        @click="handleAction('delete')"
      >
        <TrashIcon class="w-5 h-5" />
      </button>
    </div>

    <!-- Main card content -->
    <div
      class="relative bg-white dark:bg-gray-800 z-10"
      :class="{ 'transition-transform duration-200 ease-out': isAnimating }"
      :style="{ transform: `translateX(${translateX}px)` }"
      @touchstart="handleTouchStart"
      @touchmove="handleTouchMove"
      @touchend="handleTouchEnd"
    >
      <slot />
    </div>
  </div>
</template>
