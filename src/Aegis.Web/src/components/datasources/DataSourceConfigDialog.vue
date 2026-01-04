<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useToast } from '@/composables/useToast'
import type { DataSourceType, DataSourceConfig } from '@/types/workspace'
import {
  XMarkIcon,
  GlobeAltIcon,
  CircleStackIcon,
  CloudIcon,
  DocumentTextIcon,
  LinkIcon,
  ServerIcon,
  ArrowPathIcon,
  EyeIcon,
  EyeSlashIcon
} from '@heroicons/vue/24/outline'
import {
  Dialog,
  DialogPanel,
  DialogTitle,
  TransitionChild,
  TransitionRoot,
  Listbox,
  ListboxButton,
  ListboxOptions,
  ListboxOption
} from '@headlessui/vue'

interface DataSourceTypeOption {
  type: DataSourceType
  label: string
  description: string
  icon: typeof GlobeAltIcon
  color: string
}

const props = defineProps<{
  open: boolean
  workspaceId: string
  editMode?: boolean
  dataSource?: {
    id: string
    name: string
    type: DataSourceType
    config: DataSourceConfig
  }
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'save', data: { name: string; type: DataSourceType; config: DataSourceConfig }): void
}>()

const toast = useToast()

const name = ref('')
const selectedType = ref<DataSourceType>('WebCrawler')
const saving = ref(false)
const showSecrets = ref<Record<string, boolean>>({})

// Type-specific config
const webCrawlerConfig = ref({
  url: '',
  maxDepth: 3,
  respectRobotsTxt: true,
  crawlFrequency: 'daily' as const
})

const databaseConfig = ref({
  connectionString: '',
  databaseType: 'PostgreSQL' as const,
  tableName: '',
  batchSize: 1000
})

const sharePointConfig = ref({
  siteUrl: '',
  clientId: '',
  clientSecret: '',
  tenantId: '',
  libraryName: ''
})

const googleDriveConfig = ref({
  folderId: '',
  serviceAccountKey: '',
  includeSharedDrives: false
})

const confluenceConfig = ref({
  baseUrl: '',
  username: '',
  apiToken: '',
  spaceKey: '',
  includeAttachments: true
})

const notionConfig = ref({
  integrationToken: '',
  databaseId: ''
})

const s3Config = ref({
  bucketName: '',
  region: 'us-east-1',
  accessKeyId: '',
  secretAccessKey: '',
  prefix: ''
})

const azureBlobConfig = ref({
  connectionString: '',
  containerName: '',
  sasToken: '',
  prefix: ''
})

const dataSourceTypes: DataSourceTypeOption[] = [
  { type: 'WebCrawler', label: 'Web Crawler', description: 'Crawl and index web pages', icon: GlobeAltIcon, color: 'text-blue-500 bg-blue-100 dark:bg-blue-900/50' },
  { type: 'Database', label: 'Database', description: 'Connect to SQL or NoSQL databases', icon: CircleStackIcon, color: 'text-purple-500 bg-purple-100 dark:bg-purple-900/50' },
  { type: 'SharePoint', label: 'SharePoint', description: 'Microsoft SharePoint libraries', icon: CloudIcon, color: 'text-cyan-500 bg-cyan-100 dark:bg-cyan-900/50' },
  { type: 'GoogleDrive', label: 'Google Drive', description: 'Google Drive folders and files', icon: CloudIcon, color: 'text-green-500 bg-green-100 dark:bg-green-900/50' },
  { type: 'Confluence', label: 'Confluence', description: 'Atlassian Confluence spaces', icon: DocumentTextIcon, color: 'text-blue-500 bg-blue-100 dark:bg-blue-900/50' },
  { type: 'Notion', label: 'Notion', description: 'Notion databases and pages', icon: DocumentTextIcon, color: 'text-gray-500 bg-gray-100 dark:bg-gray-900/50' },
  { type: 'S3', label: 'Amazon S3', description: 'AWS S3 buckets', icon: ServerIcon, color: 'text-orange-500 bg-orange-100 dark:bg-orange-900/50' },
  { type: 'AzureBlob', label: 'Azure Blob', description: 'Azure Blob Storage containers', icon: CloudIcon, color: 'text-blue-500 bg-blue-100 dark:bg-blue-900/50' }
]

const selectedTypeOption = computed(() =>
  dataSourceTypes.find(t => t.type === selectedType.value) || dataSourceTypes[0]
)

