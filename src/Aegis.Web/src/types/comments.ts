// Comment Types

export interface Comment {
  id: string
  resourceId: string
  resourceType: CommentableResourceType
  authorId: string
  authorName: string
  authorAvatarUrl?: string
  content: string
  htmlContent?: string
  parentCommentId?: string
  threadRootId?: string
  status: CommentStatus
  isResolved: boolean
  resolvedBy?: string
  resolvedAt?: string
  isPinned: boolean
  pinnedBy?: string
  pinnedAt?: string
  createdAt: string
  updatedAt: string
  editedAt?: string
  isEdited: boolean
  mentions: CommentMention[]
  attachments: CommentAttachment[]
  anchor?: CommentAnchor
  reactionCounts: Record<ReactionType, number>
  replyCount: number
  metadata: Record<string, unknown>
}

export type CommentableResourceType =
  | 'Session'
  | 'Document'
  | 'Query'
  | 'QueryResult'
  | 'Report'
  | 'Dashboard'
  | 'DataSource'
  | 'Workspace'
  | 'Collection'

export type CommentStatus = 'Active' | 'Deleted' | 'Hidden' | 'Flagged'

export interface CreateCommentRequest {
  resourceId: string
  resourceType: CommentableResourceType
  content: string
  parentCommentId?: string
  anchor?: CommentAnchor
  mentionedUserIds?: string[]
  attachments?: CommentAttachment[]
}

export interface UpdateCommentRequest {
  content: string
  mentionedUserIds?: string[]
}

export interface CommentFilter {
  includeReplies?: boolean
  onlyResolved?: boolean
  onlyUnresolved?: boolean
  onlyPinned?: boolean
  authorId?: string
  since?: string
  sortOrder?: CommentSortOrder
  page?: number
  pageSize?: number
}

export type CommentSortOrder = 'Newest' | 'Oldest' | 'MostReplies' | 'MostReactions'

export interface CommentPage {
  comments: Comment[]
  totalCount: number
  page: number
  pageSize: number
  hasMore: boolean
}

// Mention Types

export interface CommentMention {
  userId: string
  displayName: string
  startPosition: number
  endPosition: number
  isRead: boolean
  readAt?: string
}

export interface MentionFilter {
  onlyUnread?: boolean
  resourceType?: CommentableResourceType
  since?: string
  page?: number
  pageSize?: number
}

export interface MentionPage {
  mentions: MentionInfo[]
  totalCount: number
  unreadCount: number
  page: number
  pageSize: number
}

export interface MentionInfo {
  commentId: string
  resourceId: string
  resourceType: CommentableResourceType
  mentionedBy: string
  mentionedByName: string
  commentContent: string
  mentionedAt: string
  isRead: boolean
}

// Reaction Types

export interface CommentReaction {
  commentId: string
  userId: string
  userName: string
  type: ReactionType
  createdAt: string
}

export type ReactionType =
  | 'Like'
  | 'Love'
  | 'Laugh'
  | 'Celebrate'
  | 'Insightful'
  | 'Question'
  | 'Agree'
  | 'Disagree'

export interface AddReactionRequest {
  type: ReactionType
}

// Anchor Types

export interface CommentAnchor {
  type: AnchorType
  elementId?: string
  startOffset?: number
  endOffset?: number
  selectedText?: string
  pageNumber?: number
  x?: number
  y?: number
}

export type AnchorType = 'Text' | 'Element' | 'Page' | 'Point' | 'Range'

// Attachment Types

export interface CommentAttachment {
  id: string
  fileName: string
  contentType: string
  size: number
  url: string
  thumbnailUrl?: string
}

// Search Types

export interface CommentSearchRequest {
  query: string
  workspaceId?: string
  resourceType?: CommentableResourceType
  authorId?: string
  startDate?: string
  endDate?: string
  page?: number
  pageSize?: number
}

// Statistics

export interface CommentStats {
  totalComments: number
  totalThreads: number
  totalReplies: number
  resolvedThreads: number
  unresolvedThreads: number
  totalReactions: number
  totalMentions: number
  unreadMentions: number
  commentsByResourceType: Record<CommentableResourceType, number>
  reactionsByType: Record<ReactionType, number>
  topCommenters: TopCommenter[]
  generatedAt: string
}

export interface TopCommenter {
  userId: string
  displayName: string
  commentCount: number
  reactionCount: number
}

// Helper function to get reaction emoji
export function getReactionEmoji(type: ReactionType): string {
  const emojis: Record<ReactionType, string> = {
    Like: '\uD83D\uDC4D',
    Love: '\u2764\uFE0F',
    Laugh: '\uD83D\uDE04',
    Celebrate: '\uD83C\uDF89',
    Insightful: '\uD83D\uDCA1',
    Question: '\u2753',
    Agree: '\u2705',
    Disagree: '\u274C'
  }
  return emojis[type]
}

// Helper function to format relative time
export function formatRelativeTime(dateString: string): string {
  const date = new Date(dateString)
  const now = new Date()
  const diffInSeconds = Math.floor((now.getTime() - date.getTime()) / 1000)

  if (diffInSeconds < 60) return 'just now'
  if (diffInSeconds < 3600) return `${Math.floor(diffInSeconds / 60)}m ago`
  if (diffInSeconds < 86400) return `${Math.floor(diffInSeconds / 3600)}h ago`
  if (diffInSeconds < 604800) return `${Math.floor(diffInSeconds / 86400)}d ago`

  return date.toLocaleDateString()
}
