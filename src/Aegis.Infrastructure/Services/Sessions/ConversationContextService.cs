using System.Text.RegularExpressions;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Sessions;

public class ConversationContextService : IConversationContextService
{
    private readonly ISessionService _sessionService;
    private readonly ILogger<ConversationContextService> _logger;

    // Simple token estimation (roughly 4 chars per token for English)
    private const int CharsPerToken = 4;

    public ConversationContextService(
        ISessionService sessionService,
        ILogger<ConversationContextService> logger)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<ConversationContext>> BuildContextAsync(
        Guid sessionId,
        BuildContextOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new BuildContextOptions();

        var sessionResult = await _sessionService.GetByIdAsync(sessionId, cancellationToken);
        if (sessionResult.IsFailure)
        {
            return Result<ConversationContext>.Failure(sessionResult.Error!);
        }

        var session = sessionResult.Value;
        var messages = new List<ContextMessage>();
        var totalTokens = 0;

        // Add system prompt if configured
        if (options.IncludeSystemPrompt && !string.IsNullOrEmpty(session.Settings.SystemPrompt))
        {
            var systemTokens = EstimateTokens(session.Settings.SystemPrompt);
            messages.Add(new ContextMessage
            {
                Role = ContextRole.System,
                Content = session.Settings.SystemPrompt,
                TurnNumber = 0,
                Timestamp = session.CreatedAt,
                EstimatedTokens = systemTokens
            });
            totalTokens += systemTokens;
        }

        // Get turns based on strategy
        var turns = GetTurnsForContext(session.Turns, options);

        // Build messages from turns
        foreach (var turn in turns)
        {
            var userTokens = EstimateTokens(turn.UserQuery);
            if (totalTokens + userTokens > options.MaxTokens && options.CompressIfNeeded)
            {
                break;
            }

            messages.Add(new ContextMessage
            {
                Role = ContextRole.User,
                Content = turn.UserQuery,
                TurnNumber = turn.TurnNumber,
                Timestamp = turn.CreatedAt,
                EstimatedTokens = userTokens
            });
            totalTokens += userTokens;

            if (!string.IsNullOrEmpty(turn.SystemResponse))
            {
                var assistantTokens = EstimateTokens(turn.SystemResponse);
                if (totalTokens + assistantTokens > options.MaxTokens && options.CompressIfNeeded)
                {
                    // Truncate response if needed
                    var truncatedResponse = TruncateToTokens(turn.SystemResponse, options.MaxTokens - totalTokens);
                    assistantTokens = EstimateTokens(truncatedResponse);
                    messages.Add(new ContextMessage
                    {
                        Role = ContextRole.Assistant,
                        Content = truncatedResponse,
                        TurnNumber = turn.TurnNumber,
                        Timestamp = turn.CompletedAt ?? turn.CreatedAt,
                        EstimatedTokens = assistantTokens
                    });
                    totalTokens += assistantTokens;
                    break;
                }

                messages.Add(new ContextMessage
                {
                    Role = ContextRole.Assistant,
                    Content = turn.SystemResponse,
                    TurnNumber = turn.TurnNumber,
                    Timestamp = turn.CompletedAt ?? turn.CreatedAt,
                    EstimatedTokens = assistantTokens
                });
                totalTokens += assistantTokens;
            }
        }

        // Extract entities if requested
        var entities = options.IncludeEntities
            ? ExtractEntitiesFromTurns(session.Turns)
            : new List<SessionTrackedEntity>();

        // Get current topic
        var currentTopic = await GetCurrentTopicAsync(sessionId, cancellationToken);

        var context = new ConversationContext
        {
            SessionId = sessionId,
            SystemPrompt = session.Settings.SystemPrompt ?? GetDefaultSystemPrompt(session.Type),
            Messages = messages,
            Entities = entities,
            CurrentTopic = currentTopic.IsSuccess ? currentTopic.Value : null,
            EstimatedTokens = totalTokens
        };

        _logger.LogDebug(
            "Built context for session {SessionId} with {MessageCount} messages, {TokenCount} tokens",
            sessionId, messages.Count, totalTokens);

        return Result<ConversationContext>.Success(context);
    }

