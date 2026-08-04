import { createApp, nextTick } from 'vue'
import UrlStage from '../../src/MyDocuMgm.Web/src/features/content-workflow/UrlStage.vue'
import { ApiError, api } from '../../src/MyDocuMgm.Web/src/shared/api/client'

async function flush() {
  await Promise.resolve()
  await new Promise(resolve => setTimeout(resolve, 0))
  await nextTick()
}

function setValue(element: HTMLInputElement | HTMLTextAreaElement, value: string) {
  element.value = value
  element.dispatchEvent(new Event('input', { bubbles: true }))
}

function mountStage(contentId = `new-${crypto.randomUUID()}`, newWork = true) {
  const host = document.createElement('div')
  let acceptedId = ''
  const app = createApp(UrlStage, {
    contentId,
    newWork,
    onAccepted: (id: string) => { acceptedId = id },
  })
  app.mount(host)
  return { host, app, acceptedId: () => acceptedId }
}

async function submitUrl(host: HTMLElement, url: string) {
  setValue(host.querySelector<HTMLInputElement>('#source-url')!, url)
  host.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
  await flush()
  await flush()
}

describe('Phase 2A URL intake web contract', () => {
  it('shows invalid URL validation and supports retry', async () => {
    const { host, app } = mountStage()

    await submitUrl(host, 'file:///c:/private.txt')
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('http 또는 https')

    await submitUrl(host, `https://example.com/retry-${crypto.randomUUID()}`)
    expect(host.textContent).toContain('URL을 저장했습니다')
    expect(host.textContent).toContain('수집 대기')
    app.unmount()
  })

  it('loads the existing record and duplicate message for an equivalent URL', async () => {
    const suffix = crypto.randomUUID()
    const first = await api.createUrlIntake(`https://EXAMPLE.com:443/Case-${suffix}#one`)
    const { host, app } = mountStage()

    await submitUrl(host, `https://example.com/Case-${suffix}#two`)

    expect(host.textContent).toContain('이미 등록된 URL입니다. 기존 작업을 불러왔습니다.')
    expect(host.textContent).toContain(first.normalizedUrl)
    app.unmount()
  })

  it('switches to manual input, blocks blank body, and restores saved body', async () => {
    const { host, app } = mountStage()
    await submitUrl(host, `https://example.com/manual-${crypto.randomUUID()}`)
    const id = host.querySelector<HTMLElement>('.intake-summary') ?
      (await api.createUrlIntake((host.querySelector<HTMLInputElement>('#source-url')!).value)).id : ''

    const manualButton = [...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.trim() === '본문 직접 입력')!
    manualButton.click()
    await flush()
    const textarea = host.querySelector<HTMLTextAreaElement>('#manual-body')!
    expect(textarea.maxLength).toBe(20_000)
    setValue(textarea, '   ')
    ;[...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.trim() === '본문 저장')!.click()
    await flush()
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('본문을 입력')

    setValue(textarea, '  synthetic manual body  ')
    ;[...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.trim() === '본문 저장')!.click()
    await flush()
    expect(host.textContent).toContain('본문 준비 완료')
    app.unmount()

    const reentered = mountStage(id, false)
    await flush()
    await flush()
    expect(reentered.host.querySelector<HTMLTextAreaElement>('#manual-body')?.value)
      .toBe('synthetic manual body')
    reentered.app.unmount()
  })

  it('disables duplicate submission while saving and exposes API retry', async () => {
    const { host, app } = mountStage()
    let resolveRequest!: (value: Awaited<ReturnType<typeof api.createUrlIntake>>) => void
    const delayed = new Promise<Awaited<ReturnType<typeof api.createUrlIntake>>>(resolve => {
      resolveRequest = resolve
    })
    const original = api.createUrlIntake.bind(api)
    const spy = vi.spyOn(api, 'createUrlIntake').mockReturnValueOnce(delayed)
    setValue(host.querySelector<HTMLInputElement>('#source-url')!, 'https://example.com/delayed')
    host.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
    await nextTick()
    expect(host.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(true)
    resolveRequest(await original(`https://example.com/delayed-${crypto.randomUUID()}`))
    await flush()
    spy.mockRejectedValueOnce(new ApiError('UNEXPECTED_ERROR', '일시적인 API 오류'))
    await submitUrl(host, `https://example.com/error-${crypto.randomUUID()}`)
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('일시적인 API 오류')
    spy.mockRestore()
    app.unmount()
  })

  it('selects and unlinks existing media without adding an upload control', async () => {
    const { host, app } = mountStage()
    await submitUrl(host, `https://example.com/media-${crypto.randomUUID()}`)
    const checkbox = host.querySelector<HTMLInputElement>('.media-link-card input[type="checkbox"]')!
    expect(checkbox).toBeTruthy()
    checkbox.checked = true
    checkbox.dispatchEvent(new Event('change', { bubbles: true }))
    await flush()
    expect(host.textContent).toContain('선택 1개 저장')
    ;[...host.querySelectorAll<HTMLButtonElement>('button')]
      .find(button => button.textContent?.includes('선택 1개 저장'))!.click()
    await flush()
    expect(host.textContent).toContain('원본 파일은 변경하지 않았습니다')
    expect(host.querySelector('input[type="file"]')).toBeNull()
    app.unmount()
  })

  it('keeps external fetch and later workflow success out of the URL screen', () => {
    const { host, app } = mountStage()

    expect(host.textContent).toContain('외부 사이트 접속이나 자동 수집은 실행하지 않습니다.')
    expect(host.textContent).not.toContain('수집 완료')
    expect(host.textContent).not.toContain('요약 완료')
    app.unmount()
  })
})
