export interface User {
  id: string
  email: string
  name: string
  role: UserRole
  isActive: boolean
  createdAt: string
  lastLoginAt?: string
}

export enum UserRole {
  Viewer = 'Viewer',
  Contributor = 'Contributor',
  Analyst = 'Analyst',
  Admin = 'Admin',
  SystemAdmin = 'SystemAdmin'
}

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  token: string
  refreshToken?: string
  expiresAt: string
  user: User
}

export interface RegisterRequest {
  email: string
  password: string
  name: string
}

export interface Team {
  id: string
  name: string
  description?: string
  createdBy: string
  isActive: boolean
  createdAt: string
}