const crawlFrequencies = [
  { value: 'hourly', label: 'Every hour' },
  { value: 'daily', label: 'Every day' },
  { value: 'weekly', label: 'Every week' },
  { value: 'monthly', label: 'Every month' }
]

const databaseTypes = [
  { value: 'PostgreSQL', label: 'PostgreSQL' },
  { value: 'MySQL', label: 'MySQL' },
  { value: 'SQLServer', label: 'SQL Server' },
  { value: 'MongoDB', label: 'MongoDB' }
]

const awsRegions = [
  { value: 'us-east-1', label: 'US East (N. Virginia)' },
  { value: 'us-west-2', label: 'US West (Oregon)' },
  { value: 'eu-west-1', label: 'EU (Ireland)' },
  { value: 'ap-southeast-1', label: 'Asia Pacific (Singapore)' }
]

// Watch for edit mode changes
watch(() => props.open, (isOpen) => {
  if (isOpen && props.editMode && props.dataSource) {
    name.value = props.dataSource.name
    selectedType.value = props.dataSource.type
    loadConfig(props.dataSource.config)
  } else if (isOpen && !props.editMode) {
    resetForm()
  }
}, { immediate: true })

function loadConfig(config: DataSourceConfig) {
  if (config.url) webCrawlerConfig.value.url = config.url
  if (config.connectionString) databaseConfig.value.connectionString = config.connectionString
  // Load other type-specific configs as needed
}

function resetForm() {
  name.value = ''
  selectedType.value = 'WebCrawler'
  webCrawlerConfig.value = { url: '', maxDepth: 3, respectRobotsTxt: true, crawlFrequency: 'daily' }
  databaseConfig.value = { connectionString: '', databaseType: 'PostgreSQL', tableName: '', batchSize: 1000 }
  sharePointConfig.value = { siteUrl: '', clientId: '', clientSecret: '', tenantId: '', libraryName: '' }
  googleDriveConfig.value = { folderId: '', serviceAccountKey: '', includeSharedDrives: false }
  confluenceConfig.value = { baseUrl: '', username: '', apiToken: '', spaceKey: '', includeAttachments: true }
  notionConfig.value = { integrationToken: '', databaseId: '' }
  s3Config.value = { bucketName: '', region: 'us-east-1', accessKeyId: '', secretAccessKey: '', prefix: '' }
  azureBlobConfig.value = { connectionString: '', containerName: '', sasToken: '', prefix: '' }
  showSecrets.value = {}
}

function buildConfig(): DataSourceConfig {
  const baseConfig: DataSourceConfig = {}

  switch (selectedType.value) {
    case 'WebCrawler':
      return { ...baseConfig, url: webCrawlerConfig.value.url }
    case 'Database':
      return { ...baseConfig, connectionString: databaseConfig.value.connectionString }
    case 'SharePoint':
      return { ...baseConfig, url: sharePointConfig.value.siteUrl }
    case 'GoogleDrive':
      return { ...baseConfig }
    case 'Confluence':
      return { ...baseConfig, url: confluenceConfig.value.baseUrl }
    case 'Notion':
      return { ...baseConfig }
    case 'S3':
      return { ...baseConfig }
    case 'AzureBlob':
      return { ...baseConfig, connectionString: azureBlobConfig.value.connectionString }
    default:
      return baseConfig
  }
}

const isValid = computed(() => {
  if (!name.value.trim()) return false

  switch (selectedType.value) {
    case 'WebCrawler':
      return !!webCrawlerConfig.value.url
    case 'Database':
      return !!databaseConfig.value.connectionString
    case 'SharePoint':
      return !!sharePointConfig.value.siteUrl && !!sharePointConfig.value.clientId && !!sharePointConfig.value.tenantId
    case 'GoogleDrive':
      return true
    case 'Confluence':
      return !!confluenceConfig.value.baseUrl && !!confluenceConfig.value.username
    case 'Notion':
      return !!notionConfig.value.integrationToken
    case 'S3':
      return !!s3Config.value.bucketName && !!s3Config.value.region
    case 'AzureBlob':
      return !!azureBlobConfig.value.containerName
    default:
      return true
  }
})

async function handleSave() {
  if (!isValid.value) {
    toast.error('Validation Error', 'Please fill in all required fields')
    return
  }

  saving.value = true
  try {
    emit('save', {
      name: name.value.trim(),
      type: selectedType.value,
      config: buildConfig()
    })
  } finally {
    saving.value = false
  }
}

