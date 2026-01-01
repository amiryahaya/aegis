// Activity Feed Types

export interface Activity {
  id: string
  actorId: string
  actorName: string
  actorAvatarUrl?: string
  type: FeedActivityType
  verb: ActivityVerb
  resourceId?: string
  resourceType?: ActivityResourceType
  resourceName?: string
  targetId?: string
  targetType?: ActivityResourceType
  targetName?: string
  workspaceId?: string
  workspaceName?: string
  description: string
  htmlDescription?: string
  occurredAt: string
  visibility: ActivityVisibility
  importance: ActivityImportance
  details: Record<string, unknown>
  relatedUserIds: string[]
  metadata: Record<string, unknown>
}

export type FeedActivityType =
  | 'Resource'
  | 'Collaboration'
  | 'Comment'
  | 'Query'
  | 'System'
  | 'User'
  | 'Security'

export type ActivityVerb =
  | 'Created'
  | 'Updated'
  | 'Deleted'
  | 'Shared'
  | 'Unshared'
  | 'Commented'
  | 'Replied'
  | 'Resolved'
  | 'Reopened'
  | 'Mentioned'
  | 'Reacted'
  | 'Viewed'
  | 'Downloaded'
  | 'Exported'
  | 'Uploaded'
  | 'Joined'
  | 'Left'
  | 'Invited'
  | 'Removed'
  | 'Queried'
  | 'Answered'
  | 'Pinned'
  | 'Unpinned'
  | 'Archived'
  | 'Restored'
  | 'Transferred'

export type ActivityResourceType =
  | 'Session'
  | 'Document'
  | 'Query'
  | 'Report'
  | 'Dashboard'
  | 'DataSource'
  | 'Workspace'
  | 'Collection'
  | 'Comment'
  | 'User'
  | 'Team'
  | 'Share'

export type ActivityVisibility = 'Private' | 'Team' | 'Workspace' | 'Public'

export type ActivityImportance = 'Low' | 'Normal' | 'High' | 'Critical'

export interface ActivityFilter {
  type?: FeedActivityType
  verb?: ActivityVerb
  resourceType?: ActivityResourceType
  actorId?: string
  minImportance?: ActivityImportance
  since?: string
  until?: string
  page?: number
  pageSize?: number
}

export interface ActivityPage {
  activities: Activity[]
  totalCount: number
  page: number
  pageSize: number
  hasMore: boolean
}

export interface PersonalizedFeedOptions {
  includeOwnActivities?: boolean
  onlySubscribed?: boolean
  onlyUnseen?: boolean
  workspaceIds?: string[]
  page?: number
  pageSize?: number
}

// Subscription Types

export interface ActivitySubscription {
  id: string
  userId: string
  resourceId?: string
  resourceType?: ActivityResourceType
  workspaceId?: string
  scope: SubscriptionScope
  includedVerbs: ActivityVerb[]
  excludedVerbs: ActivityVerb[]
  notifyEmail: boolean
  notifyInApp: boolean
  notifyRealTime: boolean
  createdAt: string
}

export type SubscriptionScope = 'Resource' | 'Workspace' | 'User' | 'All'

export interface SubscribeToActivityRequest {
  resourceId?: string
  resourceType?: ActivityResourceType
  workspaceId?: string
  scope?: SubscriptionScope
  includedVerbs?: ActivityVerb[]
  excludedVerbs?: ActivityVerb[]
  notifyEmail?: boolean
  notifyInApp?: boolean
  notifyRealTime?: boolean
}

// Aggregation Types

export interface AggregationOptions {
  groupWindowHours?: number
  groupByActor?: boolean
  groupByResource?: boolean
  groupByVerb?: boolean
  since?: string
  maxGroups?: number
}

export interface ActivityGroup {
  groupKey: string
  actorId?: string
  actorName?: string
  resourceId?: string
  resourceType?: ActivityResourceType
  resourceName?: string
  verb?: ActivityVerb
  count: number
  firstOccurrence: string
  lastOccurrence: string
  summary: string
  sampleActivities: Activity[]
}

// Statistics

export interface ActivityStats {
  totalActivities: number
  activitiesToday: number
  activitiesThisWeek: number
  activeUsers: number
  byType: Record<FeedActivityType, number>
  byVerb: Record<ActivityVerb, number>
  byResourceType: Record<ActivityResourceType, number>
  trends: ActivityTrend[]
  topUsers: TopActivityUser[]
  generatedAt: string
}

export interface ActivityTrend {
  date: string
  count: number
}

export interface TopActivityUser {
  userId: string
  displayName: string
  activityCount: number
}

// Helper function to get activity icon based on verb
export function getActivityIcon(verb: ActivityVerb): string {
  const icons: Record<ActivityVerb, string> = {
    Created: 'plus-circle',
    Updated: 'pencil',
    Deleted: 'trash',
    Shared: 'share',
    Unshared: 'link-off',
    Commented: 'chat-bubble',
    Replied: 'arrow-reply',
    Resolved: 'check-circle',
    Reopened: 'arrow-path',
    Mentioned: 'at-symbol',
    Reacted: 'heart',
    Viewed: 'eye',
    Downloaded: 'arrow-down-tray',
    Exported: 'document-arrow-down',
    Uploaded: 'arrow-up-tray',
    Joined: 'user-plus',
    Left: 'user-minus',
    Invited: 'envelope',
    Removed: 'user-x',
    Queried: 'magnifying-glass',
    Answered: 'chat-bubble-bottom-center-text',
    Pinned: 'pin',
    Unpinned: 'pin-slash',
    Archived: 'archive-box',
    Restored: 'archive-box-arrow-up',
    Transferred: 'arrows-right-left'
  }
  return icons[verb] || 'information-circle'
}

// Helper function to get activity color based on type
export function getActivityColor(type: FeedActivityType): string {
  const colors: Record<FeedActivityType, string> = {
    Resource: 'blue',
    Collaboration: 'purple',
    Comment: 'green',
    Query: 'yellow',
    System: 'gray',
    User: 'indigo',
    Security: 'red'
  }
  return colors[type] || 'gray'
}