    public Task<Result<RewrittenQuery>> RewriteQueryAsync(
        string query,
        ConversationContext context,
        CancellationToken cancellationToken = default)
    {
        var resolvedReferences = new List<string>();
        var expandedTerms = new List<string>();
        var rewrittenText = query;

        // Resolve pronoun references
        rewrittenText = ResolvePronounReferences(rewrittenText, context, resolvedReferences);

        // Expand abbreviated references
        rewrittenText = ExpandAbbreviatedReferences(rewrittenText, context, expandedTerms);

        // Add context from current topic if relevant
        if (context.CurrentTopic != null && !ContainsTopic(query, context.CurrentTopic))
        {
            // Don't modify the query, just note the context
        }

        var confidence = CalculateRewriteConfidence(query, rewrittenText, resolvedReferences);

        var result = new RewrittenQuery
        {
            OriginalQuery = query,
            RewrittenText = rewrittenText,
            ExpandedTerms = expandedTerms,
            ResolvedReferences = resolvedReferences,
            ConfidenceScore = confidence,
            Explanation = resolvedReferences.Any() || expandedTerms.Any()
                ? $"Resolved {resolvedReferences.Count} references, expanded {expandedTerms.Count} terms"
                : "No modifications needed"
        };

        return Task.FromResult(Result<RewrittenQuery>.Success(result));
    }

    public Task<Result<string>> ResolveReferencesAsync(
        string query,
        ConversationContext context,
        CancellationToken cancellationToken = default)
    {
        var resolved = new List<string>();
        var result = ResolvePronounReferences(query, context, resolved);
        return Task.FromResult(Result<string>.Success(result));
    }

    public async Task<Result<EntityTracker>> TrackEntitiesAsync(
        Guid sessionId,
        string text,
        CancellationToken cancellationToken = default)
    {
        var sessionResult = await _sessionService.GetByIdAsync(sessionId, cancellationToken);
        if (sessionResult.IsFailure)
        {
            return Result<EntityTracker>.Failure(sessionResult.Error!);
        }

        var session = sessionResult.Value;
        var entities = ExtractEntitiesFromTurns(session.Turns);

        // Also extract from new text
        var newEntities = ExtractEntitiesFromText(text, session.Turns.Count + 1);
        foreach (var entity in newEntities)
        {
            var existing = entities.FirstOrDefault(e =>
                e.Name.Equals(entity.Name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                // Update existing entity
                var index = entities.IndexOf(existing);
                entities[index] = existing with
                {
                    MentionCount = existing.MentionCount + 1,
                    LastMentionTurn = entity.LastMentionTurn
                };
            }
            else
            {
                entities.Add(entity);
            }
        }

        var tracker = new EntityTracker
        {
            SessionId = sessionId,
            Entities = entities,
            Coreferences = BuildCoreferences(entities)
        };

        return Result<EntityTracker>.Success(tracker);
    }

    public async Task<Result<ConversationTopic>> GetCurrentTopicAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var sessionResult = await _sessionService.GetByIdAsync(sessionId, cancellationToken);
        if (sessionResult.IsFailure)
        {
            return Result<ConversationTopic>.Failure(sessionResult.Error!);
        }

        var session = sessionResult.Value;
        if (!session.Turns.Any())
        {
            return Result<ConversationTopic>.Success(new ConversationTopic
            {
                Name = "New Conversation",
                Description = "No queries yet",
                StartTurn = 0,
                Confidence = 1.0
            });
        }

        // Analyze recent turns to determine topic
        var recentTurns = session.Turns.TakeLast(5).ToList();
        var keywords = ExtractKeywords(recentTurns);
        var topicName = DetermineTopicName(keywords, recentTurns);

        var topic = new ConversationTopic
        {
            Name = topicName,
            Keywords = keywords.Take(10).ToList(),
            StartTurn = FindTopicStartTurn(session.Turns, topicName),
            Confidence = 0.8
        };

        return Result<ConversationTopic>.Success(topic);
    }

