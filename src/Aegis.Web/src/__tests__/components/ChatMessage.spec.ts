import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import ChatMessage from '@/components/chat/ChatMessage.vue'
import type { SessionTurn } from '@/types'

// Mock heroicons
vi.mock('@heroicons/vue/24/solid', () => ({
  UserCircleIcon: { template: '<svg data-testid="user-icon" />' },
  SparklesIcon: { template: '<svg data-testid="sparkles-icon" />' },
}))

describe('ChatMessage', () => {
  const mockTurn: SessionTurn = {
    id: 'turn-1',
    sessionId: 'session-1',
    userQuery: 'What is machine learning?',
    systemResponse: 'Machine learning is a subset of artificial intelligence...',
    sources: [],
    createdAt: new Date().toISOString(),
  }

  it('renders user query', () => {
    const wrapper = mount(ChatMessage, {
      props: { turn: mockTurn },
    })

    expect(wrapper.text()).toContain('What is machine learning?')
  })

  it('renders system response', () => {
    const wrapper = mount(ChatMessage, {
      props: { turn: mockTurn },
    })

    expect(wrapper.text()).toContain('Machine learning is a subset of artificial intelligence')
  })

  it('displays "You" label for user message', () => {
    const wrapper = mount(ChatMessage, {
      props: { turn: mockTurn },
    })

    expect(wrapper.text()).toContain('You')
  })

  it('displays "AEGIS" label for assistant message', () => {
    const wrapper = mount(ChatMessage, {
      props: { turn: mockTurn },
    })

    expect(wrapper.text()).toContain('AEGIS')
  })

  it('shows typing indicator when streaming and no response yet', () => {
    const streamingTurn: SessionTurn = {
      ...mockTurn,
      systemResponse: undefined,
    }

    const wrapper = mount(ChatMessage, {
      props: {
        turn: streamingTurn,
        isStreaming: true,
      },
    })

    expect(wrapper.find('.typing-indicator').exists()).toBe(true)
  })

  it('does not show typing indicator when response is present', () => {
    const wrapper = mount(ChatMessage, {
      props: {
        turn: mockTurn,
        isStreaming: false,
      },
    })

    expect(wrapper.find('.typing-indicator').exists()).toBe(false)
  })

  it('renders sources when present', () => {
    const turnWithSources: SessionTurn = {
      ...mockTurn,
      sources: [
        {
          documentId: 'doc-1',
          documentName: 'ML Guide.pdf',
          relevanceScore: 0.95,
          excerpt: 'Machine learning enables...',
        },
        {
          documentId: 'doc-2',
          documentName: 'AI Overview.pdf',
          relevanceScore: 0.88,
          excerpt: 'Artificial intelligence...',
        },
      ],
    }

    const wrapper = mount(ChatMessage, {
      props: { turn: turnWithSources },
    })

    expect(wrapper.text()).toContain('2 sources')
    expect(wrapper.text()).toContain('ML Guide.pdf')
  })

  it('shows relevance score as percentage', () => {
    const turnWithSources: SessionTurn = {
      ...mockTurn,
      sources: [
        {
          documentId: 'doc-1',
          documentName: 'Test Doc',
          relevanceScore: 0.95,
          excerpt: 'Test excerpt',
        },
      ],
    }

    const wrapper = mount(ChatMessage, {
      props: { turn: turnWithSources },
    })

    expect(wrapper.text()).toContain('95% match')
  })

  it('renders follow-up questions when present', async () => {
    const turnWithFollowUp: SessionTurn = {
      ...mockTurn,
      followUpQuestions: [
        'How does neural networks work?',
        'What are the types of ML?',
      ],
    }

    const wrapper = mount(ChatMessage, {
      props: { turn: turnWithFollowUp },
    })

    expect(wrapper.text()).toContain('How does neural networks work?')
    expect(wrapper.text()).toContain('What are the types of ML?')
  })

  it('emits askFollowUp when follow-up question is clicked', async () => {
    const turnWithFollowUp: SessionTurn = {
      ...mockTurn,
      followUpQuestions: ['What is deep learning?'],
    }

    const wrapper = mount(ChatMessage, {
      props: { turn: turnWithFollowUp },
    })

    const followUpButton = wrapper.find('button')
    await followUpButton.trigger('click')

    expect(wrapper.emitted('askFollowUp')).toBeTruthy()
    expect(wrapper.emitted('askFollowUp')![0]).toEqual(['What is deep learning?'])
  })

  it('displays processing time when available', () => {
    const turnWithProcessingTime: SessionTurn = {
      ...mockTurn,
      processingTimeMs: 1500,
    }

    const wrapper = mount(ChatMessage, {
      props: { turn: turnWithProcessingTime },
    })

    expect(wrapper.text()).toContain('1.5s')
  })

  it('displays metrics when available', () => {
    const turnWithMetrics: SessionTurn = {
      ...mockTurn,
      metrics: {
        tokensUsed: 150,
        sourcesRetrieved: 10,
        sourcesUsed: 3,
        cacheHit: true,
      },
    }

    const wrapper = mount(ChatMessage, {
      props: { turn: turnWithMetrics },
    })

    expect(wrapper.text()).toContain('150 tokens')
    expect(wrapper.text()).toContain('3/10 sources used')
    expect(wrapper.text()).toContain('Cache hit')
  })

  it('formats time correctly', () => {
    const fixedDate = new Date('2024-01-15T14:30:00Z')
    const turnWithFixedDate: SessionTurn = {
      ...mockTurn,
      createdAt: fixedDate.toISOString(),
    }

    const wrapper = mount(ChatMessage, {
      props: { turn: turnWithFixedDate },
    })

    // The formatted time should be present (format depends on locale)
    expect(wrapper.text()).toMatch(/\d{1,2}:\d{2}/)
  })
})
