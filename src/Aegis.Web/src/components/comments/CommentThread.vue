<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { Menu, MenuButton, MenuItem, MenuItems } from '@headlessui/vue'
import {
  ChatBubbleLeftIcon,
  EllipsisVerticalIcon,
  PencilIcon,
  TrashIcon,
  CheckCircleIcon,
  ArrowPathIcon,
  MapPinIcon,
  FaceSmileIcon,
  PaperAirplaneIcon,
  UserCircleIcon
} from '@heroicons/vue/24/outline'
import { CheckCircleIcon as CheckCircleSolidIcon, MapPinIcon as MapPinSolidIcon } from '@heroicons/vue/24/solid'
import { useCommentsStore } from '@/stores/comments'
import { useAuthStore } from '@/stores/auth'
import type { Comment, CommentableResourceType, ReactionType, CreateCommentRequest } from '@/types/comments'
import { getReactionEmoji, formatRelativeTime } from '@/types/comments'

const props = defineProps<{
  resourceId: string
  resourceType: CommentableResourceType
  showInput?: boolean
}>()

const emit = defineEmits<{
  commentAdded: [comment: Comment]
}>()

const commentsStore = useCommentsStore()
const authStore = useAuthStore()

const newComment = ref('')
const replyingTo = ref<string | null>(null)
const replyContent = ref('')
const editingComment = ref<string | null>(null)
const editContent = ref('')
const showReactionPicker = ref<string | null>(null)
const expandedReplies = ref<Set<string>>(new Set())

const reactions: ReactionType[] = ['Like', 'Love', 'Laugh', 'Celebrate', 'Insightful', 'Question', 'Agree', 'Disagree']

const isLoading = computed(() => commentsStore.isLoading)
const comments = computed(() => commentsStore.comments)
const hasComments = computed(() => commentsStore.hasComments)
const currentUserId = computed(() => authStore.user?.id)

onMounted(async () => {
  await commentsStore.fetchComments(props.resourceId, props.resourceType)
})

async function handleSubmitComment() {
  if (!newComment.value.trim()) return

  const request: CreateCommentRequest = {
    resourceId: props.resourceId,
    resourceType: props.resourceType,
    content: newComment.value.trim()
  }

  const comment = await commentsStore.createComment(request)
  if (comment) {
    newComment.value = ''
    emit('commentAdded', comment)
  }
}

async function handleSubmitReply(parentId: string) {
  if (!replyContent.value.trim()) return

  const request: CreateCommentRequest = {
    resourceId: props.resourceId,
    resourceType: props.resourceType,
    content: replyContent.value.trim(),
    parentCommentId: parentId
  }

  const comment = await commentsStore.createComment(request)
  if (comment) {
    replyContent.value = ''
    replyingTo.value = null
    expandedReplies.value.add(parentId)
  }
}

async function handleEditComment(commentId: string) {
  if (!editContent.value.trim()) return

  const comment = await commentsStore.updateComment(commentId, {
    content: editContent.value.trim()
  })

  if (comment) {
    editingComment.value = null
    editContent.value = ''
  }
}

async function handleDeleteComment(commentId: string) {
  if (confirm('Are you sure you want to delete this comment?')) {
    await commentsStore.deleteComment(commentId)
  }
}

async function handleResolve(commentId: string) {
  await commentsStore.resolveComment(commentId)
}

async function handleReopen(commentId: string) {
  await commentsStore.reopenComment(commentId)
}

async function handlePin(commentId: string) {
  await commentsStore.pinComment(commentId)
}

async function handleUnpin(commentId: string) {
  await commentsStore.unpinComment(commentId)
}

async function handleReaction(commentId: string, reactionType: ReactionType) {
  await commentsStore.addReaction(commentId, reactionType)
  showReactionPicker.value = null
}

async function toggleReplies(commentId: string) {
  if (expandedReplies.value.has(commentId)) {
    expandedReplies.value.delete(commentId)
  } else {
    expandedReplies.value.add(commentId)
    if (!commentsStore.replies[commentId]) {
      await commentsStore.fetchReplies(commentId)
    }
  }
}