    public async Task<Result<TopicShift>> DetectTopicShiftAsync(
        Guid sessionId,
        string newQuery,
        CancellationToken cancellationToken = default)
    {
        var currentTopicResult = await GetCurrentTopicAsync(sessionId, cancellationToken);
        if (currentTopicResult.IsFailure)
        {
            return Result<TopicShift>.Failure(currentTopicResult.Error!);
        }

        var currentTopic = currentTopicResult.Value;
        var queryKeywords = ExtractKeywordsFromText(newQuery);

        // Check overlap with current topic keywords
        var overlap = currentTopic.Keywords
            .Intersect(queryKeywords, StringComparer.OrdinalIgnoreCase)
            .Count();

        var overlapRatio = currentTopic.Keywords.Any()
            ? (double)overlap / currentTopic.Keywords.Count
            : 0;

        var isSignificantShift = overlapRatio < 0.2;

        ConversationTopic? newTopic = null;
        if (isSignificantShift)
        {
            newTopic = new ConversationTopic
            {
                Name = DetermineTopicNameFromKeywords(queryKeywords),
                Keywords = queryKeywords.Take(10).ToList(),
                StartTurn = 0, // Will be updated when turn is added
                Confidence = 0.7
            };
        }

        var shift = new TopicShift
        {
            PreviousTopic = currentTopic,
            NewTopic = newTopic,
            IsSignificantShift = isSignificantShift,
            ShiftScore = 1.0 - overlapRatio,
            Reason = isSignificantShift
                ? $"Low keyword overlap ({overlapRatio:P0}) with current topic"
                : "Query continues current topic"
        };

        return Result<TopicShift>.Success(shift);
    }

    public async Task<Result<SessionConversationSummary>> SummarizeConversationAsync(
        Guid sessionId,
        SummarizeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SummarizeOptions();

        var sessionResult = await _sessionService.GetByIdAsync(sessionId, cancellationToken);
        if (sessionResult.IsFailure)
        {
            return Result<SessionConversationSummary>.Failure(sessionResult.Error!);
        }

        var session = sessionResult.Value;

        // Extract key information
        var questions = session.Turns.Select(t => t.UserQuery).ToList();
        var topics = ExtractKeywords(session.Turns).Take(5).ToList();
        var keyPoints = ExtractKeyPoints(session.Turns);

        // Build summary based on style
        var summaryText = options.Style switch
        {
            SummaryStyle.Bullet => BuildBulletSummary(session, keyPoints),
            SummaryStyle.Detailed => BuildDetailedSummary(session),
            SummaryStyle.Narrative => BuildNarrativeSummary(session),
            _ => BuildConciseSummary(session)
        };

        // Truncate if needed
        if (summaryText.Length > options.MaxLength)
        {
            summaryText = summaryText[..options.MaxLength] + "...";
        }

        var summary = new SessionConversationSummary
        {
            SessionId = sessionId,
            Summary = summaryText,
            KeyPoints = options.IncludeKeyPoints ? keyPoints : new List<string>(),
            QuestionsAsked = options.IncludeQuestions ? questions : new List<string>(),
            TopicsDiscussed = options.IncludeTopics ? topics : new List<string>(),
            UnresolvedQuestions = FindUnresolvedQuestions(session.Turns),
            TurnsCovered = session.Turns.Count
        };

        return Result<SessionConversationSummary>.Success(summary);
    }

    public Task<Result<ConversationContext>> CompressContextAsync(
        ConversationContext context,
        int targetTokens,
        CancellationToken cancellationToken = default)
    {
        if (context.EstimatedTokens <= targetTokens)
        {
            return Task.FromResult(Result<ConversationContext>.Success(context));
        }

        var messages = new List<ContextMessage>();
        var totalTokens = 0;

        // Always keep system prompt
        var systemMessage = context.Messages.FirstOrDefault(m => m.Role == ContextRole.System);
        if (systemMessage != null)
        {
            messages.Add(systemMessage);
            totalTokens += systemMessage.EstimatedTokens;
        }

        // Keep most recent messages first
        var otherMessages = context.Messages
            .Where(m => m.Role != ContextRole.System)
            .OrderByDescending(m => m.TurnNumber)
            .ToList();

        foreach (var message in otherMessages)
        {
            if (totalTokens + message.EstimatedTokens > targetTokens)
            {
                // Try to include truncated version
                var remaining = targetTokens - totalTokens;
                if (remaining > 100) // Only include if we can have meaningful content
                {
                    var truncated = TruncateToTokens(message.Content, remaining);
                    messages.Add(message with
                    {
                        Content = truncated,
                        EstimatedTokens = EstimateTokens(truncated)
                    });
                }
                break;
            }

            messages.Add(message);
            totalTokens += message.EstimatedTokens;
        }

        // Reorder chronologically
        messages = messages.OrderBy(m => m.TurnNumber).ToList();

        var compressed = context with
        {
            Messages = messages,
            EstimatedTokens = totalTokens,
            ConversationSummary = context.ConversationSummary ?? "Context compressed due to length"
        };

        _logger.LogDebug(
            "Compressed context from {Original} to {Compressed} tokens",
            context.EstimatedTokens, totalTokens);

        return Task.FromResult(Result<ConversationContext>.Success(compressed));
    }

