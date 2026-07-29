import { createApp } from 'vue'
import App from './App.vue'
import { router } from './app/router'
import './shared/styles/base.css'
import './shared/styles/shell.css'
import './shared/styles/features.css'

createApp(App).use(router).mount('#app')
