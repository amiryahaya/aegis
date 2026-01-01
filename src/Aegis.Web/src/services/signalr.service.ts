import * as signalR from '@microsoft/signalr'

const SIGNALR_URL = import.meta.env.VITE_SIGNALR_URL || '/hubs'

export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting'

export interface QueryCitation {
  documentId: string
  text: string
  score: number
  chunkIndex: number
}

export interface NotificationPayload {
  id: string
  type: string
  title: string
  message: string
  priority: string
  createdAt: string
  isRead: boolean
  data?: Record<string, unknown>
}

export interface SignalRCallbacks {
  // Query Hub callbacks
  onStreamToken?: (token: string) => void
  onCitations?: (citations: QueryCitation[]) => void
  onStreamComplete?: () => void
  onError?: (error: string) => void
  // Notification Hub callbacks
  onNotification?: (notification: NotificationPayload) => void
  onUnreadCount?: (count: number) => void
  onNotificationRead?: (notificationId: string) => void
  onAllNotificationsRead?: (count: number) => void
  onRecentNotifications?: (notifications: NotificationPayload[]) => void
  // Connection callbacks
  onStateChange?: (state: ConnectionState) => void
}

class SignalRService {
  private queryConnection: signalR.HubConnection | null = null
  private notificationConnection: signalR.HubConnection | null = null
  private callbacks: SignalRCallbacks = {}
  private queryState: ConnectionState = 'disconnected'
  private notificationState: ConnectionState = 'disconnected'

  // Get JWT token from localStorage
  private getAccessToken(): string {
    return localStorage.getItem('token') || ''
  }

  // ============== Query Hub ==============

