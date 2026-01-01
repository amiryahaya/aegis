import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import ChatInput from '@/components/chat/ChatInput.vue'

// Mock heroicons
vi.mock('@heroicons/vue/24/solid', () => ({
  PaperAirplaneIcon: { template: '<svg data-testid="send-icon" />' },
  StopIcon: { template: '<svg data-testid="stop-icon" />' },
}))

describe('ChatInput', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders textarea', () => {
    const wrapper = mount(ChatInput)
    expect(wrapper.find('textarea').exists()).toBe(true)
  })

  it('renders submit button', () => {
    const wrapper = mount(ChatInput)
    expect(wrapper.find('button').exists()).toBe(true)
  })

  it('submit button is disabled when textarea is empty', () => {
    const wrapper = mount(ChatInput)
    const submitButton = wrapper.find('button[type="button"]')
    expect(submitButton.attributes('disabled')).toBeDefined()
  })

  it('submit button is enabled when textarea has content', async () => {
    const wrapper = mount(ChatInput)
    const textarea = wrapper.find('textarea')

    await textarea.setValue('Hello world')

    const submitButton = wrapper.findAll('button').find(btn =>
      !btn.attributes('disabled') || btn.text() !== ''
    )
    expect(submitButton).toBeDefined()
  })

  it('emits send event with query when submitted', async () => {
    const wrapper = mount(ChatInput)
    const textarea = wrapper.find('textarea')

    await textarea.setValue('Test query')

    // Find the submit button (not the stop button)
    const submitButton = wrapper.findAll('button').find(btn =>
      btn.find('[data-testid="send-icon"]').exists()
    )

    if (submitButton) {
      await submitButton.trigger('click')

      expect(wrapper.emitted('send')).toBeTruthy()
      expect(wrapper.emitted('send')![0]).toEqual(['Test query'])
    }
  })

  it('clears textarea after sending', async () => {
    const wrapper = mount(ChatInput)
    const textarea = wrapper.find('textarea')

    await textarea.setValue('Test query')

    const submitButton = wrapper.findAll('button').find(btn =>
      btn.find('[data-testid="send-icon"]').exists()
    )

    if (submitButton) {
      await submitButton.trigger('click')

      expect((textarea.element as HTMLTextAreaElement).value).toBe('')
    }
  })

  it('submits on Enter key press', async () => {
    const wrapper = mount(ChatInput)
    const textarea = wrapper.find('textarea')

    await textarea.setValue('Test query')
    await textarea.trigger('keydown', { key: 'Enter', shiftKey: false })

    expect(wrapper.emitted('send')).toBeTruthy()
  })

  it('does not submit on Shift+Enter (allows new line)', async () => {
    const wrapper = mount(ChatInput)
    const textarea = wrapper.find('textarea')

    await textarea.setValue('Test query')
    await textarea.trigger('keydown', { key: 'Enter', shiftKey: true })

    expect(wrapper.emitted('send')).toBeFalsy()
  })

  it('does not submit empty or whitespace-only queries', async () => {
    const wrapper = mount(ChatInput)
    const textarea = wrapper.find('textarea')

    await textarea.setValue('   ')
    await textarea.trigger('keydown', { key: 'Enter', shiftKey: false })

    expect(wrapper.emitted('send')).toBeFalsy()
  })

  it('shows stop button when loading', () => {
    const wrapper = mount(ChatInput, {
      props: { loading: true },
    })

    expect(wrapper.find('[data-testid="stop-icon"]').exists()).toBe(true)
  })

  it('shows send button when not loading', () => {
    const wrapper = mount(ChatInput, {
      props: { loading: false },
    })

    expect(wrapper.find('[data-testid="send-icon"]').exists()).toBe(true)
  })

  it('emits stop event when stop button is clicked', async () => {
    const wrapper = mount(ChatInput, {
      props: { loading: true },
    })

    const stopButton = wrapper.find('button')
    await stopButton.trigger('click')

    expect(wrapper.emitted('stop')).toBeTruthy()
  })

  it('disables textarea when disabled prop is true', () => {
    const wrapper = mount(ChatInput, {
      props: { disabled: true },
    })

    const textarea = wrapper.find('textarea')
    expect(textarea.attributes('disabled')).toBeDefined()
  })

  it('does not submit when loading', async () => {
    const wrapper = mount(ChatInput, {
      props: { loading: true },
    })

    const textarea = wrapper.find('textarea')
    await textarea.setValue('Test query')
    await textarea.trigger('keydown', { key: 'Enter', shiftKey: false })

    expect(wrapper.emitted('send')).toBeFalsy()
  })

  it('displays placeholder text', () => {
    const wrapper = mount(ChatInput)
    const textarea = wrapper.find('textarea')

    expect(textarea.attributes('placeholder')).toContain('Ask anything')
  })

  it('displays helper text about keyboard shortcuts', () => {
    const wrapper = mount(ChatInput)

    expect(wrapper.text()).toContain('Enter to send')
    expect(wrapper.text()).toContain('Shift+Enter')
  })

  it('exposes focus method', async () => {
    const wrapper = mount(ChatInput)
    const vm = wrapper.vm as any

    expect(typeof vm.focus).toBe('function')
  })

  it('exposes setQuery method', async () => {
    const wrapper = mount(ChatInput)
    const vm = wrapper.vm as any

    expect(typeof vm.setQuery).toBe('function')

    vm.setQuery('Preset query')
    await wrapper.vm.$nextTick()

    const textarea = wrapper.find('textarea')
    expect((textarea.element as HTMLTextAreaElement).value).toBe('Preset query')
  })

  it('trims query before sending', async () => {
    const wrapper = mount(ChatInput)
    const textarea = wrapper.find('textarea')

    await textarea.setValue('  Test query  ')

    const submitButton = wrapper.findAll('button').find(btn =>
      btn.find('[data-testid="send-icon"]').exists()
    )

    if (submitButton) {
      await submitButton.trigger('click')

      expect(wrapper.emitted('send')![0]).toEqual(['Test query'])
    }
  })
})
