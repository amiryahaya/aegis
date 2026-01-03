/**
 * Performance & Virtual Scroll Types
 */

export interface VirtualScrollOptions {
  /** Total number of items */
  itemCount: number
  /** Height of each item in pixels */
  itemHeight: number
  /** Container height in pixels (or 'auto' to use viewport) */
  containerHeight?: number | 'auto'
  /** Number of items to render outside visible area (buffer) */
  overscan?: number
  /** Estimated item height for variable height items */
  estimatedItemHeight?: number
}

export interface VirtualScrollItem<T = unknown> {
  /** The item data */
  item: T
  /** Index in the original array */
  index: number
  /** CSS style for positioning */
  style: {
    position: 'absolute'
    top: string
    left: number
    right: number
  }
}

export interface PerformanceMetrics {
  /** Frames per second */
  fps: number
  /** Memory usage in MB (if available) */
  memoryUsage: number | null
  /** Time since last frame in ms */
  frameTime: number
  /** Long tasks detected (>50ms) */
  longTasks: number
  /** Cumulative Layout Shift score */
  cls: number
  /** Largest Contentful Paint in ms */
  lcp: number | null
  /** First Input Delay in ms */
  fid: number | null
  /** Time to Interactive in ms */
  tti: number | null
}

export interface LazyLoadOptions {
  /** Root element for intersection (default: viewport) */
  root?: Element | Document | null
  /** Margin around root */
  rootMargin?: string
  /** Threshold(s) at which to trigger callback */
  threshold?: number | number[]
  /** Only trigger once then stop observing */
  once?: boolean
}

export interface InfiniteScrollState {
  /** Is currently loading more items */
  isLoading: boolean
  /** All items have been loaded */
  isFinished: boolean
  /** An error occurred while loading */
  hasError: boolean
  /** Current page number */
  page: number
  /** Total pages available */
  totalPages: number
}

export interface SkeletonLoaderType {
  type: 'text' | 'circle' | 'rect' | 'card' | 'avatar' | 'button' | 'input'
  width?: number | string
  height?: number | string
  lines?: number
  animation?: 'pulse' | 'wave' | 'none'
  rounded?: 'none' | 'sm' | 'md' | 'lg' | 'full'
}

export interface ImageLoadState {
  /** Image has been loaded */
  isLoaded: boolean
  /** Image is currently loading */
  isLoading: boolean
  /** Image failed to load */
  hasError: boolean
  /** Natural width of the image */
  naturalWidth: number | null
  /** Natural height of the image */
  naturalHeight: number | null
}

export interface PerformanceGrade {
  score: number
  grade: 'A' | 'B' | 'C' | 'D' | 'F'
  label: string
  color: string
}

export const PERFORMANCE_GRADES: Record<string, PerformanceGrade> = {
  A: { score: 90, grade: 'A', label: 'Excellent', color: 'text-green-500' },
  B: { score: 80, grade: 'B', label: 'Good', color: 'text-blue-500' },
  C: { score: 70, grade: 'C', label: 'Needs Improvement', color: 'text-yellow-500' },
  D: { score: 60, grade: 'D', label: 'Poor', color: 'text-orange-500' },
  F: { score: 0, grade: 'F', label: 'Critical', color: 'text-red-500' }
}

export function getPerformanceGrade(score: number): PerformanceGrade {
  if (score >= 90) return PERFORMANCE_GRADES.A
  if (score >= 80) return PERFORMANCE_GRADES.B
  if (score >= 70) return PERFORMANCE_GRADES.C
  if (score >= 60) return PERFORMANCE_GRADES.D
  return PERFORMANCE_GRADES.F
}
