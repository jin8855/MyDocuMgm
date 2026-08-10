import { createApp, nextTick } from 'vue'
import { router } from '../../src/MyDocuMgm.Web/src/app/router'
import { api } from '../../src/MyDocuMgm.Web/src/shared/api/client'

async function flush() {
  await Promise.resolve()
  await new Promise(resolve => setTimeout(resolve, 0))
  await nextTick()
}

function setValue(element: HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement, value: string) {
  element.value = value
  element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? 'change' : 'input', { bubbles: true }))
}

async function createCategoryEditContent() {
  const intake = await api.createInstagramIntake(
    `https://www.instagram.com/p/category-${crypto.randomUUID()}/`,
  )
  await api.saveManualInstagram(intake.id, '직접 입력한 Caption', 'NONE', '', [])
  await api.saveAnalysisReview(intake.id, '직접 확정한 제목', '직접 확정한 요약', true, (await api.content(intake.id)).rowVersion)
  return intake.id
}

describe('Phase 2B category edit', () => {
  afterEach(() => vi.restoreAllMocks())

  it('selects a category, saves a draft, and restores values on reentry', async () => {
    const contentId = await createCategoryEditContent()
    await router.push(`/workflow/${contentId}/category-edit`)
    await router.isReady()
    const host = document.createElement('div')
    const app = createApp({ template: '<RouterView />' })
    app.use(router)
    app.mount(host)
    await flush()
    await flush()

    expect(host.querySelector('[data-testid="category-edit-stage"]')).not.toBeNull()
    expect(host.textContent).toContain('직접 확정한 제목')
    expect(host.textContent).toContain('자동 분류 없이')

    const categories = await api.categories()
    const travel = categories.find(category => category.code === 'TRAVEL')!
    setValue(host.querySelector<HTMLSelectElement>('#category-edit-category')!, travel.id)
    await flush()
    setValue(host.querySelector<HTMLInputElement>('#category-field-destination')!, '  제주  ')
    setValue(host.querySelector<HTMLInputElement>('#category-field-transportation')!, '기차')
    ;[...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.trim() === '임시저장')!.click()
    await flush()
    await flush()

    expect((await api.content(contentId)).currentWorkflowStep).toBe('CATEGORY_EDIT')
    expect((await api.categoryEdit(contentId)).valuesByCategory.TRAVEL.destination).toBe('  제주  ')

    app.unmount()
    const secondHost = document.createElement('div')
    const secondApp = createApp({ template: '<RouterView />' })
    secondApp.use(router)
    secondApp.mount(secondHost)
    await flush()
    await flush()

    expect(secondHost.querySelector<HTMLSelectElement>('#category-edit-category')?.value).toBe(travel.id)
    expect(secondHost.querySelector<HTMLInputElement>('#category-field-destination')?.value).toBe('  제주  ')
    secondApp.unmount()
  })

  it('advances to image only after the completion API succeeds', async () => {
    const contentId = await createCategoryEditContent()
    await router.push(`/workflow/${contentId}/category-edit`)
    const host = document.createElement('div')
    const app = createApp({ template: '<RouterView />' })
    app.use(router)
    app.mount(host)
    await flush()
    await flush()

    const failure = vi.spyOn(api, 'saveCategoryEdit').mockRejectedValueOnce(new Error('결정적 저장 실패'))
    const next = [...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.includes('다음 · 이미지'))!
    next.click()
    await flush()

    expect(failure).toHaveBeenCalledTimes(1)
    expect(router.currentRoute.value.path).toBe(`/workflow/${contentId}/category-edit`)
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('결정적 저장 실패')

    next.click()
    await flush()
    await flush()

    expect((await api.content(contentId)).currentWorkflowStep).toBe('MEDIA')
    expect(router.currentRoute.value.path).toBe(`/workflow/${contentId}/media`)
    app.unmount()
  })

  it('does not depend on a cooking ingredient reload after completion succeeds', async () => {
    const contentId = await createCategoryEditContent()
    await router.push(`/workflow/${contentId}/category-edit`)
    const host = document.createElement('div')
    const app = createApp({ template: '<RouterView />' })
    app.use(router)
    app.mount(host)
    await flush()
    await flush()

    const cooking = (await api.categories()).find(category => category.code === 'COOKING')!
    setValue(host.querySelector<HTMLSelectElement>('#category-edit-category')!, cooking.id)
    await flush()
    const ingredients = vi.spyOn(api, 'ingredients').mockRejectedValue(new Error('재료 조회 실패'))
    ;[...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.includes('다음 · 이미지'))!.click()
    await flush()
    await flush()

    expect(ingredients).not.toHaveBeenCalled()
    expect((await api.content(contentId)).currentWorkflowStep).toBe('MEDIA')
    expect(router.currentRoute.value.path).toBe(`/workflow/${contentId}/media`)
    app.unmount()
  })

  it('keeps the loaded cooking ingredients while the unsaved category selection is toggled', async () => {
    const contentId = await createCategoryEditContent()
    const ingredient = {
      id: 'synthetic-ingredient',
      sortOrder: 1,
      name: '합성 재료',
      quantity: '1개',
      ingredientType: '주재료' as const,
      isPrimary: true,
      note: '',
      rowVersion: 'AAAAAAAAB9E=',
    }
    vi.spyOn(api, 'ingredients').mockResolvedValue([ingredient])
    await router.push(`/workflow/${contentId}/category-edit`)
    const host = document.createElement('div')
    const app = createApp({ template: '<RouterView />' })
    app.use(router)
    app.mount(host)
    await flush()
    await flush()

    const categories = await api.categories()
    const cooking = categories.find(category => category.code === 'COOKING')!
    const product = categories.find(category => category.code === 'PRODUCT')!
    setValue(host.querySelector<HTMLSelectElement>('#category-edit-category')!, cooking.id)
    await flush()
    ;[...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.trim() === '임시저장')!.click()
    await flush()
    await flush()
    expect(host.textContent).toContain('합성 재료')

    setValue(host.querySelector<HTMLSelectElement>('#category-edit-category')!, product.id)
    await flush()
    setValue(host.querySelector<HTMLSelectElement>('#category-edit-category')!, cooking.id)
    await flush()

    expect(host.textContent).toContain('합성 재료')
    app.unmount()
  })

  it('blocks direct category edit access before analysis review completion', async () => {
    const intake = await api.createInstagramIntake(
      `https://www.instagram.com/reel/category-bypass-${crypto.randomUUID()}/`,
    )

    await expect(api.categoryEdit(intake.id)).rejects.toMatchObject({ code: 'CATEGORY_EDIT_NOT_AVAILABLE' })
    await router.push(`/workflow/${intake.id}/category-edit`)
    const host = document.createElement('div')
    const app = createApp({ template: '<RouterView />' })
    app.use(router)
    app.mount(host)
    await flush()
    await flush()

    expect(host.textContent).toContain('분석 검토를 완료한 뒤')
    expect(host.querySelector('[data-testid="category-edit-stage"]')).toBeNull()
    app.unmount()
  })
})