  async connectQueryHub(): Promise<void> {
    if (this.queryConnection?.state === signalR.HubConnectionState.Connected) {
      return
    }

    this.updateQueryState('connecting')

    this.queryConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${SIGNALR_URL}/query`, {
        accessTokenFactory: () => this.getAccessToken()
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    // Set up event handlers
    this.queryConnection.on('StreamToken', (token: string) => {
      this.callbacks.onStreamToken?.(token)
    })

    this.queryConnection.on('Citations', (citations: QueryCitation[]) => {
      this.callbacks.onCitations?.(citations)
    })

    this.queryConnection.on('StreamComplete', () => {
      this.callbacks.onStreamComplete?.()
    })

    this.queryConnection.on('Error', (error: string) => {
      this.callbacks.onError?.(error)
    })

    // Connection lifecycle
    this.queryConnection.onreconnecting(() => {
      this.updateQueryState('reconnecting')
    })

    this.queryConnection.onreconnected(() => {
      this.updateQueryState('connected')
    })

    this.queryConnection.onclose(() => {
      this.updateQueryState('disconnected')
    })

    try {
      await this.queryConnection.start()
      this.updateQueryState('connected')
    } catch (error) {
      console.error('Failed to connect to Query Hub:', error)
      this.updateQueryState('disconnected')
      throw error
    }
  }

  async disconnectQueryHub(): Promise<void> {
    if (this.queryConnection) {
      await this.queryConnection.stop()
      this.queryConnection = null
      this.updateQueryState('disconnected')
    }
  }

  async streamQuery(teamId: string, query: string, maxResults = 5): Promise<void> {
    if (!this.queryConnection || this.queryConnection.state !== signalR.HubConnectionState.Connected) {
      await this.connectQueryHub()
    }

    await this.queryConnection!.invoke('StreamQuery', teamId, query, maxResults)
  }

  private updateQueryState(state: ConnectionState): void {
    this.queryState = state
    this.callbacks.onStateChange?.(state)
  }

  get isQueryConnected(): boolean {
    return this.queryState === 'connected'
  }

  // ============== Notification Hub ==============

  async connectNotificationHub(): Promise<void> {
    if (this.notificationConnection?.state === signalR.HubConnectionState.Connected) {
      return
    }

    this.updateNotificationState('connecting')

    this.notificationConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${SIGNALR_URL}/notifications`, {
        accessTokenFactory: () => this.getAccessToken()
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    // Set up event handlers
    this.notificationConnection.on('Notification', (notification: NotificationPayload) => {
      this.callbacks.onNotification?.(notification)
    })

    this.notificationConnection.on('UnreadCount', (count: number) => {
      this.callbacks.onUnreadCount?.(count)
    })

    this.notificationConnection.on('NotificationRead', (notificationId: string) => {
      this.callbacks.onNotificationRead?.(notificationId)
    })

    this.notificationConnection.on('AllNotificationsRead', (count: number) => {
      this.callbacks.onAllNotificationsRead?.(count)
    })

    this.notificationConnection.on('RecentNotifications', (notifications: NotificationPayload[]) => {
      this.callbacks.onRecentNotifications?.(notifications)
    })

    this.notificationConnection.on('Error', (error: string) => {
      this.callbacks.onError?.(error)
    })

    // Connection lifecycle
    this.notificationConnection.onreconnecting(() => {
      this.updateNotificationState('reconnecting')
    })

    this.notificationConnection.onreconnected(() => {
      this.updateNotificationState('connected')
    })

    this.notificationConnection.onclose(() => {
      this.updateNotificationState('disconnected')
    })

    try {
      await this.notificationConnection.start()
      this.updateNotificationState('connected')
    } catch (error) {
      console.error('Failed to connect to Notification Hub:', error)
      this.updateNotificationState('disconnected')
      throw error
    }
  }

  async disconnectNotificationHub(): Promise<void> {
    if (this.notificationConnection) {
      await this.notificationConnection.stop()
      this.notificationConnection = null
      this.updateNotificationState('disconnected')
    }
  }

  async joinTeam(teamId: string): Promise<void> {
    if (!this.notificationConnection || this.notificationConnection.state !== signalR.HubConnectionState.Connected) {
      await this.connectNotificationHub()
    }
    await this.notificationConnection!.invoke('JoinTeam', teamId)
  }

  async leaveTeam(teamId: string): Promise<void> {
    if (this.notificationConnection?.state === signalR.HubConnectionState.Connected) {
      await this.notificationConnection.invoke('LeaveTeam', teamId)
    }
  }

  async joinWorkspace(workspaceId: string): Promise<void> {
    if (!this.notificationConnection || this.notificationConnection.state !== signalR.HubConnectionState.Connected) {
      await this.connectNotificationHub()
    }
    await this.notificationConnection!.invoke('JoinWorkspace', workspaceId)
  }

  async leaveWorkspace(workspaceId: string): Promise<void> {
    if (this.notificationConnection?.state === signalR.HubConnectionState.Connected) {
      await this.notificationConnection.invoke('LeaveWorkspace', workspaceId)
    }
  }

  async markAsRead(notificationId: string): Promise<void> {
    if (this.notificationConnection?.state === signalR.HubConnectionState.Connected) {
      await this.notificationConnection.invoke('MarkAsRead', notificationId)
    }
  }

  async markAllAsRead(): Promise<void> {
    if (this.notificationConnection?.state === signalR.HubConnectionState.Connected) {
      await this.notificationConnection.invoke('MarkAllAsRead')
    }
  }

  async getRecentNotifications(count = 10): Promise<void> {
    if (this.notificationConnection?.state === signalR.HubConnectionState.Connected) {
      await this.notificationConnection.invoke('GetRecent', count)
    }
  }

  private updateNotificationState(state: ConnectionState): void {
    this.notificationState = state
    this.callbacks.onStateChange?.(state)
  }

  get isNotificationConnected(): boolean {
    return this.notificationState === 'connected'
  }

  // ============== Callback Registration ==============

  setCallbacks(callbacks: SignalRCallbacks): void {
    this.callbacks = { ...this.callbacks, ...callbacks }
  }

  clearCallbacks(): void {
    this.callbacks = {}
  }

  // ============== Cleanup ==============

  async disconnectAll(): Promise<void> {
    await Promise.all([
      this.disconnectQueryHub(),
      this.disconnectNotificationHub()
    ])
    this.clearCallbacks()
  }
}

export const signalRService = new SignalRService()
export default signalRService