    public Task<Result<SessionQueryIntent>> ClassifyIntentAsync(
        string query,
        ConversationContext context,
        CancellationToken cancellationToken = default)
    {
        var queryLower = query.ToLowerInvariant();
        var isFollowUp = IsFollowUpQuery(query, context);

        var intentType = ClassifySessionQueryIntent(queryLower);
        var requiresContext = isFollowUp ||
                              ContainsPronouns(query) ||
                              ContainsReferences(query);

        var parameters = ExtractIntentParameters(query, intentType);

        var intent = new SessionQueryIntent
        {
            Type = intentType,
            Confidence = 0.8,
            Parameters = parameters,
            RequiresContext = requiresContext,
            IsFollowUp = isFollowUp,
            Explanation = $"Classified as {intentType}" + (isFollowUp ? " (follow-up)" : "")
        };

        return Task.FromResult(Result<SessionQueryIntent>.Success(intent));
    }

    public async Task<Result<IReadOnlyList<string>>> GenerateFollowUpsAsync(
        Guid sessionId,
        string lastResponse,
        int count = 3,
        CancellationToken cancellationToken = default)
    {
        var sessionResult = await _sessionService.GetByIdAsync(sessionId, cancellationToken);
        if (sessionResult.IsFailure)
        {
            return Result<IReadOnlyList<string>>.Failure(sessionResult.Error!);
        }

        var session = sessionResult.Value;
        var followUps = new List<string>();

        // Generate based on response content and conversation history
        var keywords = ExtractKeywordsFromText(lastResponse);
        var entities = ExtractEntitiesFromText(lastResponse, 0);

        // Template-based follow-up generation
        if (entities.Any(e => e.Type == SessionEntityType.Person))
        {
            var person = entities.First(e => e.Type == SessionEntityType.Person);
            followUps.Add($"What else can you tell me about {person.Name}?");
        }

        if (entities.Any(e => e.Type == SessionEntityType.Concept))
        {
            var concept = entities.First(e => e.Type == SessionEntityType.Concept);
            followUps.Add($"Can you explain {concept.Name} in more detail?");
        }

        if (keywords.Any())
        {
            followUps.Add($"How does this relate to {keywords.First()}?");
        }

        // Add generic follow-ups if needed
        if (followUps.Count < count)
        {
            followUps.Add("Can you provide more examples?");
        }
        if (followUps.Count < count)
        {
            followUps.Add("What are the key takeaways?");
        }
        if (followUps.Count < count)
        {
            followUps.Add("Are there any related topics I should explore?");
        }

        return Result<IReadOnlyList<string>>.Success(followUps.Take(count).ToList());
    }

    #region Private Helpers

    private static int EstimateTokens(string text)
    {
        return (int)Math.Ceiling((double)text.Length / CharsPerToken);
    }

    private static string TruncateToTokens(string text, int maxTokens)
    {
        var maxChars = maxTokens * CharsPerToken;
        if (text.Length <= maxChars) return text;
        return text[..maxChars] + "...";
    }

    private static List<SessionTurn> GetTurnsForContext(
        List<SessionTurn> turns,
        BuildContextOptions options)
    {
        return options.Strategy switch
        {
            ContextStrategy.RecentFirst => turns.TakeLast(options.MaxTurns).ToList(),
            ContextStrategy.RelevantFirst => turns.TakeLast(options.MaxTurns).ToList(), // Simplified
            ContextStrategy.Summarized => turns.TakeLast(options.MaxTurns / 2).ToList(),
            ContextStrategy.Hybrid => turns.TakeLast(options.MaxTurns).ToList(),
            _ => turns.TakeLast(options.MaxTurns).ToList()
        };
    }