function toggleSecret(field: string) {
  showSecrets.value[field] = !showSecrets.value[field]
}
</script>

<template>
  <TransitionRoot appear :show="open" as="template">
    <Dialog as="div" class="relative z-50" @close="!saving && emit('close')">
      <TransitionChild
        enter="duration-300 ease-out"
        enter-from="opacity-0"
        enter-to="opacity-100"
        leave="duration-200 ease-in"
        leave-from="opacity-100"
        leave-to="opacity-0"
      >
        <div class="fixed inset-0 bg-black/30 backdrop-blur-sm" />
      </TransitionChild>

      <div class="fixed inset-0 overflow-y-auto">
        <div class="flex min-h-full items-center justify-center p-4">
          <TransitionChild
            enter="duration-300 ease-out"
            enter-from="opacity-0 scale-95"
            enter-to="opacity-100 scale-100"
            leave="duration-200 ease-in"
            leave-from="opacity-100 scale-100"
            leave-to="opacity-0 scale-95"
          >
            <DialogPanel class="w-full max-w-2xl transform rounded-xl bg-white p-6 shadow-xl transition-all dark:bg-gray-800">
              <div class="flex items-center justify-between mb-6">
                <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-white">
                  {{ editMode ? 'Edit Data Source' : 'Add Data Source' }}
                </DialogTitle>
                <button
                  v-if="!saving"
                  class="rounded-lg p-1 text-gray-400 hover:text-gray-500 dark:hover:text-gray-300"
                  @click="emit('close')"
                >
                  <XMarkIcon class="h-6 w-6" />
                </button>
              </div>

              <div class="space-y-6">
                <!-- Name -->
                <div>
                  <label class="label">Name <span class="text-red-500">*</span></label>
                  <input
                    v-model="name"
                    type="text"
                    class="input w-full"
                    placeholder="My Data Source"
                  />
                </div>

                <!-- Type Selection -->
                <div v-if="!editMode">
                  <label class="label">Type <span class="text-red-500">*</span></label>
                  <Listbox v-model="selectedType">
                    <div class="relative">
                      <ListboxButton class="input w-full text-left flex items-center gap-3">
                        <div class="rounded-lg p-1.5" :class="selectedTypeOption.color">
                          <component :is="selectedTypeOption.icon" class="h-5 w-5" />
                        </div>
                        <div class="flex-1">
                          <p class="font-medium text-gray-900 dark:text-white">
                            {{ selectedTypeOption.label }}
                          </p>
                          <p class="text-sm text-gray-500 dark:text-gray-400">
                            {{ selectedTypeOption.description }}
                          </p>
                        </div>
                      </ListboxButton>
                      <ListboxOptions class="absolute z-10 mt-1 w-full rounded-lg border border-gray-200 bg-white py-1 shadow-lg dark:border-gray-700 dark:bg-gray-800">
                        <ListboxOption
                          v-for="type in dataSourceTypes"
                          :key="type.type"
                          :value="type.type"
                          v-slot="{ active, selected }"
                        >
                          <div
                            class="flex items-center gap-3 px-4 py-3 cursor-pointer"
                            :class="[
                              active ? 'bg-gray-100 dark:bg-gray-700' : '',
                              selected ? 'bg-aegis-50 dark:bg-aegis-900/20' : ''
                            ]"
                          >
                            <div class="rounded-lg p-1.5" :class="type.color">
                              <component :is="type.icon" class="h-5 w-5" />
                            </div>
                            <div>
                              <p class="font-medium text-gray-900 dark:text-white">
                                {{ type.label }}
                              </p>
                              <p class="text-sm text-gray-500 dark:text-gray-400">
                                {{ type.description }}
                              </p>
                            </div>
                          </div>
                        </ListboxOption>
                      </ListboxOptions>
                    </div>
                  </Listbox>
                </div>

                <!-- Type-specific Configuration -->
                <div class="border-t border-gray-200 pt-6 dark:border-gray-700">
                  <h3 class="text-sm font-medium text-gray-900 dark:text-white mb-4">Configuration</h3>

                  <!-- Web Crawler -->
                  <div v-if="selectedType === 'WebCrawler'" class="space-y-4">
                    <div>
                      <label class="label">URL <span class="text-red-500">*</span></label>
                      <input
                        v-model="webCrawlerConfig.url"
                        type="url"
                        class="input w-full"
                        placeholder="https://example.com"
                      />
                    </div>
                    <div class="grid grid-cols-2 gap-4">
                      <div>
                        <label class="label">Max Depth</label>
                        <input
                          v-model.number="webCrawlerConfig.maxDepth"
                          type="number"
                          min="1"
                          max="10"
                          class="input w-full"
                        />
                      </div>
                      <div>
                        <label class="label">Crawl Frequency</label>
                        <select v-model="webCrawlerConfig.crawlFrequency" class="input w-full">
                          <option v-for="freq in crawlFrequencies" :key="freq.value" :value="freq.value">
                            {{ freq.label }}
                          </option>
                        </select>
                      </div>
                    </div>
                    <div class="flex items-center gap-2">
                      <input
                        v-model="webCrawlerConfig.respectRobotsTxt"
                        type="checkbox"
                        id="robotsTxt"
                        class="rounded border-gray-300 text-aegis-600"
                      />
                      <label for="robotsTxt" class="text-sm text-gray-700 dark:text-gray-300">
                        Respect robots.txt
                      </label>
                    </div>
                  </div>

                  <!-- Database -->
                  <div v-else-if="selectedType === 'Database'" class="space-y-4">
                    <div>
                      <label class="label">Database Type</label>
                      <select v-model="databaseConfig.databaseType" class="input w-full">
                        <option v-for="db in databaseTypes" :key="db.value" :value="db.value">
                          {{ db.label }}
                        </option>
                      </select>
                    </div>
                    <div>
                      <label class="label">Connection String <span class="text-red-500">*</span></label>
                      <div class="relative">
                        <input
                          v-model="databaseConfig.connectionString"
                          :type="showSecrets['connectionString'] ? 'text' : 'password'"
                          class="input w-full pr-10"
                          placeholder="Host=localhost;Database=mydb;Username=user;Password=pass"
                        />
                        <button
                          type="button"
                          class="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                          @click="toggleSecret('connectionString')"
                        >
                          <EyeSlashIcon v-if="showSecrets['connectionString']" class="h-5 w-5" />
                          <EyeIcon v-else class="h-5 w-5" />
                        </button>
                      </div>
                    </div>
                    <div class="grid grid-cols-2 gap-4">
                      <div>
                        <label class="label">Table Name</label>
                        <input
                          v-model="databaseConfig.tableName"
                          type="text"
                          class="input w-full"
                          placeholder="documents"
                        />
                      </div>
                      <div>
                        <label class="label">Batch Size</label>
                        <input
                          v-model.number="databaseConfig.batchSize"
                          type="number"
                          min="100"
                          max="10000"
                          class="input w-full"
                        />
                      </div>
                    </div>
                  </div>

                  <!-- SharePoint -->
                  <div v-else-if="selectedType === 'SharePoint'" class="space-y-4">
                    <div>
                      <label class="label">Site URL <span class="text-red-500">*</span></label>
                      <input
                        v-model="sharePointConfig.siteUrl"
                        type="url"
                        class="input w-full"
                        placeholder="https://contoso.sharepoint.com/sites/mysite"
                      />
                    </div>
                    <div class="grid grid-cols-2 gap-4">
                      <div>
                        <label class="label">Client ID <span class="text-red-500">*</span></label>
                        <input
                          v-model="sharePointConfig.clientId"
                          type="text"
                          class="input w-full"
                        />
                      </div>
                      <div>
                        <label class="label">Tenant ID <span class="text-red-500">*</span></label>
                        <input
                          v-model="sharePointConfig.tenantId"
                          type="text"
                          class="input w-full"
                        />
                      </div>
                    </div>
                    <div>
                      <label class="label">Client Secret</label>
                      <div class="relative">
                        <input
                          v-model="sharePointConfig.clientSecret"
                          :type="showSecrets['clientSecret'] ? 'text' : 'password'"
                          class="input w-full pr-10"
                        />
                        <button
                          type="button"
                          class="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                          @click="toggleSecret('clientSecret')"
                        >
                          <EyeSlashIcon v-if="showSecrets['clientSecret']" class="h-5 w-5" />
                          <EyeIcon v-else class="h-5 w-5" />
                        </button>
                      </div>
                    </div>
                    <div>
                      <label class="label">Library Name</label>
                      <input
                        v-model="sharePointConfig.libraryName"
                        type="text"
                        class="input w-full"
                        placeholder="Documents"
                      />
                    </div>
                  </div>

                  <!-- S3 -->
                  <div v-else-if="selectedType === 'S3'" class="space-y-4">
                    <div class="grid grid-cols-2 gap-4">
                      <div>
                        <label class="label">Bucket Name <span class="text-red-500">*</span></label>
                        <input
                          v-model="s3Config.bucketName"
                          type="text"
                          class="input w-full"
                          placeholder="my-bucket"
                        />
                      </div>
                      <div>
                        <label class="label">Region <span class="text-red-500">*</span></label>
                        <select v-model="s3Config.region" class="input w-full">
                          <option v-for="region in awsRegions" :key="region.value" :value="region.value">
                            {{ region.label }}
                          </option>
                        </select>
                      </div>
                    </div>
                    <div class="grid grid-cols-2 gap-4">
                      <div>
                        <label class="label">Access Key ID</label>
                        <input
                          v-model="s3Config.accessKeyId"
                          type="text"
                          class="input w-full"
                        />
                      </div>
                      <div>
                        <label class="label">Secret Access Key</label>
                        <div class="relative">
                          <input
                            v-model="s3Config.secretAccessKey"
                            :type="showSecrets['secretAccessKey'] ? 'text' : 'password'"
                            class="input w-full pr-10"
                          />
                          <button
                            type="button"
                            class="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                            @click="toggleSecret('secretAccessKey')"
                          >
                            <EyeSlashIcon v-if="showSecrets['secretAccessKey']" class="h-5 w-5" />
                            <EyeIcon v-else class="h-5 w-5" />
                          </button>
                        </div>
                      </div>
                    </div>
                    <div>
                      <label class="label">Prefix</label>
                      <input
                        v-model="s3Config.prefix"
                        type="text"
                        class="input w-full"
                        placeholder="documents/"
                      />
                    </div>
                  </div>

                  <!-- Azure Blob -->
                  <div v-else-if="selectedType === 'AzureBlob'" class="space-y-4">
                    <div>
                      <label class="label">Container Name <span class="text-red-500">*</span></label>
                      <input
                        v-model="azureBlobConfig.containerName"
                        type="text"
                        class="input w-full"
                        placeholder="my-container"
                      />
                    </div>
                    <div>
                      <label class="label">Connection String</label>
                      <div class="relative">
                        <input
                          v-model="azureBlobConfig.connectionString"
                          :type="showSecrets['azureConnectionString'] ? 'text' : 'password'"
                          class="input w-full pr-10"
                        />
                        <button
                          type="button"
                          class="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                          @click="toggleSecret('azureConnectionString')"
                        >
                          <EyeSlashIcon v-if="showSecrets['azureConnectionString']" class="h-5 w-5" />
                          <EyeIcon v-else class="h-5 w-5" />
                        </button>
                      </div>
                    </div>
                    <div>
                      <label class="label">SAS Token (alternative)</label>
                      <div class="relative">
                        <input
                          v-model="azureBlobConfig.sasToken"
                          :type="showSecrets['sasToken'] ? 'text' : 'password'"
                          class="input w-full pr-10"
                        />
                        <button
                          type="button"
                          class="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                          @click="toggleSecret('sasToken')"
                        >
                          <EyeSlashIcon v-if="showSecrets['sasToken']" class="h-5 w-5" />
                          <EyeIcon v-else class="h-5 w-5" />
                        </button>
                      </div>
                    </div>
                    <div>
                      <label class="label">Prefix</label>
                      <input
                        v-model="azureBlobConfig.prefix"
                        type="text"
                        class="input w-full"
                        placeholder="documents/"
                      />
                    </div>
                  </div>

                  <!-- Confluence -->
                  <div v-else-if="selectedType === 'Confluence'" class="space-y-4">
                    <div>
                      <label class="label">Base URL <span class="text-red-500">*</span></label>
                      <input
                        v-model="confluenceConfig.baseUrl"
                        type="url"
                        class="input w-full"
                        placeholder="https://your-domain.atlassian.net/wiki"
                      />
                    </div>
                    <div class="grid grid-cols-2 gap-4">
                      <div>
                        <label class="label">Username <span class="text-red-500">*</span></label>
                        <input
                          v-model="confluenceConfig.username"
                          type="email"
                          class="input w-full"
                          placeholder="user@example.com"
                        />
                      </div>
                      <div>
                        <label class="label">API Token</label>
                        <div class="relative">
                          <input
                            v-model="confluenceConfig.apiToken"
                            :type="showSecrets['apiToken'] ? 'text' : 'password'"
                            class="input w-full pr-10"
                          />
                          <button
                            type="button"
                            class="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                            @click="toggleSecret('apiToken')"
                          >
                            <EyeSlashIcon v-if="showSecrets['apiToken']" class="h-5 w-5" />
                            <EyeIcon v-else class="h-5 w-5" />
                          </button>
                        </div>
                      </div>
                    </div>
                    <div>
                      <label class="label">Space Key</label>
                      <input
                        v-model="confluenceConfig.spaceKey"
                        type="text"
                        class="input w-full"
                        placeholder="MYSPACE"
                      />
                    </div>
                    <div class="flex items-center gap-2">
                      <input
                        v-model="confluenceConfig.includeAttachments"
                        type="checkbox"
                        id="includeAttachments"
                        class="rounded border-gray-300 text-aegis-600"
                      />
                      <label for="includeAttachments" class="text-sm text-gray-700 dark:text-gray-300">
                        Include attachments
                      </label>
                    </div>
                  </div>

                  <!-- Notion -->
                  <div v-else-if="selectedType === 'Notion'" class="space-y-4">
                    <div>
                      <label class="label">Integration Token <span class="text-red-500">*</span></label>
                      <div class="relative">
                        <input
                          v-model="notionConfig.integrationToken"
                          :type="showSecrets['integrationToken'] ? 'text' : 'password'"
                          class="input w-full pr-10"
                          placeholder="secret_..."
                        />
                        <button
                          type="button"
                          class="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                          @click="toggleSecret('integrationToken')"
                        >
                          <EyeSlashIcon v-if="showSecrets['integrationToken']" class="h-5 w-5" />
                          <EyeIcon v-else class="h-5 w-5" />
                        </button>
                      </div>
                    </div>
                    <div>
                      <label class="label">Database ID</label>
                      <input
                        v-model="notionConfig.databaseId"
                        type="text"
                        class="input w-full"
                        placeholder="Optional: specific database to sync"
                      />
                    </div>
                  </div>

                  <!-- Google Drive -->
                  <div v-else-if="selectedType === 'GoogleDrive'" class="space-y-4">
                    <div>
                      <label class="label">Folder ID</label>
                      <input
                        v-model="googleDriveConfig.folderId"
                        type="text"
                        class="input w-full"
                        placeholder="Optional: specific folder ID"
                      />
                    </div>
                    <div>
                      <label class="label">Service Account Key (JSON)</label>
                      <textarea
                        v-model="googleDriveConfig.serviceAccountKey"
                        class="input w-full h-24"
                        placeholder='{"type": "service_account", ...}'
                      />
                    </div>
                    <div class="flex items-center gap-2">
                      <input
                        v-model="googleDriveConfig.includeSharedDrives"
                        type="checkbox"
                        id="includeSharedDrives"
                        class="rounded border-gray-300 text-aegis-600"
                      />
                      <label for="includeSharedDrives" class="text-sm text-gray-700 dark:text-gray-300">
                        Include shared drives
                      </label>
                    </div>
                  </div>

                  <!-- File Upload (default) -->
                  <div v-else class="p-4 bg-gray-50 rounded-lg dark:bg-gray-700/50">
                    <p class="text-sm text-gray-600 dark:text-gray-400">
                      File upload data sources are created automatically when you upload documents.
                    </p>
                  </div>
                </div>
              </div>

              <!-- Actions -->
              <div class="mt-6 flex justify-end gap-3">
                <button
                  v-if="!saving"
                  class="btn-ghost"
                  @click="emit('close')"
                >
                  Cancel
                </button>
                <button
                  class="btn-primary"
                  :disabled="!isValid || saving"
                  @click="handleSave"
                >
                  <template v-if="saving">
                    <ArrowPathIcon class="h-4 w-4 mr-2 animate-spin" />
                    Saving...
                  </template>
                  <template v-else>
                    <LinkIcon class="h-4 w-4 mr-2" />
                    {{ editMode ? 'Save Changes' : 'Add Data Source' }}
                  </template>
                </button>
              </div>
            </DialogPanel>
          </TransitionChild>
        </div>
      </div>
    </Dialog>
  </TransitionRoot>
</template>
