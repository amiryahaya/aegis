<script setup lang="ts">
import { ref, computed } from 'vue'
import { Disclosure, DisclosureButton, DisclosurePanel } from '@headlessui/vue'
import {
  EnvelopeIcon,
  PlusIcon,
  PencilIcon,
  TrashIcon,
  DocumentDuplicateIcon,
  ChevronDownIcon,
  EyeIcon,
  CodeBracketIcon,
  CheckCircleIcon,
  XCircleIcon
} from '@heroicons/vue/24/outline'
import type { EmailTemplate, CreateEmailTemplateRequest, UpdateEmailTemplateRequest, EmailTemplateCategory, TemplateVariable } from '@/types'
import { EMAIL_TEMPLATE_CATEGORIES } from '@/types/systemConfig'

const props = defineProps<{
  templates: EmailTemplate[]
  templatesByCategory: Record<EmailTemplateCategory, EmailTemplate[]>
  isLoading?: boolean
  isSaving?: boolean
}>()

const emit = defineEmits<{
  create: [request: CreateEmailTemplateRequest]
  update: [id: string, request: UpdateEmailTemplateRequest]
  delete: [id: string]
  duplicate: [id: string]
  toggleActive: [id: string, isActive: boolean]
}>()

const showDialog = ref(false)
const showPreviewDialog = ref(false)
const editingTemplate = ref<EmailTemplate | null>(null)
const previewTemplate = ref<EmailTemplate | null>(null)
const previewMode = ref<'html' | 'text'>('html')

const categories = Object.entries(EMAIL_TEMPLATE_CATEGORIES) as [EmailTemplateCategory, string][]

// Form state
const formData = ref<CreateEmailTemplateRequest>({
  name: '',
  slug: '',
  subject: '',
  htmlBody: '',
  textBody: '',
  category: 'notifications',
  variables: []
})

const newVariable = ref<TemplateVariable>({
  name: '',
  description: '',
  example: '',
  required: false
})

const activeCount = computed(() => props.templates.filter(t => t.isActive).length)

function formatVariable(name: string): string {
  return `{{${name}}}`
}

function openCreateDialog() {
  formData.value = {
    name: '',
    slug: '',
    subject: '',
    htmlBody: '',
    textBody: '',
    category: 'notifications',
    variables: []
  }
  editingTemplate.value = null
  showDialog.value = true
}

function openEditDialog(template: EmailTemplate) {
  formData.value = {
    name: template.name,
    slug: template.slug,
    subject: template.subject,
    htmlBody: template.htmlBody,
    textBody: template.textBody,
    category: template.category,
    variables: [...template.variables]
  }
  editingTemplate.value = template
  showDialog.value = true
}

function openPreviewDialog(template: EmailTemplate) {
  previewTemplate.value = template
  previewMode.value = 'html'
  showPreviewDialog.value = true
}

function handleSubmit() {
  if (editingTemplate.value) {
    emit('update', editingTemplate.value.id, {
      name: formData.value.name,
      subject: formData.value.subject,
      htmlBody: formData.value.htmlBody,
      textBody: formData.value.textBody,
      category: formData.value.category,
      variables: formData.value.variables
    })
  } else {
    emit('create', formData.value)
  }
  showDialog.value = false
}

function handleDelete(template: EmailTemplate) {
  if (template.isDefault) {
    alert('Cannot delete default templates')
    return
  }
  if (confirm(`Are you sure you want to delete "${template.name}"?`)) {
    emit('delete', template.id)
  }
}

function generateSlug(name: string): string {
  return name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '')
}

function addVariable() {
  if (newVariable.value.name.trim()) {
    formData.value.variables = [
      ...(formData.value.variables || []),
      { ...newVariable.value }
    ]
    newVariable.value = { name: '', description: '', example: '', required: false }
  }
}

function removeVariable(index: number) {
  formData.value.variables = formData.value.variables?.filter((_, i) => i !== index)
}

function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  })
}