    private static string GetDefaultSystemPrompt(SessionType sessionType)
    {
        return sessionType switch
        {
            SessionType.Research => "You are a research assistant helping with in-depth investigation.",
            SessionType.Analysis => "You are an analytical assistant helping to examine and interpret information.",
            SessionType.Comparison => "You are helping compare and contrast different topics or documents.",
            SessionType.Summary => "You are helping summarize and distill key information.",
            _ => "You are a helpful assistant answering questions based on available documents."
        };
    }

    private static List<SessionTrackedEntity> ExtractEntitiesFromTurns(List<SessionTurn> turns)
    {
        var entities = new Dictionary<string, SessionTrackedEntity>(StringComparer.OrdinalIgnoreCase);

        foreach (var turn in turns)
        {
            var turnEntities = ExtractEntitiesFromText(turn.UserQuery, turn.TurnNumber);
            if (!string.IsNullOrEmpty(turn.SystemResponse))
            {
                turnEntities.AddRange(ExtractEntitiesFromText(turn.SystemResponse, turn.TurnNumber));
            }

            foreach (var entity in turnEntities)
            {
                if (entities.TryGetValue(entity.Name, out var existing))
                {
                    entities[entity.Name] = existing with
                    {
                        MentionCount = existing.MentionCount + 1,
                        LastMentionTurn = turn.TurnNumber
                    };
                }
                else
                {
                    entities[entity.Name] = entity;
                }
            }
        }

        return entities.Values.OrderByDescending(e => e.Salience).ToList();
    }

    private static List<SessionTrackedEntity> ExtractEntitiesFromText(string text, int turnNumber)
    {
        var entities = new List<SessionTrackedEntity>();

        // Simple pattern-based entity extraction
        // In a real implementation, use NER service

        // Capitalized words (potential proper nouns)
        var properNouns = Regex.Matches(text, @"\b[A-Z][a-z]+(?:\s+[A-Z][a-z]+)*\b");
        foreach (Match match in properNouns)
        {
            if (match.Value.Length > 2 && !IsCommonWord(match.Value))
            {
                entities.Add(new SessionTrackedEntity
                {
                    Name = match.Value,
                    Type = GuessSessionEntityType(match.Value),
                    MentionCount = 1,
                    FirstMentionTurn = turnNumber,
                    LastMentionTurn = turnNumber,
                    Salience = 0.5
                });
            }
        }

        return entities;
    }

