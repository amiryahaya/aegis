import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '@/services/api'
import type {
  Comment,
  CommentPage,
  CommentFilter,
  CreateCommentRequest,
  UpdateCommentRequest,
  CommentableResourceType,
  CommentReaction,
  ReactionType,
  MentionPage,
  MentionFilter,
  CommentStats,
  CommentSearchRequest
} from '@/types/comments'

export const useCommentsStore = defineStore('comments', () => {
  // State
  const comments = ref<Comment[]>([])
  const currentComment = ref<Comment | null>(null)
  const replies = ref<Record<string, Comment[]>>({})
  const totalCount = ref(0)
  const currentPage = ref(1)
  const pageSize = ref(20)
  const hasMore = ref(false)
  const mentions = ref<MentionPage | null>(null)
  const unreadMentionCount = ref(0)
  const stats = ref<CommentStats | null>(null)
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  // Getters
  const hasComments = computed(() => comments.value.length > 0)
  const pinnedComments = computed(() => comments.value.filter(c => c.isPinned))
  const resolvedComments = computed(() => comments.value.filter(c => c.isResolved))
  const unresolvedComments = computed(() => comments.value.filter(c => !c.isResolved))

  // Actions
  async function fetchComments(
    resourceId: string,
    resourceType: CommentableResourceType,
    filter?: CommentFilter
  ): Promise<CommentPage | null> {
    isLoading.value = true
    error.value = null
    try {
      const params = new URLSearchParams()
      params.append('resourceType', resourceType)
      if (filter?.includeReplies !== undefined) params.append('includeReplies', filter.includeReplies.toString())
      if (filter?.onlyResolved) params.append('onlyResolved', 'true')
      if (filter?.onlyUnresolved) params.append('onlyUnresolved', 'true')
      if (filter?.onlyPinned) params.append('onlyPinned', 'true')
      if (filter?.authorId) params.append('authorId', filter.authorId)
      if (filter?.since) params.append('since', filter.since)
      if (filter?.sortOrder) params.append('sortOrder', filter.sortOrder)
      params.append('page', (filter?.page || currentPage.value).toString())
      params.append('pageSize', (filter?.pageSize || pageSize.value).toString())

      const response = await api.get<CommentPage>(`/comments/resource/${resourceId}?${params}`)
      comments.value = response.comments
      totalCount.value = response.totalCount
      hasMore.value = response.hasMore
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch comments'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function fetchReplies(commentId: string): Promise<Comment[]> {
    try {
      const response = await api.get<Comment[]>(`/comments/${commentId}/replies`)
      replies.value[commentId] = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch replies'
      return []
    }
  }

  async function createComment(request: CreateCommentRequest): Promise<Comment | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.post<Comment>('/comments', request)

      if (request.parentCommentId) {
        // Add to replies
        if (!replies.value[request.parentCommentId]) {
          replies.value[request.parentCommentId] = []
        }
        replies.value[request.parentCommentId].push(response)

        // Update reply count on parent
        const parent = comments.value.find(c => c.id === request.parentCommentId)
        if (parent) {
          parent.replyCount++
        }
      } else {
        // Add to main comments list
        comments.value = [response, ...comments.value]
        totalCount.value++
      }

      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to create comment'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function updateComment(
    commentId: string,
    request: UpdateCommentRequest
  ): Promise<Comment | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.put<Comment>(`/comments/${commentId}`, request)

      // Update in the comments list
      const index = comments.value.findIndex(c => c.id === commentId)
      if (index !== -1) {
        comments.value[index] = response
      }

      // Update in replies if applicable
      for (const parentId of Object.keys(replies.value)) {
        const replyIndex = replies.value[parentId].findIndex(r => r.id === commentId)
        if (replyIndex !== -1) {
          replies.value[parentId][replyIndex] = response
          break
        }
      }

      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to update comment'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function deleteComment(commentId: string): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.delete(`/comments/${commentId}`)

      // Remove from comments list
      const index = comments.value.findIndex(c => c.id === commentId)
      if (index !== -1) {
        comments.value.splice(index, 1)
        totalCount.value--
      }

      // Remove from replies if applicable
      for (const parentId of Object.keys(replies.value)) {
        const replyIndex = replies.value[parentId].findIndex(r => r.id === commentId)
        if (replyIndex !== -1) {
          replies.value[parentId].splice(replyIndex, 1)
          // Update parent reply count
          const parent = comments.value.find(c => c.id === parentId)
          if (parent) {
            parent.replyCount--
          }
          break
        }
      }

      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to delete comment'
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function addReaction(commentId: string, reactionType: ReactionType): Promise<CommentReaction | null> {
    try {
      const response = await api.post<CommentReaction>(`/comments/${commentId}/reactions`, {
        type: reactionType
      })

      // Update reaction count
      const comment = findComment(commentId)
      if (comment) {
        comment.reactionCounts[reactionType] = (comment.reactionCounts[reactionType] || 0) + 1
      }

      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to add reaction'
      return null
    }
  }

  async function removeReaction(commentId: string, reactionType: ReactionType): Promise<boolean> {
    try {
      await api.delete(`/comments/${commentId}/reactions/${reactionType}`)

      // Update reaction count
      const comment = findComment(commentId)
      if (comment && comment.reactionCounts[reactionType]) {
        comment.reactionCounts[reactionType]--
        if (comment.reactionCounts[reactionType] <= 0) {
          delete comment.reactionCounts[reactionType]
        }
      }

      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to remove reaction'
      return false
    }
  }

  async function resolveComment(commentId: string): Promise<Comment | null> {
    try {
      const response = await api.post<Comment>(`/comments/${commentId}/resolve`)
      updateCommentInState(commentId, response)
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to resolve comment'
      return null
    }
  }

  async function reopenComment(commentId: string): Promise<Comment | null> {
    try {
      const response = await api.post<Comment>(`/comments/${commentId}/reopen`)
      updateCommentInState(commentId, response)
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to reopen comment'
      return null
    }
  }

  async function pinComment(commentId: string): Promise<Comment | null> {
    try {
      const response = await api.post<Comment>(`/comments/${commentId}/pin`)
      updateCommentInState(commentId, response)
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to pin comment'
      return null
    }
  }

  async function unpinComment(commentId: string): Promise<Comment | null> {
    try {
      const response = await api.post<Comment>(`/comments/${commentId}/unpin`)
      updateCommentInState(commentId, response)
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to unpin comment'
      return null
    }
  }

  async function fetchMentions(filter?: MentionFilter): Promise<MentionPage | null> {
    try {
      const params = new URLSearchParams()
      if (filter?.onlyUnread) params.append('onlyUnread', 'true')
      if (filter?.resourceType) params.append('resourceType', filter.resourceType)
      if (filter?.since) params.append('since', filter.since)
      params.append('page', (filter?.page || 1).toString())
      params.append('pageSize', (filter?.pageSize || 20).toString())

      const response = await api.get<MentionPage>(`/comments/mentions?${params}`)
      mentions.value = response
      unreadMentionCount.value = response.unreadCount
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch mentions'
      return null
    }
  }

  async function markMentionsAsRead(commentIds?: string[]): Promise<boolean> {
    try {
      await api.post('/comments/mentions/read', { commentIds })
      if (commentIds) {
        unreadMentionCount.value = Math.max(0, unreadMentionCount.value - commentIds.length)
      } else {
        unreadMentionCount.value = 0
      }
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to mark mentions as read'
      return false
    }
  }

  async function searchComments(request: CommentSearchRequest): Promise<CommentPage | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.post<CommentPage>('/comments/search', request)
      comments.value = response.comments
      totalCount.value = response.totalCount
      hasMore.value = response.hasMore
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to search comments'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function fetchStats(
    resourceId?: string,
    resourceType?: CommentableResourceType
  ): Promise<CommentStats | null> {
    try {
      const params = new URLSearchParams()
      if (resourceId) params.append('resourceId', resourceId)
      if (resourceType) params.append('resourceType', resourceType)

      const response = await api.get<CommentStats>(`/comments/stats?${params}`)
      stats.value = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch stats'
      return null
    }
  }

  // Helper functions
  function findComment(commentId: string): Comment | null {
    // Check main comments
    const comment = comments.value.find(c => c.id === commentId)
    if (comment) return comment

    // Check replies
    for (const parentId of Object.keys(replies.value)) {
      const reply = replies.value[parentId].find(r => r.id === commentId)
      if (reply) return reply
    }

    return null
  }

  function updateCommentInState(commentId: string, updated: Comment) {
    const index = comments.value.findIndex(c => c.id === commentId)
    if (index !== -1) {
      comments.value[index] = updated
    }

    for (const parentId of Object.keys(replies.value)) {
      const replyIndex = replies.value[parentId].findIndex(r => r.id === commentId)
      if (replyIndex !== -1) {
        replies.value[parentId][replyIndex] = updated
        break
      }
    }
  }

  function clearError() {
    error.value = null
  }

  function reset() {
    comments.value = []
    currentComment.value = null
    replies.value = {}
    totalCount.value = 0
    currentPage.value = 1
    hasMore.value = false
    mentions.value = null
    unreadMentionCount.value = 0
    stats.value = null
    error.value = null
  }

  return {
    // State
    comments,
    currentComment,
    replies,
    totalCount,
    currentPage,
    pageSize,
    hasMore,
    mentions,
    unreadMentionCount,
    stats,
    isLoading,
    error,

    // Getters
    hasComments,
    pinnedComments,
    resolvedComments,
    unresolvedComments,

    // Actions
    fetchComments,
    fetchReplies,
    createComment,
    updateComment,
    deleteComment,
    addReaction,
    removeReaction,
    resolveComment,
    reopenComment,
    pinComment,
    unpinComment,
    fetchMentions,
    markMentionsAsRead,
    searchComments,
    fetchStats,
    clearError,
    reset
  }
})
