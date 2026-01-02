<script setup lang="ts">
import { onMounted, watch } from 'vue'
import { useOnboarding } from '@/composables/useOnboarding'
import TourTooltip from './TourTooltip.vue'

const {
  isTourActive,
  currentStepData,
  currentStep,
  totalSteps,
  isFirstStep,
  isLastStep,
  nextStep,
  previousStep,
  skipTour,
  registerMainTour
} = useOnboarding()

// Register main tour on mount
onMounted(() => {
  registerMainTour()
})

// Handle close
function handleClose(): void {
  skipTour()
}

// Handle keyboard navigation
function handleKeyDown(event: KeyboardEvent): void {
  if (!isTourActive.value) return

  switch (event.key) {
    case 'Escape':
      skipTour()
      break
    case 'ArrowRight':
    case 'Enter':
      nextStep()
      break
    case 'ArrowLeft':
      previousStep()
      break
  }
}

// Add keyboard listener when tour is active
watch(isTourActive, (active) => {
  if (active) {
    window.addEventListener('keydown', handleKeyDown)
  } else {
    window.removeEventListener('keydown', handleKeyDown)
  }
})
</script>

<template>
  <TourTooltip
    v-if="isTourActive && currentStepData"
    :step="currentStepData"
    :current-index="currentStep"
    :total-steps="totalSteps"
    :is-first="isFirstStep"
    :is-last="isLastStep"
    @next="nextStep"
    @previous="previousStep"
    @skip="skipTour"
    @close="handleClose"
  />
</template>
