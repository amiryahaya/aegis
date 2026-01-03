<script setup lang="ts">
import { computed, watch } from 'vue'
import {
  Dialog,
  DialogPanel,
  DialogTitle,
  TransitionRoot,
  TransitionChild
} from '@headlessui/vue'
import {
  XMarkIcon,
  DocumentArrowDownIcon,
  CheckIcon,
  ArrowLeftIcon,
  ArrowRightIcon,
  CodeBracketIcon,
  TableCellsIcon,
  DocumentIcon,
  DocumentTextIcon,
  GlobeAltIcon
} from '@heroicons/vue/24/outline'
import { CheckCircleIcon, ExclamationCircleIcon } from '@heroicons/vue/24/solid'
import { useExportDialog, useExport } from '@/composables/useExport'

const { state, closeDialog, selectFormat, nextStep, previousStep, setJob, updateOptions, availableFormats } = useExportDialog()
const { isExporting, exportProgress, startExport, downloadExport } = useExport()

const iconMap: Record<string, object> = {
  'code-bracket': CodeBracketIcon,
  'table-cells': TableCellsIcon,
  'document': DocumentIcon,
  'document-text': DocumentTextIcon,
  'globe-alt': GlobeAltIcon
}

function getIcon(iconName: string) {
  return iconMap[iconName] || DocumentIcon
}

const stepTitle = computed(() => {
  switch (state.value.step) {
    case 'format':
      return 'Choose Export Format'
    case 'options':
      return 'Export Options'
    case 'progress':
      return 'Exporting...'
    case 'complete':
      return 'Export Complete'
    default:
      return 'Export'
  }
})

const canProceed = computed(() => {
  switch (state.value.step) {
    case 'format':
      return state.value.selectedFormat !== null
    case 'options':
      return true
    default:
      return false
  }
})

async function handleExport() {
  if (!state.value.selectedFormat) return

  nextStep() // Move to progress step

  try {
    const job = await startExport({
      format: state.value.selectedFormat,
      resourceType: state.value.resourceType,
      resourceId: state.value.resourceId,
      resourceIds: state.value.resourceIds,
      includeMetadata: state.value.options.includeMetadata,
      includeTimestamps: state.value.options.includeTimestamps,
      dateRange: state.value.options.dateRange,
      fields: state.value.options.fields
    })

    setJob(job)
    nextStep() // Move to complete step
  } catch {
    // Error handling is done in useExport
  }
}

function handleDownload() {
  if (state.value.job) {
    downloadExport(state.value.job)
  }
}

function handleClose() {
  closeDialog()
}