function getCategoryColor(category: EmailTemplateCategory): string {
  const colors: Record<EmailTemplateCategory, string> = {
    authentication: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300',
    notifications: 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-300',
    alerts: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-300',
    reports: 'bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-300',
    invitations: 'bg-yellow-100 text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-300',
    system: 'bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-300'
  }
  return colors[category]
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div>
        <h3 class="text-lg font-medium text-gray-900 dark:text-white">Email Templates</h3>
        <p class="text-sm text-gray-500 dark:text-gray-400">
          {{ activeCount }} of {{ templates.length }} templates active
        </p>
      </div>
      <button
        type="button"
        class="inline-flex items-center gap-2 rounded-lg bg-aegis-600 px-4 py-2 text-sm font-medium text-white hover:bg-aegis-700"
        @click="openCreateDialog"
      >
        <PlusIcon class="h-4 w-4" />
        Add Template
      </button>
    </div>

    <!-- Loading state -->
    <div v-if="isLoading" class="animate-pulse space-y-4">
      <div v-for="i in 3" :key="i" class="h-32 rounded-lg bg-gray-200 dark:bg-gray-700" />
    </div>

    <!-- Templates by category -->
    <div v-else class="space-y-4">
      <Disclosure
        v-for="[category, label] in categories"
        :key="category"
        v-slot="{ open }"
        :default-open="templatesByCategory[category].length > 0"
      >
        <div class="rounded-lg border bg-white dark:border-gray-700 dark:bg-gray-800">
          <DisclosureButton
            class="flex w-full items-center justify-between px-4 py-3 text-left"
          >
            <div class="flex items-center gap-3">
              <span :class="['rounded px-2 py-1 text-xs font-medium', getCategoryColor(category)]">
                {{ label }}
              </span>
              <span class="text-sm text-gray-500 dark:text-gray-400">
                {{ templatesByCategory[category].length }} templates
              </span>
            </div>
            <ChevronDownIcon
              :class="[
                'h-5 w-5 text-gray-400 transition-transform',
                open && 'rotate-180'
              ]"
            />
          </DisclosureButton>

          <DisclosurePanel class="border-t px-4 pb-4 dark:border-gray-700">
            <div class="divide-y dark:divide-gray-700">
              <div
                v-for="template in templatesByCategory[category]"
                :key="template.id"
                class="flex items-center gap-4 py-4 first:pt-4"
              >
                <div class="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-lg bg-gray-100 dark:bg-gray-700">
                  <EnvelopeIcon class="h-5 w-5 text-gray-500 dark:text-gray-400" />
                </div>

                <div class="min-w-0 flex-1">
                  <div class="flex items-center gap-2">
                    <h4 class="font-medium text-gray-900 dark:text-white">{{ template.name }}</h4>
                    <span
                      v-if="template.isDefault"
                      class="rounded bg-gray-100 px-1.5 py-0.5 text-xs text-gray-600 dark:bg-gray-700 dark:text-gray-300"
                    >
                      Default
                    </span>
                    <component
                      :is="template.isActive ? CheckCircleIcon : XCircleIcon"
                      :class="[
                        'h-4 w-4',
                        template.isActive ? 'text-green-500' : 'text-gray-400'
                      ]"
                    />
                  </div>
                  <p class="mt-0.5 text-sm text-gray-500 dark:text-gray-400 truncate">
                    Subject: {{ template.subject }}
                  </p>
                  <div class="mt-1 flex items-center gap-4 text-xs text-gray-400">
                    <span>{{ template.variables.length }} variables</span>
                    <span>Updated {{ formatDate(template.updatedAt) }}</span>
                  </div>
                </div>

                <div class="flex items-center gap-1">
                  <button
                    type="button"
                    class="rounded p-1.5 text-gray-400 hover:bg-gray-100 hover:text-gray-600 dark:hover:bg-gray-700"
                    title="Preview"
                    @click="openPreviewDialog(template)"
                  >
                    <EyeIcon class="h-4 w-4" />
                  </button>
                  <button
                    type="button"
                    class="rounded p-1.5 text-gray-400 hover:bg-gray-100 hover:text-gray-600 dark:hover:bg-gray-700"
                    title="Duplicate"
                    @click="emit('duplicate', template.id)"
                  >
                    <DocumentDuplicateIcon class="h-4 w-4" />
                  </button>
                  <button
                    type="button"
                    class="rounded p-1.5 text-gray-400 hover:bg-gray-100 hover:text-gray-600 dark:hover:bg-gray-700"
                    title="Edit"
                    @click="openEditDialog(template)"
                  >
                    <PencilIcon class="h-4 w-4" />
                  </button>
                  <button
                    type="button"
                    class="rounded p-1.5 text-gray-400 hover:bg-red-50 hover:text-red-600 disabled:opacity-50 dark:hover:bg-red-900/20"
                    title="Delete"
                    :disabled="template.isDefault"
                    @click="handleDelete(template)"
                  >
                    <TrashIcon class="h-4 w-4" />
                  </button>
                </div>
              </div>

              <div
                v-if="templatesByCategory[category].length === 0"
                class="py-8 text-center text-sm text-gray-500 dark:text-gray-400"
              >
                No {{ label.toLowerCase() }} templates
              </div>
            </div>
          </DisclosurePanel>
        </div>
      </Disclosure>
    </div>

    <!-- Create/Edit Dialog -->
    <Teleport to="body">
      <div
        v-if="showDialog"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
        @click.self="showDialog = false"
      >
        <div class="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-lg bg-white p-6 shadow-xl dark:bg-gray-800">
          <h3 class="text-lg font-medium text-gray-900 dark:text-white">
            {{ editingTemplate ? 'Edit Email Template' : 'Create Email Template' }}
          </h3>

          <form class="mt-4 space-y-4" @submit.prevent="handleSubmit">
            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Name</label>
                <input
                  v-model="formData.name"
                  type="text"
                  required
                  class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                  placeholder="Welcome Email"
                  @input="!editingTemplate && (formData.slug = generateSlug(formData.name))"
                />
              </div>
              <div>
                <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Slug</label>
                <input
                  v-model="formData.slug"
                  type="text"
                  required
                  :disabled="!!editingTemplate"
                  class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 disabled:bg-gray-100 dark:border-gray-600 dark:bg-gray-700 dark:text-white dark:disabled:bg-gray-600"
                  placeholder="welcome-email"
                />
              </div>
            </div>

            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Category</label>
                <select
                  v-model="formData.category"
                  class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                >
                  <option v-for="[cat, label] in categories" :key="cat" :value="cat">
                    {{ label }}
                  </option>
                </select>
              </div>
              <div>
                <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Subject</label>
                <input
                  v-model="formData.subject"
                  type="text"
                  required
                  class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                  placeholder="Welcome to {{siteName}}!"
                />
              </div>
            </div>

            <div>
              <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">HTML Body</label>
              <textarea
                v-model="formData.htmlBody"
                rows="6"
                required
                class="w-full rounded-lg border-gray-300 font-mono text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                placeholder="<h1>Welcome!</h1><p>Hello {{userName}}...</p>"
              />
            </div>

            <div>
              <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Plain Text Body</label>
              <textarea
                v-model="formData.textBody"
                rows="3"
                class="w-full rounded-lg border-gray-300 font-mono text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                placeholder="Welcome! Hello {{userName}}..."
              />
            </div>

            <!-- Variables -->
            <div>
              <label class="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-300">Template Variables</label>
              <div class="space-y-2">
                <div
                  v-for="(variable, index) in formData.variables"
                  :key="index"
                  class="flex items-center gap-2 rounded bg-gray-50 p-2 dark:bg-gray-700"
                >
                  <code class="text-sm text-aegis-600 dark:text-aegis-400">{{ formatVariable(variable.name) }}</code>
                  <span class="flex-1 text-sm text-gray-500 dark:text-gray-400">{{ variable.description }}</span>
                  <span v-if="variable.required" class="text-xs text-red-500">Required</span>
                  <button
                    type="button"
                    class="text-gray-400 hover:text-red-500"
                    @click="removeVariable(index)"
                  >
                    <TrashIcon class="h-4 w-4" />
                  </button>
                </div>

                <div class="flex items-end gap-2">
                  <div class="flex-1">
                    <input
                      v-model="newVariable.name"
                      type="text"
                      class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                      placeholder="Variable name"
                    />
                  </div>
                  <div class="flex-1">
                    <input
                      v-model="newVariable.description"
                      type="text"
                      class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                      placeholder="Description"
                    />
                  </div>
                  <label class="flex items-center gap-1 text-sm">
                    <input v-model="newVariable.required" type="checkbox" class="rounded" />
                    Req
                  </label>
                  <button
                    type="button"
                    class="rounded-lg bg-gray-100 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-600 dark:text-gray-300 dark:hover:bg-gray-500"
                    @click="addVariable"
                  >
                    Add
                  </button>
                </div>
              </div>
            </div>

            <div class="flex justify-end gap-3 pt-4">
              <button
                type="button"
                class="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-700"
                @click="showDialog = false"
              >
                Cancel
              </button>
              <button
                type="submit"
                class="rounded-lg bg-aegis-600 px-4 py-2 text-sm font-medium text-white hover:bg-aegis-700 disabled:opacity-50"
                :disabled="isSaving"
              >
                {{ isSaving ? 'Saving...' : (editingTemplate ? 'Update' : 'Create') }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>

    <!-- Preview Dialog -->
    <Teleport to="body">
      <div
        v-if="showPreviewDialog && previewTemplate"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
        @click.self="showPreviewDialog = false"
      >
        <div class="max-h-[90vh] w-full max-w-3xl overflow-hidden rounded-lg bg-white shadow-xl dark:bg-gray-800">
          <div class="flex items-center justify-between border-b px-6 py-4 dark:border-gray-700">
            <h3 class="text-lg font-medium text-gray-900 dark:text-white">
              Preview: {{ previewTemplate.name }}
            </h3>
            <div class="flex items-center gap-2">
              <button
                type="button"
                :class="[
                  'rounded-lg px-3 py-1.5 text-sm font-medium',
                  previewMode === 'html'
                    ? 'bg-aegis-100 text-aegis-700 dark:bg-aegis-900/30 dark:text-aegis-300'
                    : 'text-gray-600 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700'
                ]"
                @click="previewMode = 'html'"
              >
                <EyeIcon class="mr-1 inline h-4 w-4" />
                Preview
              </button>
              <button
                type="button"
                :class="[
                  'rounded-lg px-3 py-1.5 text-sm font-medium',
                  previewMode === 'text'
                    ? 'bg-aegis-100 text-aegis-700 dark:bg-aegis-900/30 dark:text-aegis-300'
                    : 'text-gray-600 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700'
                ]"
                @click="previewMode = 'text'"
              >
                <CodeBracketIcon class="mr-1 inline h-4 w-4" />
                Source
              </button>
            </div>
          </div>

          <div class="max-h-[60vh] overflow-y-auto p-6">
            <div class="mb-4 rounded bg-gray-100 px-4 py-2 dark:bg-gray-700">
              <p class="text-sm text-gray-600 dark:text-gray-300">
                <span class="font-medium">Subject:</span> {{ previewTemplate.subject }}
              </p>
            </div>

            <div
              v-if="previewMode === 'html'"
              class="rounded border bg-white p-4 dark:border-gray-600 dark:bg-gray-900"
              v-html="previewTemplate.htmlBody"
            />
            <pre
              v-else
              class="overflow-x-auto rounded border bg-gray-50 p-4 text-sm dark:border-gray-600 dark:bg-gray-900"
            >{{ previewTemplate.htmlBody }}</pre>

            <div v-if="previewTemplate.variables.length > 0" class="mt-4">
              <h4 class="mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">Variables</h4>
              <div class="flex flex-wrap gap-2">
                <span
                  v-for="variable in previewTemplate.variables"
                  :key="variable.name"
                  class="rounded bg-gray-100 px-2 py-1 text-sm dark:bg-gray-700"
                >
                  <code class="text-aegis-600 dark:text-aegis-400">{{ formatVariable(variable.name) }}</code>
                  <span v-if="variable.required" class="ml-1 text-xs text-red-500">*</span>
                </span>
              </div>
            </div>
          </div>

          <div class="flex justify-end border-t px-6 py-4 dark:border-gray-700">
            <button
              type="button"
              class="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-700"
              @click="showPreviewDialog = false"
            >
              Close
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