function startEditing(comment: Comment) {
  editingComment.value = comment.id
  editContent.value = comment.content
}

function cancelEditing() {
  editingComment.value = null
  editContent.value = ''
}

function startReplying(commentId: string) {
  replyingTo.value = commentId
  replyContent.value = ''
}

function cancelReplying() {
  replyingTo.value = null
  replyContent.value = ''
}

function isOwner(comment: Comment): boolean {
  return comment.authorId === currentUserId.value
}
</script>

<template>
  <div class="space-y-4">
    <!-- New Comment Input -->
    <div v-if="showInput !== false" class="flex gap-3">
      <UserCircleIcon class="h-8 w-8 text-gray-400 flex-shrink-0" />
      <div class="flex-1">
        <textarea
          v-model="newComment"
          rows="2"
          class="w-full rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 px-3 py-2 text-sm text-gray-900 dark:text-white placeholder-gray-500 dark:placeholder-gray-400 focus:ring-2 focus:ring-aegis-500 focus:border-transparent resize-none"
          placeholder="Add a comment..."
          @keydown.meta.enter="handleSubmitComment"
          @keydown.ctrl.enter="handleSubmitComment"
        />
        <div class="mt-2 flex justify-end">
          <button
            class="btn-primary text-sm"
            :disabled="!newComment.trim() || isLoading"
            @click="handleSubmitComment"
          >
            <PaperAirplaneIcon class="h-4 w-4 mr-1" />
            Comment
          </button>
        </div>
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="isLoading && !hasComments" class="space-y-4">
      <div v-for="i in 3" :key="i" class="animate-pulse flex gap-3">
        <div class="h-8 w-8 bg-gray-200 dark:bg-gray-700 rounded-full"></div>
        <div class="flex-1 space-y-2">
          <div class="h-4 bg-gray-200 dark:bg-gray-700 rounded w-1/4"></div>
          <div class="h-3 bg-gray-200 dark:bg-gray-700 rounded w-3/4"></div>
        </div>
      </div>
    </div>

    <!-- Empty State -->
    <div v-else-if="!hasComments" class="text-center py-8">
      <ChatBubbleLeftIcon class="mx-auto h-10 w-10 text-gray-400" />
      <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">No comments yet. Be the first to comment!</p>
    </div>

    <!-- Comments List -->
    <div v-else class="space-y-4">
      <div
        v-for="comment in comments"
        :key="comment.id"
        class="group"
        :class="{ 'opacity-60': comment.isResolved }"
      >
        <!-- Comment -->
        <div class="flex gap-3">
          <!-- Avatar -->
          <div class="flex-shrink-0">
            <img
              v-if="comment.authorAvatarUrl"
              :src="comment.authorAvatarUrl"
              :alt="comment.authorName"
              class="h-8 w-8 rounded-full"
            />
            <UserCircleIcon v-else class="h-8 w-8 text-gray-400" />
          </div>

          <!-- Content -->
          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-2">
              <span class="font-medium text-sm text-gray-900 dark:text-white">
                {{ comment.authorName }}
              </span>
              <span class="text-xs text-gray-500 dark:text-gray-400">
                {{ formatRelativeTime(comment.createdAt) }}
              </span>
              <span v-if="comment.isEdited" class="text-xs text-gray-400 dark:text-gray-500">
                (edited)
              </span>
              <CheckCircleSolidIcon
                v-if="comment.isResolved"
                class="h-4 w-4 text-green-500"
                title="Resolved"
              />
              <MapPinSolidIcon
                v-if="comment.isPinned"
                class="h-4 w-4 text-aegis-500"
                title="Pinned"
              />
            </div>

            <!-- Edit Mode -->
            <div v-if="editingComment === comment.id" class="mt-2">
              <textarea
                v-model="editContent"
                rows="2"
                class="w-full rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 px-3 py-2 text-sm text-gray-900 dark:text-white focus:ring-2 focus:ring-aegis-500 focus:border-transparent resize-none"
              />
              <div class="mt-2 flex gap-2">
                <button class="btn-primary text-xs" @click="handleEditComment(comment.id)">
                  Save
                </button>
                <button class="btn-secondary text-xs" @click="cancelEditing">
                  Cancel
                </button>
              </div>
            </div>

            <!-- Comment Content -->
            <p v-else class="mt-1 text-sm text-gray-700 dark:text-gray-300 whitespace-pre-wrap">
              {{ comment.content }}
            </p>

            <!-- Actions -->
            <div class="mt-2 flex items-center gap-3">
              <!-- Reactions Display -->
              <div v-if="Object.keys(comment.reactionCounts).length > 0" class="flex items-center gap-1">
                <button
                  v-for="(count, type) in comment.reactionCounts"
                  :key="type"
                  class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs bg-gray-100 dark:bg-gray-700 hover:bg-gray-200 dark:hover:bg-gray-600"
                  :title="`${count} ${type}`"
                >
                  {{ getReactionEmoji(type as ReactionType) }}
                  <span>{{ count }}</span>
                </button>
              </div>

              <!-- Action Buttons -->
              <div class="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
                <!-- React -->
                <div class="relative">
                  <button
                    class="hover:text-gray-700 dark:hover:text-gray-200 transition-colors"
                    @click="showReactionPicker = showReactionPicker === comment.id ? null : comment.id"
                  >
                    <FaceSmileIcon class="h-4 w-4" />
                  </button>

                  <!-- Reaction Picker -->
                  <div
                    v-if="showReactionPicker === comment.id"
                    class="absolute left-0 bottom-full mb-1 flex gap-1 p-1 bg-white dark:bg-gray-800 rounded-lg shadow-lg border border-gray-200 dark:border-gray-700 z-10"
                  >
                    <button
                      v-for="reaction in reactions"
                      :key="reaction"
                      class="p-1 hover:bg-gray-100 dark:hover:bg-gray-700 rounded"
                      :title="reaction"
                      @click="handleReaction(comment.id, reaction)"
                    >
                      {{ getReactionEmoji(reaction) }}
                    </button>
                  </div>
                </div>

                <!-- Reply -->
                <button
                  class="hover:text-gray-700 dark:hover:text-gray-200 transition-colors"
                  @click="startReplying(comment.id)"
                >
                  Reply
                </button>

                <!-- Toggle Replies -->
                <button
                  v-if="comment.replyCount > 0"
                  class="hover:text-gray-700 dark:hover:text-gray-200 transition-colors"
                  @click="toggleReplies(comment.id)"
                >
                  {{ expandedReplies.has(comment.id) ? 'Hide' : 'Show' }} {{ comment.replyCount }} {{ comment.replyCount === 1 ? 'reply' : 'replies' }}
                </button>

                <!-- More Actions -->
                <Menu as="div" class="relative inline-block text-left opacity-0 group-hover:opacity-100 transition-opacity">
                  <MenuButton class="hover:text-gray-700 dark:hover:text-gray-200 transition-colors">
                    <EllipsisVerticalIcon class="h-4 w-4" />
                  </MenuButton>

                  <transition
                    enter-active-class="transition duration-100 ease-out"
                    enter-from-class="transform scale-95 opacity-0"
                    enter-to-class="transform scale-100 opacity-100"
                    leave-active-class="transition duration-75 ease-in"
                    leave-from-class="transform scale-100 opacity-100"
                    leave-to-class="transform scale-95 opacity-0"
                  >
                    <MenuItems class="absolute right-0 mt-1 w-40 origin-top-right rounded-md bg-white dark:bg-gray-800 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none z-10">
                      <div class="py-1">
                        <MenuItem v-if="isOwner(comment)" v-slot="{ active }">
                          <button
                            class="flex w-full items-center gap-2 px-4 py-2 text-sm"
                            :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
                            @click="startEditing(comment)"
                          >
                            <PencilIcon class="h-4 w-4" />
                            Edit
                          </button>
                        </MenuItem>

                        <MenuItem v-if="!comment.isResolved" v-slot="{ active }">
                          <button
                            class="flex w-full items-center gap-2 px-4 py-2 text-sm"
                            :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
                            @click="handleResolve(comment.id)"
                          >
                            <CheckCircleIcon class="h-4 w-4" />
                            Resolve
                          </button>
                        </MenuItem>

                        <MenuItem v-else v-slot="{ active }">
                          <button
                            class="flex w-full items-center gap-2 px-4 py-2 text-sm"
                            :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
                            @click="handleReopen(comment.id)"
                          >
                            <ArrowPathIcon class="h-4 w-4" />
                            Reopen
                          </button>
                        </MenuItem>

                        <MenuItem v-if="!comment.isPinned" v-slot="{ active }">
                          <button
                            class="flex w-full items-center gap-2 px-4 py-2 text-sm"
                            :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
                            @click="handlePin(comment.id)"
                          >
                            <MapPinIcon class="h-4 w-4" />
                            Pin
                          </button>
                        </MenuItem>

                        <MenuItem v-else v-slot="{ active }">
                          <button
                            class="flex w-full items-center gap-2 px-4 py-2 text-sm"
                            :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
                            @click="handleUnpin(comment.id)"
                          >
                            <MapPinIcon class="h-4 w-4" />
                            Unpin
                          </button>
                        </MenuItem>

                        <MenuItem v-if="isOwner(comment)" v-slot="{ active }">
                          <button
                            class="flex w-full items-center gap-2 px-4 py-2 text-sm text-red-600 dark:text-red-400"
                            :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
                            @click="handleDeleteComment(comment.id)"
                          >
                            <TrashIcon class="h-4 w-4" />
                            Delete
                          </button>
                        </MenuItem>
                      </div>
                    </MenuItems>
                  </transition>
                </Menu>
              </div>
            </div>

            <!-- Reply Input -->
            <div v-if="replyingTo === comment.id" class="mt-3 flex gap-2">
              <textarea
                v-model="replyContent"
                rows="2"
                class="flex-1 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 px-3 py-2 text-sm text-gray-900 dark:text-white placeholder-gray-500 focus:ring-2 focus:ring-aegis-500 focus:border-transparent resize-none"
                placeholder="Write a reply..."
                @keydown.meta.enter="handleSubmitReply(comment.id)"
                @keydown.ctrl.enter="handleSubmitReply(comment.id)"
              />
              <div class="flex flex-col gap-1">
                <button
                  class="btn-primary text-xs px-2 py-1"
                  :disabled="!replyContent.trim()"
                  @click="handleSubmitReply(comment.id)"
                >
                  Reply
                </button>
                <button class="btn-secondary text-xs px-2 py-1" @click="cancelReplying">
                  Cancel
                </button>
              </div>
            </div>

            <!-- Replies -->
            <div v-if="expandedReplies.has(comment.id) && commentsStore.replies[comment.id]" class="mt-3 ml-4 space-y-3 border-l-2 border-gray-200 dark:border-gray-700 pl-4">
              <div
                v-for="reply in commentsStore.replies[comment.id]"
                :key="reply.id"
                class="flex gap-3"
              >
                <div class="flex-shrink-0">
                  <img
                    v-if="reply.authorAvatarUrl"
                    :src="reply.authorAvatarUrl"
                    :alt="reply.authorName"
                    class="h-6 w-6 rounded-full"
                  />
                  <UserCircleIcon v-else class="h-6 w-6 text-gray-400" />
                </div>
                <div class="flex-1 min-w-0">
                  <div class="flex items-center gap-2">
                    <span class="font-medium text-xs text-gray-900 dark:text-white">
                      {{ reply.authorName }}
                    </span>
                    <span class="text-xs text-gray-500 dark:text-gray-400">
                      {{ formatRelativeTime(reply.createdAt) }}
                    </span>
                  </div>
                  <p class="mt-0.5 text-sm text-gray-700 dark:text-gray-300">
                    {{ reply.content }}
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Load More -->
    <div v-if="commentsStore.hasMore" class="flex justify-center">
      <button class="btn-secondary text-sm" :disabled="isLoading">
        Load more comments
      </button>
    </div>
  </div>
</template>
