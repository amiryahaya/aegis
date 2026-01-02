import { ref, computed, watch } from 'vue'
import type { Tour, TourStep, OnboardingState, OnboardingPreferences } from '@/types/onboarding'
import { DEFAULT_ONBOARDING_STATE, MAIN_TOUR_STEPS } from '@/types/onboarding'

const STORAGE_KEY = 'aegis_onboarding'

// Global state
const state = ref<OnboardingState>(DEFAULT_ONBOARDING_STATE)
const tours = ref<Map<string, Tour>>(new Map())
const isWelcomeModalOpen = ref(false)
const isTourActive = ref(false)

// Load state from localStorage
function loadState(): void {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored) {
      const parsed = JSON.parse(stored)
      state.value = { ...DEFAULT_ONBOARDING_STATE, ...parsed }
    }
  } catch {
    state.value = DEFAULT_ONBOARDING_STATE
  }
}

// Save state to localStorage
function saveState(): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state.value))
  } catch {
    // Ignore storage errors
  }
}

// Watch for state changes and persist
watch(state, saveState, { deep: true })

export function useOnboarding() {
  // Initialize state on first use
  if (!state.value.hasCompletedWelcome && state.value.completedTours.length === 0) {
    loadState()
  }

  // Computed properties
  const hasCompletedWelcome = computed(() => state.value.hasCompletedWelcome)
  const currentTour = computed(() => state.value.currentTour)
  const currentStep = computed(() => state.value.currentStep)
  const preferences = computed(() => state.value.preferences)

  const currentTourData = computed((): Tour | null => {
    if (!state.value.currentTour) return null
    return tours.value.get(state.value.currentTour) || null
  })

  const currentStepData = computed((): TourStep | null => {
    if (!currentTourData.value) return null
    return currentTourData.value.steps[state.value.currentStep] || null
  })

  const totalSteps = computed((): number => {
    return currentTourData.value?.steps.length || 0
  })

  const progress = computed((): number => {
    if (totalSteps.value === 0) return 0
    return Math.round(((state.value.currentStep + 1) / totalSteps.value) * 100)
  })

  const isFirstStep = computed(() => state.value.currentStep === 0)
  const isLastStep = computed(() => state.value.currentStep === totalSteps.value - 1)

  // Register a tour
  function registerTour(tour: Tour): void {
    tours.value.set(tour.id, tour)
  }

  // Unregister a tour
  function unregisterTour(tourId: string): void {
    tours.value.delete(tourId)
  }

  // Start a tour
  async function startTour(tourId: string): Promise<void> {
    const tour = tours.value.get(tourId)
    if (!tour) {
      console.warn(`Tour "${tourId}" not found`)
      return
    }

    state.value.currentTour = tourId
    state.value.currentStep = 0
    isTourActive.value = true

    // Check if first step has a beforeShow callback
    const firstStep = tour.steps[0]
    if (firstStep?.beforeShow) {
      const canShow = await firstStep.beforeShow()
      if (!canShow) {
        await nextStep()
      }
    }
  }

  // Go to next step
  async function nextStep(): Promise<void> {
    const tour = currentTourData.value
    if (!tour) return

    if (isLastStep.value) {
      await completeTour()
      return
    }

    const nextIndex = state.value.currentStep + 1
    const nextStepData = tour.steps[nextIndex]

    // Execute current step action if defined
    const currentStepAction = currentStepData.value?.action
    if (currentStepAction) {
      await currentStepAction()
    }

    // Check if next step has a beforeShow callback
    if (nextStepData?.beforeShow) {
      const canShow = await nextStepData.beforeShow()
      if (!canShow) {
        state.value.currentStep = nextIndex
        await nextStep() // Skip to the step after
        return
      }
    }

    state.value.currentStep = nextIndex
  }

  // Go to previous step
  function previousStep(): void {
    if (!isFirstStep.value) {
      state.value.currentStep--
    }
  }

  // Go to specific step
  function goToStep(index: number): void {
    const tour = currentTourData.value
    if (!tour) return

    if (index >= 0 && index < tour.steps.length) {
      state.value.currentStep = index
    }
  }

  // Complete current tour
  async function completeTour(): Promise<void> {
    const tour = currentTourData.value
    if (!tour) return

    // Execute final step action if defined
    const currentStepAction = currentStepData.value?.action
    if (currentStepAction) {
      await currentStepAction()
    }

    // Mark tour as completed
    if (!state.value.completedTours.includes(tour.id)) {
      state.value.completedTours.push(tour.id)
    }

    // Remove from skipped if it was there
    const skippedIndex = state.value.skippedTours.indexOf(tour.id)
    if (skippedIndex !== -1) {
      state.value.skippedTours.splice(skippedIndex, 1)
    }

    // Call onComplete callback
    tour.onComplete?.()

    // Reset tour state
    state.value.currentTour = null
    state.value.currentStep = 0
    isTourActive.value = false
  }

  // Skip current tour
  function skipTour(): void {
    const tour = currentTourData.value
    if (!tour) return

    // Mark tour as skipped
    if (!state.value.skippedTours.includes(tour.id)) {
      state.value.skippedTours.push(tour.id)
    }

    // Call onSkip callback
    tour.onSkip?.()

    // Reset tour state
    state.value.currentTour = null
    state.value.currentStep = 0
    isTourActive.value = false
  }

  // Check if a tour is completed
  function isTourCompleted(tourId: string): boolean {
    return state.value.completedTours.includes(tourId)
  }

  // Check if a tour is skipped
  function isTourSkipped(tourId: string): boolean {
    return state.value.skippedTours.includes(tourId)
  }

  // Reset a specific tour
  function resetTour(tourId: string): void {
    state.value.completedTours = state.value.completedTours.filter(id => id !== tourId)
    state.value.skippedTours = state.value.skippedTours.filter(id => id !== tourId)
  }

  // Reset all onboarding
  function resetOnboarding(): void {
    state.value = DEFAULT_ONBOARDING_STATE
    saveState()
  }

  // Complete welcome modal
  function completeWelcome(): void {
    state.value.hasCompletedWelcome = true
    isWelcomeModalOpen.value = false
  }

  // Show welcome modal
  function showWelcome(): void {
    isWelcomeModalOpen.value = true
  }

  // Hide welcome modal
  function hideWelcome(): void {
    isWelcomeModalOpen.value = false
  }

  // Update preferences
  function updatePreferences(prefs: Partial<OnboardingPreferences>): void {
    state.value.preferences = { ...state.value.preferences, ...prefs }
  }

  // Check if should show onboarding
  function shouldShowOnboarding(): boolean {
    return !state.value.hasCompletedWelcome && state.value.preferences.showTipsOnStartup
  }

  // Register the main tour
  function registerMainTour(): void {
    registerTour({
      id: 'main',
      name: 'Getting Started',
      description: 'Learn the basics of Aegis',
      steps: MAIN_TOUR_STEPS,
      onComplete: () => {
        console.log('Main tour completed!')
      }
    })
  }

  return {
    // State
    state,
    hasCompletedWelcome,
    currentTour,
    currentStep,
    currentTourData,
    currentStepData,
    totalSteps,
    progress,
    isFirstStep,
    isLastStep,
    preferences,
    isWelcomeModalOpen,
    isTourActive,

    // Tour management
    registerTour,
    unregisterTour,
    startTour,
    nextStep,
    previousStep,
    goToStep,
    completeTour,
    skipTour,
    isTourCompleted,
    isTourSkipped,
    resetTour,
    resetOnboarding,

    // Welcome modal
    completeWelcome,
    showWelcome,
    hideWelcome,
    shouldShowOnboarding,

    // Preferences
    updatePreferences,

    // Helpers
    registerMainTour
  }
}