    private static bool IsCommonWord(string word)
    {
        var common = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "The", "This", "That", "These", "Those", "What", "When", "Where",
            "Why", "How", "Can", "Could", "Would", "Should", "Please", "Thank"
        };
        return common.Contains(word);
    }

    private static SessionEntityType GuessSessionEntityType(string name)
    {
        // Simple heuristics
        if (name.EndsWith("Corp") || name.EndsWith("Inc") || name.EndsWith("Ltd"))
            return SessionEntityType.Organization;
        if (name.Contains("Street") || name.Contains("City") || name.Contains("Country"))
            return SessionEntityType.Location;
        return SessionEntityType.Concept;
    }

    private static Dictionary<string, List<string>> BuildCoreferences(List<SessionTrackedEntity> entities)
    {
        var coreferences = new Dictionary<string, List<string>>();
        foreach (var entity in entities)
        {
            if (entity.Aliases.Any())
            {
                coreferences[entity.Name] = entity.Aliases;
            }
        }
        return coreferences;
    }

    private static List<string> ExtractKeywords(List<SessionTurn> turns)
    {
        var allText = string.Join(" ",
            turns.SelectMany(t => new[] { t.UserQuery, t.SystemResponse ?? "" }));
        return ExtractKeywordsFromText(allText);
    }

    private static List<string> ExtractKeywordsFromText(string text)
    {
        var words = Regex.Split(text.ToLowerInvariant(), @"\W+")
            .Where(w => w.Length > 3)
            .Where(w => !StopWords.Contains(w))
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(20)
            .ToList();
        return words;
    }

    private static readonly HashSet<string> StopWords = new()
    {
        "the", "and", "for", "that", "this", "with", "from", "have", "been",
        "what", "when", "where", "which", "who", "will", "would", "could",
        "should", "about", "into", "more", "some", "than", "them", "then",
        "there", "these", "they", "through", "very", "your"
    };

    private static string DetermineTopicName(List<string> keywords, List<SessionTurn> turns)
    {
        if (!keywords.Any()) return "General Discussion";
        return string.Join(" ", keywords.Take(3)).ToUpperInvariant();
    }

    private static string DetermineTopicNameFromKeywords(List<string> keywords)
    {
        if (!keywords.Any()) return "New Topic";
        return string.Join(" ", keywords.Take(2));
    }

    private static int FindTopicStartTurn(List<SessionTurn> turns, string topicName)
    {
        // Simplified - return first turn mentioning keywords
        var keywords = topicName.ToLowerInvariant().Split(' ');
        for (int i = 0; i < turns.Count; i++)
        {
            if (keywords.Any(k => turns[i].UserQuery.ToLowerInvariant().Contains(k)))
            {
                return i + 1;
            }
        }
        return 1;
    }

    private static bool ContainsTopic(string query, ConversationTopic topic)
    {
        var queryLower = query.ToLowerInvariant();
        return topic.Keywords.Any(k => queryLower.Contains(k.ToLowerInvariant()));
    }

    private string ResolvePronounReferences(
        string query,
        ConversationContext context,
        List<string> resolved)
    {
        var result = query;

        // Get the most recent entities mentioned
        var recentEntities = context.Entities
            .OrderByDescending(e => e.LastMentionTurn)
            .ToList();

        // Replace pronouns with entity names
        var pronounPatterns = new Dictionary<string, string[]>
        {
            { @"\bit\b", new[] { "concept", "thing", "topic" } },
            { @"\bthey\b", new[] { "organization", "people", "group" } },
            { @"\bhe\b", new[] { "person" } },
            { @"\bshe\b", new[] { "person" } },
            { @"\bthem\b", new[] { "organization", "people" } }
        };

        foreach (var (pattern, types) in pronounPatterns)
        {
            if (Regex.IsMatch(result, pattern, RegexOptions.IgnoreCase))
            {
                var entity = recentEntities.FirstOrDefault();
                if (entity != null)
                {
                    result = Regex.Replace(result, pattern, entity.Name, RegexOptions.IgnoreCase);
                    resolved.Add($"{pattern} -> {entity.Name}");
                }
            }
        }

        return result;
    }

    private static string ExpandAbbreviatedReferences(
        string query,
        ConversationContext context,
        List<string> expanded)
    {
        // Handle references like "the same", "the previous", etc.
        var result = query;

        if (result.Contains("the same", StringComparison.OrdinalIgnoreCase))
        {
            var lastUserMessage = context.Messages
                .Where(m => m.Role == ContextRole.User)
                .OrderByDescending(m => m.TurnNumber)
                .Skip(1)
                .FirstOrDefault();

            if (lastUserMessage != null)
            {
                // Note: In a real implementation, extract the subject from the previous query
                expanded.Add("'the same' references previous query subject");
            }
        }

        return result;
    }

    private static double CalculateRewriteConfidence(
        string original,
        string rewritten,
        List<string> resolutions)
    {
        if (original == rewritten) return 1.0;
        return Math.Max(0.5, 1.0 - (0.1 * resolutions.Count));
    }

    private static List<string> ExtractKeyPoints(List<SessionTurn> turns)
    {
        var keyPoints = new List<string>();

        foreach (var turn in turns)
        {
            if (!string.IsNullOrEmpty(turn.SystemResponse))
            {
                // Extract first sentence as a key point
                var firstSentence = Regex.Match(turn.SystemResponse, @"^[^.!?]+[.!?]");
                if (firstSentence.Success && firstSentence.Value.Length > 20)
                {
                    keyPoints.Add(firstSentence.Value.Trim());
                }
            }
        }

        return keyPoints.Take(5).ToList();
    }

    private static string BuildConciseSummary(Session session)
    {
        var turnCount = session.Turns.Count;
        var topics = ExtractKeywords(session.Turns).Take(3);
        return $"Conversation with {turnCount} exchanges about {string.Join(", ", topics)}.";
    }

    private static string BuildBulletSummary(Session session, List<string> keyPoints)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Session: {session.Title}");
        sb.AppendLine($"Turns: {session.Turns.Count}");
        sb.AppendLine("Key Points:");
        foreach (var point in keyPoints)
        {
            sb.AppendLine($"  - {point}");
        }
        return sb.ToString();
    }

    private static string BuildDetailedSummary(Session session)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Session '{session.Title}' ({session.Type})");
        sb.AppendLine($"Started: {session.CreatedAt:g}, Duration: {session.Duration?.TotalMinutes:F0} minutes");
        sb.AppendLine($"Total exchanges: {session.Turns.Count}");
        sb.AppendLine();
        sb.AppendLine("Questions asked:");
        foreach (var turn in session.Turns.Take(5))
        {
            sb.AppendLine($"  - {turn.UserQuery}");
        }
        if (session.Turns.Count > 5)
        {
            sb.AppendLine($"  ... and {session.Turns.Count - 5} more");
        }
        return sb.ToString();
    }

    private static string BuildNarrativeSummary(Session session)
    {
        if (!session.Turns.Any())
        {
            return $"A new session titled '{session.Title}' with no queries yet.";
        }

        var firstQuery = session.Turns.First().UserQuery;
        return $"The user started by asking about '{firstQuery}'. " +
               $"Over {session.Turns.Count} exchanges, the conversation explored various aspects of the topic.";
    }

    private static List<string> FindUnresolvedQuestions(List<SessionTurn> turns)
    {
        return turns
            .Where(t => t.Status == TurnStatus.Failed ||
                       (t.Feedback?.Rating == FeedbackRating.Negative))
            .Select(t => t.UserQuery)
            .ToList();
    }

    private static bool IsFollowUpQuery(string query, ConversationContext context)
    {
        var indicators = new[] { "what about", "how about", "and", "also", "more", "else", "another" };
        var queryLower = query.ToLowerInvariant();
        return indicators.Any(i => queryLower.StartsWith(i)) || ContainsPronouns(query);
    }

    private static bool ContainsPronouns(string query)
    {
        var pronouns = new[] { " it ", " they ", " them ", " he ", " she ", " this ", " that " };
        var queryWithSpaces = $" {query.ToLowerInvariant()} ";
        return pronouns.Any(p => queryWithSpaces.Contains(p));
    }

    private static bool ContainsReferences(string query)
    {
        var references = new[] { "the same", "the previous", "mentioned", "earlier", "above" };
        return references.Any(r => query.Contains(r, StringComparison.OrdinalIgnoreCase));
    }

    private static SessionIntentType ClassifySessionQueryIntent(string queryLower)
    {
        if (queryLower.StartsWith("what is") || queryLower.StartsWith("define"))
            return SessionIntentType.Definition;
        if (queryLower.StartsWith("how") || queryLower.Contains("explain"))
            return SessionIntentType.Explanation;
        if (queryLower.StartsWith("compare") || queryLower.Contains("difference"))
            return SessionIntentType.Comparison;
        if (queryLower.StartsWith("summarize") || queryLower.StartsWith("summary"))
            return SessionIntentType.Summary;
        if (queryLower.StartsWith("list") || queryLower.Contains("what are the"))
            return SessionIntentType.List;
        if (queryLower.Contains("recommend") || queryLower.Contains("suggest"))
            return SessionIntentType.Recommendation;
        if (queryLower.Contains("clarify") || queryLower.Contains("mean"))
            return SessionIntentType.Clarification;
        if (queryLower.Contains("verify") || queryLower.Contains("true") || queryLower.Contains("correct"))
            return SessionIntentType.Verification;
        return SessionIntentType.Question;
    }

    private static Dictionary<string, string> ExtractIntentParameters(string query, SessionIntentType intent)
    {
        var parameters = new Dictionary<string, string>();

        // Extract subject based on intent type
        switch (intent)
        {
            case SessionIntentType.Definition:
                var defMatch = Regex.Match(query, @"(?:what is|define)\s+(.+)", RegexOptions.IgnoreCase);
                if (defMatch.Success)
                    parameters["subject"] = defMatch.Groups[1].Value.Trim('?');
                break;
            case SessionIntentType.Comparison:
                var compMatch = Regex.Match(query, @"(?:compare|difference between)\s+(.+?)\s+(?:and|vs)\s+(.+)", RegexOptions.IgnoreCase);
                if (compMatch.Success)
                {
                    parameters["subject1"] = compMatch.Groups[1].Value;
                    parameters["subject2"] = compMatch.Groups[2].Value.Trim('?');
                }
                break;
        }

        return parameters;
    }

    #endregion
}
