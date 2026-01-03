import { ref, computed, readonly } from 'vue'
import queryService from '@/services/query.service'
import type {
  QueryResponse,
  QuerySource,
  QueryAnalysis,
  StreamingCallbacks
} from '@/services/query.service'

export interface QueryState {
  query: string
  response: string
  sources: QuerySource[]
  analysis: QueryAnalysis | null
  isLoading: boolean
  isStreaming: boolean
  error: string | null
  tokensUsed: number
  processingTime: number
}

export interface UseQueryOptions {
  onToken?: (token: string) => void
  onSources?: (sources: QuerySource[]) => void
  onAnalysis?: (analysis: QueryAnalysis) => void
  onComplete?: (response: string) => void
  onError?: (error: string) => void
}

export function useQuery(workspaceId: string, options: UseQueryOptions = {}) {
  // State
  const query = ref('')
  const response = ref('')
  const sources = ref<QuerySource[]>([])
  const analysis = ref<QueryAnalysis | null>(null)
  const isLoading = ref(false)
  const isStreaming = ref(false)
  const error = ref<string | null>(null)
  const tokensUsed = ref(0)
  const processingTime = ref(0)
  const conversationId = ref<string | undefined>(undefined)
  const abortController = ref<AbortController | null>(null)
  const conversationHistory = ref<Array<{ query: string; response: string }>>([])

  // Computed
  const hasResponse = computed(() => response.value.length > 0)
  const hasSources = computed(() => sources.value.length > 0)
  const formattedSources = computed(() => queryService.formatSources(sources.value))
  const followUpQuestions = computed(() =>
    response.value ? queryService.extractFollowUpQuestions(response.value) : []
  )
  const state = computed<QueryState>(() => ({
    query: query.value,
    response: response.value,
    sources: sources.value,
    analysis: analysis.value,
    isLoading: isLoading.value,
    isStreaming: isStreaming.value,
    error: error.value,
    tokensUsed: tokensUsed.value,
    processingTime: processingTime.value
  }))

  // Actions
  function reset() {
    query.value = ''
    response.value = ''
    sources.value = []
    analysis.value = null
    error.value = null
    tokensUsed.value = 0
    processingTime.value = 0
    isLoading.value = false
    isStreaming.value = false
    abortController.value = null
  }

  function resetConversation() {
    reset()
    conversationId.value = undefined
    conversationHistory.value = []
  }

  function cancel() {
    if (abortController.value) {
      abortController.value.abort()
      abortController.value = null
      isStreaming.value = false
      isLoading.value = false
    }
  }

  async function executeQuery(queryText: string): Promise<QueryResponse | null> {
    query.value = queryText
    response.value = ''
    sources.value = []
    analysis.value = null
    error.value = null
    isLoading.value = true
    isStreaming.value = false

    const startTime = Date.now()

    try {
      const result = await queryService.query(
        workspaceId,
        queryText,
        conversationId.value
      )

      response.value = result.response
      sources.value = result.sources
      analysis.value = result.queryAnalysis
      tokensUsed.value = result.tokensUsed
      processingTime.value = result.processingTime
      conversationId.value = result.conversationId

      // Add to conversation history
      conversationHistory.value.push({
        query: queryText,
        response: result.response
      })

      options.onComplete?.(result.response)
      return result
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Query failed'
      error.value = errorMessage
      options.onError?.(errorMessage)
      return null
    } finally {
      isLoading.value = false
      processingTime.value = Date.now() - startTime
    }
  }

  function streamQuery(queryText: string): void {
    query.value = queryText
    response.value = ''
    sources.value = []
    analysis.value = null
    error.value = null
    isLoading.value = true
    isStreaming.value = true

    const startTime = Date.now()

    const callbacks: StreamingCallbacks = {
      onToken: (token) => {
        response.value += token
        options.onToken?.(token)
      },
      onSources: (newSources) => {
        sources.value = newSources
        options.onSources?.(newSources)
      },
      onAnalysis: (newAnalysis) => {
        analysis.value = newAnalysis
        options.onAnalysis?.(newAnalysis)
      },
      onComplete: () => {
        isStreaming.value = false
        isLoading.value = false
        processingTime.value = Date.now() - startTime
        abortController.value = null

        // Add to conversation history
        conversationHistory.value.push({
          query: queryText,
          response: response.value
        })

        options.onComplete?.(response.value)
      },
      onError: (errorMessage) => {
        error.value = errorMessage
        isStreaming.value = false
        isLoading.value = false
        processingTime.value = Date.now() - startTime
        abortController.value = null
        options.onError?.(errorMessage)
      }
    }

    abortController.value = queryService.streamQuery(
      workspaceId,
      queryText,
      callbacks,
      conversationId.value
    )
  }

  function buildContextualQuery(queryText: string): string {
    return queryService.buildContextualQuery(queryText, conversationHistory.value)
  }

  function setConversationId(id: string | undefined) {
    conversationId.value = id
  }

  return {
    // State (readonly)
    query: readonly(query),
    response: readonly(response),
    sources: readonly(sources),
    analysis: readonly(analysis),
    isLoading: readonly(isLoading),
    isStreaming: readonly(isStreaming),
    error: readonly(error),
    tokensUsed: readonly(tokensUsed),
    processingTime: readonly(processingTime),
    conversationId: readonly(conversationId),
    conversationHistory: readonly(conversationHistory),

    // Computed
    hasResponse,
    hasSources,
    formattedSources,
    followUpQuestions,
    state,

    // Actions
    executeQuery,
    streamQuery,
    buildContextualQuery,
    cancel,
    reset,
    resetConversation,
    setConversationId
  }
}

// Factory function for creating a new query instance per chat session
export function createQuerySession(workspaceId: string, options: UseQueryOptions = {}) {
  return useQuery(workspaceId, options)
}

export type QueryComposable = ReturnType<typeof useQuery>
