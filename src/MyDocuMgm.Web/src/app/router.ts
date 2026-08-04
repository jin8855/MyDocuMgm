import { createRouter, createWebHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'
import CategoryManagementPage from '../pages/CategoryManagementPage.vue'
import DashboardPage from '../pages/DashboardPage.vue'
import ErrorStatePage from '../pages/ErrorStatePage.vue'
import MobileReadingPage from '../pages/MobileReadingPage.vue'
import WorkflowPage from '../pages/WorkflowPage.vue'
import WorkListPage from '../pages/WorkListPage.vue'

const newWorkIdPrefix = 'new-'

export function createNewWorkPath(): string {
  return `/workflow/${newWorkIdPrefix}${globalThis.crypto.randomUUID()}/url`
}

export function isNewWorkId(id: string): boolean {
  return id.startsWith(newWorkIdPrefix)
}

export const routes: RouteRecordRaw[] = [
  { path: '/', component: DashboardPage },
  { path: '/contents', component: WorkListPage },
  { path: '/categories', component: CategoryManagementPage },
  { path: '/mobile', component: MobileReadingPage },
  { path: '/workflow/:id/:step', component: WorkflowPage, props: true },
  { path: '/contents/:id', redirect: (route) => `/workflow/${String(route.params.id)}/detail` },
  { path: '/error', component: ErrorStatePage },
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
  scrollBehavior: () => ({ top: 0 }),
})
