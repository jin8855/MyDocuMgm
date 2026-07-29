import { createRouter, createWebHistory } from 'vue-router'
import DashboardView from './views/DashboardView.vue'
import ContentDetailView from './views/ContentDetailView.vue'
import ContentFormView from './views/ContentFormView.vue'
import ContentListView from './views/ContentListView.vue'
import MediaView from './views/MediaView.vue'

export const routes = [
  { path: '/', component: DashboardView },
  { path: '/contents', component: ContentListView },
  { path: '/contents/new', component: ContentFormView },
  { path: '/contents/:id', component: ContentDetailView, props: true },
  { path: '/contents/:id/edit', component: ContentFormView, props: true },
  { path: '/contents/:id/media', component: MediaView, props: true },
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
  scrollBehavior: () => ({ top: 0 }),
})
