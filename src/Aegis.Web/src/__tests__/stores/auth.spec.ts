import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '@/stores/auth'
import { UserRole } from '@/types'
import api from '@/services/api'

// Mock the API
vi.mock('@/services/api', () => ({
  default: {
    post: vi.fn(),
    get: vi.fn(),
  },
}))

// Create a working localStorage mock for these tests
const localStorageData: Record<string, string> = {}
const localStorageMock = {
  getItem: vi.fn((key: string) => localStorageData[key] || null),
  setItem: vi.fn((key: string, value: string) => { localStorageData[key] = value }),
  removeItem: vi.fn((key: string) => { delete localStorageData[key] }),
  clear: vi.fn(() => { Object.keys(localStorageData).forEach(key => delete localStorageData[key]) }),
}

describe('Auth Store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    Object.keys(localStorageData).forEach(key => delete localStorageData[key])
    Object.defineProperty(window, 'localStorage', { value: localStorageMock, writable: true })
  })

  afterEach(() => {
    Object.keys(localStorageData).forEach(key => delete localStorageData[key])
  })

  describe('initial state', () => {
    it('has null user and token initially', () => {
      const store = useAuthStore()
      expect(store.user).toBeNull()
      expect(store.token).toBeNull()
    })

    it('is not authenticated initially', () => {
      const store = useAuthStore()
      expect(store.isAuthenticated).toBe(false)
    })

    it('has default userName', () => {
      const store = useAuthStore()
      expect(store.userName).toBe('User')
    })

    it('is not admin by default', () => {
      const store = useAuthStore()
      expect(store.isAdmin).toBe(false)
    })
  })

  describe('initialize', () => {
    it('loads user and token from localStorage', () => {
      const mockUser = {
        id: 'user-1',
        email: 'test@example.com',
        name: 'Test User',
        role: UserRole.Viewer,
        teamId: 'team-1',
      }

      localStorage.setItem('token', 'test-token')
      localStorage.setItem('user', JSON.stringify(mockUser))

      const store = useAuthStore()
      store.initialize()

      expect(store.token).toBe('test-token')
      expect(store.user).toEqual(mockUser)
      expect(store.isAuthenticated).toBe(true)
    })

    it('handles invalid JSON in localStorage', () => {
      localStorage.setItem('token', 'test-token')
      localStorage.setItem('user', 'invalid-json')

      const store = useAuthStore()
      store.initialize()

      expect(store.user).toBeNull()
      expect(store.token).toBeNull()
    })
  })

  describe('login', () => {
    it('successfully logs in user', async () => {
      const mockResponse = {
        token: 'jwt-token',
        user: {
          id: 'user-1',
          email: 'test@example.com',
          name: 'Test User',
          role: UserRole.Viewer,
          teamId: 'team-1',
        },
      }

      vi.mocked(api.post).mockResolvedValueOnce(mockResponse)

      const store = useAuthStore()
      const result = await store.login({
        email: 'test@example.com',
        password: 'password123',
      })

      expect(result).toBe(true)
      expect(store.user).toEqual(mockResponse.user)
      expect(store.token).toBe('jwt-token')
      expect(store.isAuthenticated).toBe(true)
      expect(store.error).toBeNull()
    })

    it('stores credentials in localStorage on successful login', async () => {
      const mockResponse = {
        token: 'jwt-token',
        user: {
          id: 'user-1',
          email: 'test@example.com',
          name: 'Test User',
          role: UserRole.Viewer,
          teamId: 'team-1',
        },
      }

      vi.mocked(api.post).mockResolvedValueOnce(mockResponse)

      const store = useAuthStore()
      await store.login({
        email: 'test@example.com',
        password: 'password123',
      })

      expect(localStorage.getItem('token')).toBe('jwt-token')
      expect(JSON.parse(localStorage.getItem('user')!)).toEqual(mockResponse.user)
    })

    it('handles login failure', async () => {
      vi.mocked(api.post).mockRejectedValueOnce({
        detail: 'Invalid credentials',
      })

      const store = useAuthStore()
      const result = await store.login({
        email: 'test@example.com',
        password: 'wrong-password',
      })

      expect(result).toBe(false)
      expect(store.error).toBe('Invalid credentials')
      expect(store.isAuthenticated).toBe(false)
    })

    it('sets loading state during login', async () => {
      vi.mocked(api.post).mockImplementationOnce(() =>
        new Promise(resolve =>
          setTimeout(() => resolve({ token: 'token', user: {} }), 100)
        )
      )

      const store = useAuthStore()
      const loginPromise = store.login({
        email: 'test@example.com',
        password: 'password',
      })

      expect(store.loading).toBe(true)
      await loginPromise
      expect(store.loading).toBe(false)
    })
  })

  describe('logout', () => {
    it('clears user and token', async () => {
      const mockResponse = {
        token: 'jwt-token',
        user: {
          id: 'user-1',
          email: 'test@example.com',
          name: 'Test User',
          role: UserRole.Viewer,
          teamId: 'team-1',
        },
      }

      vi.mocked(api.post).mockResolvedValueOnce(mockResponse)

      const store = useAuthStore()
      await store.login({ email: 'test@example.com', password: 'password' })

      expect(store.isAuthenticated).toBe(true)

      store.logout()

      expect(store.user).toBeNull()
      expect(store.token).toBeNull()
      expect(store.isAuthenticated).toBe(false)
    })

    it('clears localStorage on logout', async () => {
      localStorage.setItem('token', 'test-token')
      localStorage.setItem('user', JSON.stringify({ id: 'user-1' }))

      const store = useAuthStore()
      store.logout()

      expect(localStorage.getItem('token')).toBeNull()
      expect(localStorage.getItem('user')).toBeNull()
    })
  })

  describe('refreshUser', () => {
    it('refreshes user data from API', async () => {
      const initialUser = {
        id: 'user-1',
        email: 'test@example.com',
        name: 'Test User',
        role: UserRole.Viewer,
        teamId: 'team-1',
      }

      const updatedUser = {
        ...initialUser,
        name: 'Updated Name',
      }

      localStorage.setItem('token', 'test-token')
      localStorage.setItem('user', JSON.stringify(initialUser))

      vi.mocked(api.get).mockResolvedValueOnce(updatedUser)

      const store = useAuthStore()
      store.initialize()

      await store.refreshUser()

      expect(store.user?.name).toBe('Updated Name')
    })

    it('logs out on refresh failure', async () => {
      localStorage.setItem('token', 'test-token')
      localStorage.setItem('user', JSON.stringify({ id: 'user-1' }))

      vi.mocked(api.get).mockRejectedValueOnce(new Error('Unauthorized'))

      const store = useAuthStore()
      store.initialize()

      await store.refreshUser()

      expect(store.isAuthenticated).toBe(false)
      expect(store.user).toBeNull()
    })

    it('does nothing if not authenticated', async () => {
      const store = useAuthStore()

      await store.refreshUser()

      expect(api.get).not.toHaveBeenCalled()
    })
  })

  describe('computed properties', () => {
    it('userName returns user name when authenticated', async () => {
      const mockResponse = {
        token: 'jwt-token',
        user: {
          id: 'user-1',
          email: 'test@example.com',
          name: 'John Doe',
          role: UserRole.Viewer,
          teamId: 'team-1',
        },
      }

      vi.mocked(api.post).mockResolvedValueOnce(mockResponse)

      const store = useAuthStore()
      await store.login({ email: 'test@example.com', password: 'password' })

      expect(store.userName).toBe('John Doe')
    })

    it('isAdmin is true for Admin role', async () => {
      const mockResponse = {
        token: 'jwt-token',
        user: {
          id: 'user-1',
          email: 'admin@example.com',
          name: 'Admin User',
          role: UserRole.Admin,
          teamId: 'team-1',
        },
      }

      vi.mocked(api.post).mockResolvedValueOnce(mockResponse)

      const store = useAuthStore()
      await store.login({ email: 'admin@example.com', password: 'password' })

      expect(store.isAdmin).toBe(true)
    })

    it('isAdmin is true for SystemAdmin role', async () => {
      const mockResponse = {
        token: 'jwt-token',
        user: {
          id: 'user-1',
          email: 'sysadmin@example.com',
          name: 'SysAdmin User',
          role: UserRole.SystemAdmin,
          teamId: 'team-1',
        },
      }

      vi.mocked(api.post).mockResolvedValueOnce(mockResponse)

      const store = useAuthStore()
      await store.login({ email: 'sysadmin@example.com', password: 'password' })

      expect(store.isAdmin).toBe(true)
    })

    it('isAdmin is false for Viewer role', async () => {
      const mockResponse = {
        token: 'jwt-token',
        user: {
          id: 'user-1',
          email: 'viewer@example.com',
          name: 'Viewer User',
          role: UserRole.Viewer,
          teamId: 'team-1',
        },
      }

      vi.mocked(api.post).mockResolvedValueOnce(mockResponse)

      const store = useAuthStore()
      await store.login({ email: 'viewer@example.com', password: 'password' })

      expect(store.isAdmin).toBe(false)
    })
  })
})
