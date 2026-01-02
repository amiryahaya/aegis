import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { errorTrackingPlugin } from './composables/useErrorTracking'
import './assets/styles/main.css'

const app = createApp(App)

// State management
app.use(createPinia())

// Routing
app.use(router)

// Error tracking (global error handler)
app.use(errorTrackingPlugin)

// Mount app
app.mount('#app')
