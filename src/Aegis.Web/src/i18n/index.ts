import { createI18n } from 'vue-i18n'
import en from './locales/en.json'
import es from './locales/es.json'
import fr from './locales/fr.json'
import de from './locales/de.json'
import zh from './locales/zh.json'

// Type-safe locale messages
export type MessageSchema = typeof en

// Supported locales
export const SUPPORTED_LOCALES = [
  { code: 'en', name: 'English', flag: '🇺🇸' },
  { code: 'es', name: 'Español', flag: '🇪🇸' },
  { code: 'fr', name: 'Français', flag: '🇫🇷' },
  { code: 'de', name: 'Deutsch', flag: '🇩🇪' },
  { code: 'zh', name: '中文', flag: '🇨🇳' }
] as const

export type LocaleCode = typeof SUPPORTED_LOCALES[number]['code']

// Get saved locale or detect from browser
const getSavedLocale = (): string => {
  const saved = localStorage.getItem('aegis-locale')
  if (saved && SUPPORTED_LOCALES.some(l => l.code === saved)) {
    return saved
  }

  // Try to detect from browser
  const browserLang = navigator.language.split('-')[0]
  if (SUPPORTED_LOCALES.some(l => l.code === browserLang)) {
    return browserLang
  }

  return 'en'
}

// Create i18n instance
const i18n = createI18n({
  locale: getSavedLocale(),
  fallbackLocale: 'en',
  messages: {
    en,
    es,
    fr,
    de,
    zh
  },
  // Suppress missing translation warnings in development
  missingWarn: false,
  fallbackWarn: false
})

// Helper to change locale
export const setLocale = (locale: LocaleCode) => {
  // @ts-expect-error - vue-i18n typing issue with locale
  i18n.global.locale = locale
  localStorage.setItem('aegis-locale', locale)
  document.documentElement.lang = locale
}

// Helper to get current locale
export const getLocale = (): LocaleCode => {
  return i18n.global.locale as unknown as LocaleCode
}

export default i18n
