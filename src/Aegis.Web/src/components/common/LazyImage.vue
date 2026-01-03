<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useLazyLoad } from '@/composables/useIntersectionObserver'

const props = withDefaults(defineProps<{
  /** Image source URL */
  src: string
  /** Alt text for accessibility */
  alt: string
  /** Placeholder image or color */
  placeholder?: string
  /** Width of the image */
  width?: number | string
  /** Height of the image */
  height?: number | string
  /** Object fit style */
  fit?: 'contain' | 'cover' | 'fill' | 'none' | 'scale-down'
  /** Blur amount for placeholder */
  blurAmount?: number
  /** Transition duration in ms */
  transitionDuration?: number
  /** Root margin for lazy loading */
  rootMargin?: string
  /** Fallback image on error */
  fallback?: string
  /** CSS class for the image */
  imageClass?: string
}>(), {
  placeholder: '',
  fit: 'cover',
  blurAmount: 10,
  transitionDuration: 300,
  rootMargin: '100px',
  fallback: '',
  imageClass: ''
})

const emit = defineEmits<{
  (e: 'load'): void
  (e: 'error', error: Event): void
}>()

const containerRef = ref<HTMLElement | null>(null)
const imageRef = ref<HTMLImageElement | null>(null)
const hasError = ref(false)
const isImageLoaded = ref(false)

// Lazy load detection
const { isLoaded: shouldLoad } = useLazyLoad(containerRef, {
  rootMargin: props.rootMargin
})

// Actual image source
const imageSrc = computed(() => {
  if (hasError.value && props.fallback) {
    return props.fallback
  }
  return shouldLoad.value ? props.src : ''
})

// Container style
const containerStyle = computed(() => ({
  width: typeof props.width === 'number' ? `${props.width}px` : props.width,
  height: typeof props.height === 'number' ? `${props.height}px` : props.height,
  position: 'relative' as const,
  overflow: 'hidden'
}))

// Image style
const imageStyle = computed(() => ({
  objectFit: props.fit,
  opacity: isImageLoaded.value ? 1 : 0,
  transition: `opacity ${props.transitionDuration}ms ease-in-out`,
  filter: isImageLoaded.value ? 'none' : `blur(${props.blurAmount}px)`
}))

// Placeholder style
const placeholderStyle = computed(() => ({
  position: 'absolute' as const,
  inset: 0,
  opacity: isImageLoaded.value ? 0 : 1,
  transition: `opacity ${props.transitionDuration}ms ease-in-out`,
  backgroundColor: props.placeholder.startsWith('#') ? props.placeholder : undefined,
  backgroundImage: props.placeholder && !props.placeholder.startsWith('#')
    ? `url(${props.placeholder})`
    : undefined,
  backgroundSize: 'cover',
  backgroundPosition: 'center'
}))

// Handle image load
function onLoad() {
  isImageLoaded.value = true
  emit('load')
}

// Handle image error
function onError(event: Event) {
  hasError.value = true
  emit('error', event)
}

// Reset state when src changes
watch(() => props.src, () => {
  hasError.value = false
  isImageLoaded.value = false
})
</script>

<template>
  <div
    ref="containerRef"
    :style="containerStyle"
    class="lazy-image-container"
  >
    <!-- Placeholder -->
    <div
      v-if="placeholder || !isImageLoaded"
      :style="placeholderStyle"
      class="lazy-image-placeholder"
    >
      <slot name="placeholder">
        <div
          v-if="!placeholder"
          class="h-full w-full animate-pulse bg-gray-200 dark:bg-gray-700"
        />
      </slot>
    </div>

    <!-- Actual image -->
    <img
      v-if="imageSrc"
      ref="imageRef"
      :src="imageSrc"
      :alt="alt"
      :style="imageStyle"
      :class="['lazy-image', imageClass]"
      @load="onLoad"
      @error="onError"
    />

    <!-- Error state -->
    <slot v-if="hasError && !fallback" name="error">
      <div class="absolute inset-0 flex items-center justify-center bg-gray-100 dark:bg-gray-800">
        <svg
          class="h-8 w-8 text-gray-400"
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
        >
          <path
            stroke-linecap="round"
            stroke-linejoin="round"
            stroke-width="2"
            d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"
          />
        </svg>
      </div>
    </slot>
  </div>
</template>

<style scoped>
.lazy-image-container {
  display: inline-block;
}

.lazy-image {
  width: 100%;
  height: 100%;
  display: block;
}

.lazy-image-placeholder {
  pointer-events: none;
}
</style>
