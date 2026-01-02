<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  Dialog,
  DialogPanel,
  TransitionRoot,
  TransitionChild
} from '@headlessui/vue'
import {
  SparklesIcon,
  FolderIcon,
  ChatBubbleLeftRightIcon,
  UserGroupIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  XMarkIcon,
  RocketLaunchIcon
} from '@heroicons/vue/24/outline'
import { useOnboarding } from '@/composables/useOnboarding'
import { WELCOME_SLIDES } from '@/types/onboarding'

const {
  isWelcomeModalOpen,
  completeWelcome,
  hideWelcome,
  startTour
} = useOnboarding()

const currentSlide = ref(0)

const totalSlides = WELCOME_SLIDES.length

const isFirstSlide = computed(() => currentSlide.value === 0)
const isLastSlide = computed(() => currentSlide.value === totalSlides - 1)

const slide = computed(() => WELCOME_SLIDES[currentSlide.value])

function getIcon(iconName: string) {
  const icons: Record<string, typeof SparklesIcon> = {
    SparklesIcon,
    FolderIcon,
    ChatBubbleLeftRightIcon,
    UserGroupIcon
  }
  return icons[iconName] || SparklesIcon
}

function nextSlide(): void {
  if (!isLastSlide.value) {
    currentSlide.value++
  }
}

function previousSlide(): void {
  if (!isFirstSlide.value) {
    currentSlide.value--
  }
}

function goToSlide(index: number): void {
  if (index >= 0 && index < totalSlides) {
    currentSlide.value = index
  }
}

function handleGetStarted(): void {
  completeWelcome()
  // Start the main tour after a short delay
  setTimeout(() => {
    startTour('main')
  }, 300)
}

function handleSkip(): void {
  completeWelcome()
}
</script>

<template>
  <TransitionRoot :show="isWelcomeModalOpen" as="template">
    <Dialog
      :open="isWelcomeModalOpen"
      @close="hideWelcome"
      class="relative z-50"
    >
      <!-- Backdrop -->
      <TransitionChild
        as="template"
        enter="ease-out duration-300"
        enter-from="opacity-0"
        enter-to="opacity-100"
        leave="ease-in duration-200"
        leave-from="opacity-100"
        leave-to="opacity-0"
      >
        <div class="fixed inset-0 bg-gray-900/75 backdrop-blur-sm" />
      </TransitionChild>

      <!-- Dialog content -->
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
              class="w-full max-w-lg transform overflow-hidden rounded-2xl bg-white dark:bg-gray-800 shadow-2xl transition-all"
            >
              <!-- Close button -->
              <button
                @click="handleSkip"
                class="absolute top-4 right-4 p-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 z-10"
              >
                <XMarkIcon class="h-5 w-5" />
              </button>

              <!-- Slide content -->
              <div class="relative overflow-hidden">
                <!-- Background gradient -->
                <div class="absolute inset-0 bg-gradient-to-br from-aegis-50 to-white dark:from-aegis-900/20 dark:to-gray-800" />

                <!-- Icon and content -->
                <div class="relative px-8 pt-12 pb-8 text-center">
                  <!-- Icon -->
                  <div class="mx-auto w-20 h-20 bg-aegis-100 dark:bg-aegis-900/50 rounded-2xl flex items-center justify-center mb-6">
                    <component
                      :is="getIcon(slide.icon)"
                      class="h-10 w-10 text-aegis-600 dark:text-aegis-400"
                    />
                  </div>

                  <!-- Title -->
                  <h2 class="text-2xl font-bold text-gray-900 dark:text-white mb-3">
                    {{ slide.title }}
                  </h2>

                  <!-- Description -->
                  <p class="text-gray-600 dark:text-gray-400 leading-relaxed">
                    {{ slide.description }}
                  </p>
                </div>
              </div>

              <!-- Footer with navigation -->
              <div class="px-8 py-6 bg-gray-50 dark:bg-gray-700/50 border-t border-gray-200 dark:border-gray-700">
                <!-- Progress dots -->
                <div class="flex justify-center gap-2 mb-6">
                  <button
                    v-for="(_, index) in WELCOME_SLIDES"
                    :key="index"
                    @click="goToSlide(index)"
                    class="w-2 h-2 rounded-full transition-all"
                    :class="[
                      index === currentSlide
                        ? 'w-6 bg-aegis-500'
                        : 'bg-gray-300 dark:bg-gray-600 hover:bg-gray-400 dark:hover:bg-gray-500'
                    ]"
                  />
                </div>

                <!-- Navigation buttons -->
                <div class="flex items-center justify-between">
                  <!-- Previous / Skip -->
                  <button
                    v-if="!isFirstSlide"
                    @click="previousSlide"
                    class="flex items-center gap-1 px-4 py-2 text-sm font-medium text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-200"
                  >
                    <ChevronLeftIcon class="h-4 w-4" />
                    Back
                  </button>
                  <button
                    v-else
                    @click="handleSkip"
                    class="px-4 py-2 text-sm font-medium text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200"
                  >
                    Skip
                  </button>

                  <!-- Next / Get Started -->
                  <button
                    v-if="!isLastSlide"
                    @click="nextSlide"
                    class="flex items-center gap-1 px-4 py-2 text-sm font-medium text-white bg-aegis-600 hover:bg-aegis-700 rounded-lg transition-colors"
                  >
                    Next
                    <ChevronRightIcon class="h-4 w-4" />
                  </button>
                  <button
                    v-else
                    @click="handleGetStarted"
                    class="flex items-center gap-2 px-5 py-2.5 text-sm font-medium text-white bg-aegis-600 hover:bg-aegis-700 rounded-lg transition-colors"
                  >
                    <RocketLaunchIcon class="h-4 w-4" />
                    Get Started
                  </button>
                </div>
              </div>
            </DialogPanel>
          </TransitionChild>
        </div>
      </div>
    </Dialog>
  </TransitionRoot>
</template>
