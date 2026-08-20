import { createApp, nextTick } from 'vue'
import HelpPopover from '../../src/MyDocuMgm.Web/src/shared/components/HelpPopover.vue'

describe('clickable help popover', () => {
  it('supports click, keyboard, one-open-at-a-time, Escape and outside close', async () => {
    const host = document.createElement('div')
    document.body.append(host)
    const app = createApp({
      components: { HelpPopover },
      template: `
        <div>
          <HelpPopover label="첫 번째 도움말">첫 번째 내용</HelpPopover>
          <HelpPopover label="두 번째 도움말">두 번째 내용</HelpPopover>
          <button id="outside" type="button">바깥</button>
        </div>
      `,
    })
    app.mount(host)
    const buttons = [...host.querySelectorAll<HTMLButtonElement>('.help-trigger')]

    expect(buttons).toHaveLength(2)
    expect(buttons[0].type).toBe('button')
    expect(buttons[0].getAttribute('aria-expanded')).toBe('false')

    buttons[0].click()
    await nextTick()
    await nextTick()
    expect(buttons[0].getAttribute('aria-expanded')).toBe('true')
    expect(host.textContent).toContain('첫 번째 내용')
    const firstPanel = host.querySelector<HTMLElement>('.help-popover-panel')!
    expect(firstPanel.style.top).not.toBe('')
    expect(firstPanel.style.left).not.toBe('')

    buttons[1].dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }))
    await nextTick()
    expect(buttons[0].getAttribute('aria-expanded')).toBe('false')
    expect(buttons[1].getAttribute('aria-expanded')).toBe('true')
    expect(host.textContent).toContain('두 번째 내용')

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }))
    await nextTick()
    expect(buttons[1].getAttribute('aria-expanded')).toBe('false')
    expect(document.activeElement).toBe(buttons[1])

    buttons[0].dispatchEvent(new KeyboardEvent('keydown', { key: ' ', bubbles: true }))
    await nextTick()
    expect(buttons[0].getAttribute('aria-expanded')).toBe('true')
    host.querySelector<HTMLButtonElement>('#outside')!.click()
    document.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true }))
    await nextTick()
    expect(buttons[0].getAttribute('aria-expanded')).toBe('false')

    app.unmount()
    host.remove()
  })
})
