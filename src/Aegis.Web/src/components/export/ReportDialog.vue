<script setup lang="ts">
import { computed, ref } from 'vue'
import {
  Dialog,
  DialogPanel,
  DialogTitle,
  TransitionRoot,
  TransitionChild,
  RadioGroup,
  RadioGroupOption
} from '@headlessui/vue'
import {
  XMarkIcon,
  DocumentChartBarIcon,
  CheckIcon,
  ArrowLeftIcon,
  ArrowRightIcon,
  ChartBarIcon,
  ShieldCheckIcon,
  CogIcon,
  CalendarIcon,
  ClockIcon
} from '@heroicons/vue/24/outline'
import { CheckCircleIcon, ExclamationCircleIcon } from '@heroicons/vue/24/solid'
import type { FileFormat, ScheduleFrequency } from '@/types/export'
import { useReportDialog, useExport } from '@/composables/useExport'
import { EXPORT_FORMATS } from '@/types/export'

const {
  state,
  closeDialog,
  selectTemplate,
  nextStep,
  previousStep,
  setJob,
  updateFilters,
  toggleScheduled,
  templatesByCategory
} = useReportDialog()

const { isExporting, exportProgress, startExport } = useExport()

const selectedFormat = ref<FileFormat>('pdf')
const selectedFrequency = ref<ScheduleFrequency>('weekly')
const scheduleTime = ref('09:00')
const scheduleDay = ref(1) // Monday

const categoryIcons: Record<string, object> = {
  analytics: ChartBarIcon,
  compliance: ShieldCheckIcon,
  operations: CogIcon
}

const categoryLabels: Record<string, string> = {
  analytics: 'Analytics',
  compliance: 'Compliance',
  operations: 'Operations'
}

const frequencyOptions: { value: ScheduleFrequency; label: string }[] = [
  { value: 'daily', label: 'Daily' },
  { value: 'weekly', label: 'Weekly' },
  { value: 'monthly', label: 'Monthly' },
  { value: 'quarterly', label: 'Quarterly' }
]

const dayOfWeekOptions = [
  { value: 0, label: 'Sunday' },
  { value: 1, label: 'Monday' },
  { value: 2, label: 'Tuesday' },
  { value: 3, label: 'Wednesday' },
  { value: 4, label: 'Thursday' },
  { value: 5, label: 'Friday' },
  { value: 6, label: 'Saturday' }
]

const stepTitle = computed(() => {
  switch (state.value.step) {
    case 'template':
      return 'Choose Report Type'
    case 'filters':
      return 'Configure Report'
    case 'schedule':
      return 'Schedule Report'
    case 'progress':
      return 'Generating Report...'
    case 'complete':
      return 'Report Ready'
    default:
      return 'Generate Report'
  }
})

const canProceed = computed(() => {
  switch (state.value.step) {
    case 'template':
      return state.value.selectedTemplate !== null
    case 'filters':
      return state.value.filters.dateRange !== undefined
    case 'schedule':
      return true
    default:
      return false
  }
})

const availableFormats = computed(() => {
  if (!state.value.selectedTemplate) return EXPORT_FORMATS
  return EXPORT_FORMATS.filter(f =>
    state.value.selectedTemplate?.formats.includes(f.value)
  )
})

const dateRangeStart = computed(() => {
  const range = state.value.filters.dateRange as { start: Date; end: Date } | undefined
  return range?.start || new Date()
})

const dateRangeEnd = computed(() => {
  const range = state.value.filters.dateRange as { start: Date; end: Date } | undefined
  return range?.end || new Date()
})

async function handleGenerate() {
  if (!state.value.selectedTemplate) return

  nextStep() // Move to progress step

  try {
    const job = await startExport({
      format: selectedFormat.value,
      resourceType: 'analytics',
      includeMetadata: true,
      includeTimestamps: true,
      dateRange: state.value.filters.dateRange as { start: Date; end: Date },
      filters: state.value.filters
    })

    setJob(job)
    nextStep() // Move to complete step
  } catch {
    // Error handling is done in useExport
  }
}

function handleClose() {
  closeDialog()
}

function formatDateForInput(date: Date): string {
  return date.toISOString().split('T')[0]
}