// Auto-close after successful download
watch(() => state.value.isOpen, (isOpen) => {
  if (!isOpen) {
    // Reset state after close animation
    setTimeout(() => {
      state.value.step = 'format'
      state.value.selectedFormat = null
      state.value.job = null
    }, 300)
  }
})
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
              class="w-full max-w-md transform overflow-hidden rounded-xl bg-white p-6 shadow-xl transition-all dark:bg-gray-800"
            >
              <!-- Header -->
              <div class="flex items-center justify-between mb-4">
                <div class="flex items-center gap-3">
                  <div class="rounded-lg bg-aegis-100 p-2 dark:bg-aegis-900/30">
                    <DocumentArrowDownIcon class="h-5 w-5 text-aegis-600 dark:text-aegis-400" />
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

              <!-- Resource info -->
              <p v-if="state.resourceName" class="text-sm text-gray-500 dark:text-gray-400 mb-4">
                Exporting: {{ state.resourceName }}
              </p>

              <!-- Step: Format Selection -->
              <div v-if="state.step === 'format'" class="space-y-3">
                <button
                  v-for="format in availableFormats"
                  :key="format.value"
                  type="button"
                  class="w-full flex items-center gap-3 rounded-lg border-2 p-3 text-left transition-colors"
                  :class="[
                    state.selectedFormat === format.value
                      ? 'border-aegis-500 bg-aegis-50 dark:bg-aegis-900/20'
                      : 'border-gray-200 hover:border-gray-300 dark:border-gray-700 dark:hover:border-gray-600'
                  ]"
                  @click="selectFormat(format.value)"
                >
                  <div
                    class="rounded-lg p-2"
                    :class="[
                      state.selectedFormat === format.value
                        ? 'bg-aegis-100 dark:bg-aegis-900/40'
                        : 'bg-gray-100 dark:bg-gray-700'
                    ]"
                  >
                    <component
                      :is="getIcon(format.icon)"
                      class="h-5 w-5"
                      :class="[
                        state.selectedFormat === format.value
                          ? 'text-aegis-600 dark:text-aegis-400'
                          : 'text-gray-500 dark:text-gray-400'
                      ]"
                    />
                  </div>
                  <div class="flex-1">
                    <div class="font-medium text-gray-900 dark:text-white">
                      {{ format.label }}
                    </div>
                    <div class="text-sm text-gray-500 dark:text-gray-400">
                      {{ format.description }}
                    </div>
                  </div>
                  <CheckIcon
                    v-if="state.selectedFormat === format.value"
                    class="h-5 w-5 text-aegis-600 dark:text-aegis-400"
                  />
                </button>
              </div>

              <!-- Step: Options -->
              <div v-else-if="state.step === 'options'" class="space-y-4">
                <div class="space-y-3">
                  <label class="flex items-center gap-3">
                    <input
                      type="checkbox"
                      :checked="state.options.includeMetadata"
                      class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500"
                      @change="updateOptions({ includeMetadata: ($event.target as HTMLInputElement).checked })"
                    />
                    <div>
                      <span class="font-medium text-gray-900 dark:text-white">Include Metadata</span>
                      <p class="text-sm text-gray-500 dark:text-gray-400">
                        Add export date, format info, and resource details
                      </p>
                    </div>
                  </label>

                  <label class="flex items-center gap-3">
                    <input
                      type="checkbox"
                      :checked="state.options.includeTimestamps"
                      class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500"
                      @change="updateOptions({ includeTimestamps: ($event.target as HTMLInputElement).checked })"
                    />
                    <div>
                      <span class="font-medium text-gray-900 dark:text-white">Include Timestamps</span>
                      <p class="text-sm text-gray-500 dark:text-gray-400">
                        Add creation and modification dates to records
                      </p>
                    </div>
                  </label>
                </div>

                <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-700/50">
                  <div class="text-sm text-gray-600 dark:text-gray-300">
                    <strong>Format:</strong> {{ state.selectedFormat?.toUpperCase() }}
                  </div>
                  <div class="text-sm text-gray-600 dark:text-gray-300">
                    <strong>Resource:</strong> {{ state.resourceType }}
                  </div>
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
                    Preparing your {{ state.selectedFormat?.toUpperCase() }} export...
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
                      {{ state.job?.status === 'completed' ? 'Export Ready!' : 'Export Failed' }}
                    </h3>
                    <p v-if="state.job?.status === 'completed'" class="mt-1 text-sm text-gray-500 dark:text-gray-400">
                      {{ state.job?.fileName }}
                    </p>
                    <p v-else class="mt-1 text-sm text-red-600 dark:text-red-400">
                      {{ state.job?.error }}
                    </p>
                  </div>

                  <button
                    v-if="state.job?.status === 'completed'"
                    type="button"
                    class="btn-primary gap-2"
                    @click="handleDownload"
                  >
                    <DocumentArrowDownIcon class="h-5 w-5" />
                    Download
                  </button>
                </div>
              </div>

              <!-- Footer -->
              <div
                v-if="state.step === 'format' || state.step === 'options'"
                class="mt-6 flex justify-between"
              >
                <button
                  v-if="state.step === 'options'"
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
                  @click="state.step === 'options' ? handleExport() : nextStep()"
                >
                  {{ state.step === 'options' ? 'Export' : 'Next' }}
                  <ArrowRightIcon v-if="state.step === 'format'" class="h-4 w-4" />
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