function handleStartDateChange(event: Event) {
  const target = event.target as HTMLInputElement
  const dateRange = state.value.filters.dateRange as { start: Date; end: Date } | undefined
  if (dateRange) {
    updateFilters({
      dateRange: {
        start: new Date(target.value),
        end: dateRange.end
      }
    })
  }
}

function handleEndDateChange(event: Event) {
  const target = event.target as HTMLInputElement
  const dateRange = state.value.filters.dateRange as { start: Date; end: Date } | undefined
  if (dateRange) {
    updateFilters({
      dateRange: {
        start: dateRange.start,
        end: new Date(target.value)
      }
    })
  }
}
</script>

<template>
  <TransitionRoot appear :show="state.isOpen" as="template">
    <Dialog as="div" class="relative z-50" @close="handleClose">
      <TransitionChild
        as="template"
        enter="duration-300 ease-out"
        enter-from="opacity-0"
        enter-to="opacity-100"
        leave="duration-200 ease-in"
        leave-from="opacity-100"
        leave-to="opacity-0"
      >
        <div class="fixed inset-0 bg-black/25 dark:bg-black/50" />
      </TransitionChild>

      <div class="fixed inset-0 overflow-y-auto">
        <div class="flex min-h-full items-center justify-center p-4">
          <TransitionChild
            as="template"
            enter="duration-300 ease-out"
            enter-from="opacity-0 scale-95"
            enter-to="opacity-100 scale-100"
            leave="duration-200 ease-in"
            leave-from="opacity-100 scale-100"
            leave-to="opacity-0 scale-95"
          >
            <DialogPanel
              class="w-full max-w-lg transform overflow-hidden rounded-xl bg-white p-6 shadow-xl transition-all dark:bg-gray-800"
            >
              <!-- Header -->
              <div class="flex items-center justify-between mb-4">
                <div class="flex items-center gap-3">
                  <div class="rounded-lg bg-aegis-100 p-2 dark:bg-aegis-900/30">
                    <DocumentChartBarIcon class="h-5 w-5 text-aegis-600 dark:text-aegis-400" />
                  </div>
                  <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-white">
                    {{ stepTitle }}
                  </DialogTitle>
                </div>
                <button
                  type="button"
                  class="rounded-lg p-1.5 text-gray-400 hover:bg-gray-100 hover:text-gray-500 dark:hover:bg-gray-700"
                  @click="handleClose"
                >
                  <XMarkIcon class="h-5 w-5" />
                </button>
              </div>

              <!-- Step: Template Selection -->
              <div v-if="state.step === 'template'" class="space-y-4">
                <div
                  v-for="(templates, category) in templatesByCategory"
                  :key="category"
                  class="space-y-2"
                >
                  <div class="flex items-center gap-2 text-sm font-medium text-gray-500 dark:text-gray-400">
                    <component :is="categoryIcons[category]" class="h-4 w-4" />
                    {{ categoryLabels[category] }}
                  </div>
                  <div class="grid gap-2">
                    <button
                      v-for="template in templates"
                      :key="template.id"
                      type="button"
                      class="flex items-start gap-3 rounded-lg border-2 p-3 text-left transition-colors"
                      :class="[
                        state.selectedTemplate?.id === template.id
                          ? 'border-aegis-500 bg-aegis-50 dark:bg-aegis-900/20'
                          : 'border-gray-200 hover:border-gray-300 dark:border-gray-700 dark:hover:border-gray-600'
                      ]"
                      @click="selectTemplate(template)"
                    >
                      <div class="flex-1">
                        <div class="font-medium text-gray-900 dark:text-white">
                          {{ template.name }}
                        </div>
                        <div class="text-sm text-gray-500 dark:text-gray-400">
                          {{ template.description }}
                        </div>
                        <div class="mt-1 flex gap-1">
                          <span
                            v-for="format in template.formats"
                            :key="format"
                            class="inline-flex rounded px-1.5 py-0.5 text-xs font-medium bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-300"
                          >
                            {{ format.toUpperCase() }}
                          </span>
                        </div>
                      </div>
                      <CheckIcon
                        v-if="state.selectedTemplate?.id === template.id"
                        class="h-5 w-5 text-aegis-600 dark:text-aegis-400 shrink-0"
                      />
                    </button>
                  </div>
                </div>
              </div>

              <!-- Step: Filters -->
              <div v-else-if="state.step === 'filters'" class="space-y-4">
                <!-- Date Range -->
                <div>
                  <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                    <CalendarIcon class="inline h-4 w-4 mr-1" />
                    Date Range
                  </label>
                  <div class="grid grid-cols-2 gap-3">
                    <div>
                      <label class="block text-xs text-gray-500 dark:text-gray-400 mb-1">
                        Start Date
                      </label>
                      <input
                        type="date"
                        :value="formatDateForInput(dateRangeStart)"
                        class="block w-full rounded-lg border-gray-300 text-sm shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                        @change="handleStartDateChange"
                      />
                    </div>
                    <div>
                      <label class="block text-xs text-gray-500 dark:text-gray-400 mb-1">
                        End Date
                      </label>
                      <input
                        type="date"
                        :value="formatDateForInput(dateRangeEnd)"
                        class="block w-full rounded-lg border-gray-300 text-sm shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                        @change="handleEndDateChange"
                      />
                    </div>
                  </div>
                </div>

                <!-- Format Selection -->
                <div>
                  <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                    Export Format
                  </label>
                  <RadioGroup v-model="selectedFormat" class="grid grid-cols-4 gap-2">
                    <RadioGroupOption
                      v-for="format in availableFormats"
                      :key="format.value"
                      v-slot="{ checked }"
                      :value="format.value"
                    >
                      <div
                        class="cursor-pointer rounded-lg border-2 p-2 text-center text-sm font-medium transition-colors"
                        :class="[
                          checked
                            ? 'border-aegis-500 bg-aegis-50 text-aegis-700 dark:bg-aegis-900/20 dark:text-aegis-300'
                            : 'border-gray-200 text-gray-700 hover:border-gray-300 dark:border-gray-700 dark:text-gray-300 dark:hover:border-gray-600'
                        ]"
                      >
                        {{ format.label }}
                      </div>
                    </RadioGroupOption>
                  </RadioGroup>
                </div>

                <!-- Schedule Toggle -->
                <div class="flex items-center gap-3 pt-2">
                  <input
                    :checked="state.isScheduled"
                    type="checkbox"
                    class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500"
                    @change="toggleScheduled(($event.target as HTMLInputElement).checked)"
                  />
                  <div>
                    <span class="font-medium text-gray-900 dark:text-white">
                      <ClockIcon class="inline h-4 w-4 mr-1" />
                      Schedule this report
                    </span>
                    <p class="text-sm text-gray-500 dark:text-gray-400">
                      Automatically generate and send this report on a recurring basis
                    </p>
                  </div>
                </div>
              </div>

              <!-- Step: Schedule -->
              <div v-else-if="state.step === 'schedule'" class="space-y-4">
                <!-- Frequency -->
                <div>
                  <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                    Frequency
                  </label>
                  <RadioGroup v-model="selectedFrequency" class="grid grid-cols-4 gap-2">
                    <RadioGroupOption
                      v-for="option in frequencyOptions"
                      :key="option.value"
                      v-slot="{ checked }"
                      :value="option.value"
                    >
                      <div
                        class="cursor-pointer rounded-lg border-2 p-2 text-center text-sm font-medium transition-colors"
                        :class="[
                          checked
                            ? 'border-aegis-500 bg-aegis-50 text-aegis-700 dark:bg-aegis-900/20 dark:text-aegis-300'
                            : 'border-gray-200 text-gray-700 hover:border-gray-300 dark:border-gray-700 dark:text-gray-300 dark:hover:border-gray-600'
                        ]"
                      >
                        {{ option.label }}
                      </div>
                    </RadioGroupOption>
                  </RadioGroup>
                </div>

                <!-- Day of Week (for weekly) -->
                <div v-if="selectedFrequency === 'weekly'">
                  <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                    Day of Week
                  </label>
                  <select
                    v-model="scheduleDay"
                    class="block w-full rounded-lg border-gray-300 text-sm shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                  >
                    <option
                      v-for="day in dayOfWeekOptions"
                      :key="day.value"
                      :value="day.value"
                    >
                      {{ day.label }}
                    </option>
                  </select>
                </div>

                <!-- Time -->
                <div>
                  <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                    Time
                  </label>
                  <input
                    v-model="scheduleTime"
                    type="time"
                    class="block w-full rounded-lg border-gray-300 text-sm shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                  />
                </div>

                <!-- Summary -->
                <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-700/50">
                  <p class="text-sm text-gray-600 dark:text-gray-300">
                    This report will be generated
                    <strong>{{ selectedFrequency }}</strong>
                    <span v-if="selectedFrequency === 'weekly'">
                      on <strong>{{ dayOfWeekOptions.find(d => d.value === scheduleDay)?.label }}</strong>
                    </span>
                    at <strong>{{ scheduleTime }}</strong>
                  </p>
                </div>
              </div>

              <!-- Step: Progress -->
              <div v-else-if="state.step === 'progress'" class="py-8">
                <div class="flex flex-col items-center gap-4">
                  <div class="relative">
                    <svg class="h-20 w-20 -rotate-90 transform">
                      <circle
                        cx="40"
                        cy="40"
                        r="36"
                        stroke-width="8"
                        fill="none"
                        class="stroke-gray-200 dark:stroke-gray-700"
                      />
                      <circle
                        cx="40"
                        cy="40"
                        r="36"
                        stroke-width="8"
                        fill="none"
                        stroke-linecap="round"
                        class="stroke-aegis-500 transition-all duration-300"
                        :stroke-dasharray="226.2"
                        :stroke-dashoffset="226.2 - (exportProgress / 100) * 226.2"
                      />
                    </svg>
                    <div class="absolute inset-0 flex items-center justify-center">
                      <span class="text-xl font-semibold text-gray-900 dark:text-white">
                        {{ exportProgress }}%
                      </span>
                    </div>
                  </div>
                  <p class="text-sm text-gray-500 dark:text-gray-400">
                    Generating {{ state.selectedTemplate?.name }}...
                  </p>
                </div>
              </div>

              <!-- Step: Complete -->
              <div v-else-if="state.step === 'complete'" class="py-6">
                <div class="flex flex-col items-center gap-4 text-center">
                  <div
                    v-if="state.job?.status === 'completed'"
                    class="rounded-full bg-green-100 p-3 dark:bg-green-900/30"
                  >
                    <CheckCircleIcon class="h-8 w-8 text-green-600 dark:text-green-400" />
                  </div>
                  <div v-else class="rounded-full bg-red-100 p-3 dark:bg-red-900/30">
                    <ExclamationCircleIcon class="h-8 w-8 text-red-600 dark:text-red-400" />
                  </div>

                  <div>
                    <h3 class="font-medium text-gray-900 dark:text-white">
                      {{ state.job?.status === 'completed' ? 'Report Ready!' : 'Report Failed' }}
                    </h3>
                    <p v-if="state.job?.status === 'completed'" class="mt-1 text-sm text-gray-500 dark:text-gray-400">
                      {{ state.job?.fileName }}
                    </p>
                    <p v-else class="mt-1 text-sm text-red-600 dark:text-red-400">
                      {{ state.job?.error }}
                    </p>
                  </div>
                </div>
              </div>

              <!-- Footer -->
              <div
                v-if="state.step !== 'progress' && state.step !== 'complete'"
                class="mt-6 flex justify-between"
              >
                <button
                  v-if="state.step !== 'template'"
                  type="button"
                  class="btn-secondary gap-1"
                  @click="previousStep"
                >
                  <ArrowLeftIcon class="h-4 w-4" />
                  Back
                </button>
                <div v-else />

                <button
                  type="button"
                  class="btn-primary gap-1"
                  :disabled="!canProceed || isExporting"
                  @click="
                    state.step === 'filters' && !state.isScheduled
                      ? handleGenerate()
                      : state.step === 'schedule'
                        ? handleGenerate()
                        : nextStep()
                  "
                >
                  {{
                    state.step === 'filters' && !state.isScheduled
                      ? 'Generate Report'
                      : state.step === 'schedule'
                        ? 'Schedule & Generate'
                        : 'Next'
                  }}
                  <ArrowRightIcon v-if="state.step === 'template'" class="h-4 w-4" />
                </button>
              </div>

              <!-- Close button for complete step -->
              <div v-if="state.step === 'complete'" class="mt-6 flex justify-center">
                <button type="button" class="btn-secondary" @click="handleClose">
                  Close
                </button>
              </div>
            </DialogPanel>
          </TransitionChild>
        </div>
      </div>
    </Dialog>
  </TransitionRoot>
</template>
