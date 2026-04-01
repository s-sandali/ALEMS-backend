using backend.Models.Simulations;
using backend.Services.Simulations;

namespace backend.Services;

/// <summary>
/// Coordinates algorithm simulation execution.
/// </summary>
public class SimulationService : ISimulationService
{
    private static readonly HashSet<string> SupportedAlgorithms =
    [
        "bubble_sort",
        "bubble-sort",
        "binary_search",
        "binary-search",
        "quick_sort",
        "quick-sort"
    ];

    private static readonly HashSet<string> TerminalActionLabels =
    [
        "complete",
        "early_exit",
        "target_found",
        "found",
        "target_not_found",
        "not_found"
    ];

    private static readonly HashSet<string> DecisionActionLabels =
    [
        "discard_left",
        "discard_right",
        "target_found",
        "found"
    ];

    private enum InteractionProfile
    {
        Default,
        BinarySearch,
        QuickSort
    }

    private readonly IEnumerable<IAlgorithmSimulationEngine> _engines;
    private readonly ISimulationSessionStore _sessionStore;
    private readonly ILogger<SimulationService> _logger;

    public SimulationService(
        IEnumerable<IAlgorithmSimulationEngine> engines,
        ISimulationSessionStore sessionStore,
        ILogger<SimulationService> logger)
    {
        _engines = engines;
        _sessionStore = sessionStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<SimulationResponse> RunAsync(string algorithm, int[] array, int? targetValue)
    {
        var engine = ResolveEngine(algorithm);
        var clonedInput = array.ToArray();
        return Task.FromResult(engine.Run(clonedInput, targetValue));
    }

    /// <inheritdoc />
    public Task<SimulationSession> StartSessionAsync(string algorithm, int[] array, int? targetValue)
    {
        var simulation = ResolveEngine(algorithm).Run(array.ToArray(), targetValue);
        var session = new SimulationSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            Steps = simulation.Steps,
            CurrentStepIndex = FindNextActionableStepIndex(simulation.Steps, 0),
            TargetValue = simulation.TargetValue
        };

        _sessionStore.Save(session);
        return Task.FromResult(CloneSession(session));
    }

    /// <inheritdoc />
    public Task<SimulationValidationResponse> ValidateStepAsync(string sessionId, string actionType, int[] indices)
    {
        var session = _sessionStore.Get(sessionId);

        if (session is null)
        {
            throw new KeyNotFoundException($"Simulation session '{sessionId}' was not found.");
        }

        lock (session)
        {
            var expectedIndex = NormalizeSessionIndex(session);
            var expectedStep = session.Steps[expectedIndex];
            var expectedActionLabel = expectedStep.ActionLabel.Trim().ToLowerInvariant();
            var normalizedAction = NormalizeActionLabel(actionType, expectedStep);
            var currentArrayState = GetCurrentArrayState(session, expectedIndex);

            if (TerminalActionLabels.Contains(expectedActionLabel))
            {
                var terminalAction = NormalizeActionLabel(expectedActionLabel, expectedStep);
                return Task.FromResult(new SimulationValidationResponse
                {
                    SessionId = session.SessionId,
                    Correct = false,
                    NewArrayState = currentArrayState,
                    NextState = currentArrayState,
                    NextExpectedAction = terminalAction,
                    Message = "Practice complete.",
                    Hint = "No more actions are needed.",
                    SuggestedIndices = [],
                    CurrentStepIndex = session.CurrentStepIndex
                });
            }

            if (IsDecisionAction(normalizedAction))
            {
                var decisionStepIndex = FindExpectedDecisionStepIndex(session.Steps, expectedIndex);
                if (decisionStepIndex is null)
                {
                    return Task.FromResult(new SimulationValidationResponse
                    {
                        SessionId = session.SessionId,
                        Correct = false,
                        NewArrayState = currentArrayState,
                        NextState = currentArrayState,
                        NextExpectedAction = normalizedAction,
                        Message = "No decision is expected at this step.",
                        Hint = "Wait for the midpoint before choosing a direction.",
                        SuggestedIndices = [],
                        CurrentStepIndex = session.CurrentStepIndex
                    });
                }

                var decisionStep = session.Steps[decisionStepIndex.Value];
                var expectedDecision = NormalizeActionLabel(decisionStep.ActionLabel, decisionStep);
                var isCorrectDecision = normalizedAction == expectedDecision;

                if (!isCorrectDecision)
                {
                    return Task.FromResult(new SimulationValidationResponse
                    {
                        SessionId = session.SessionId,
                        Correct = false,
                        NewArrayState = currentArrayState,
                        NextState = currentArrayState,
                        NextExpectedAction = expectedDecision,
                        Message = "Incorrect decision.",
                        Hint = BuildHint(expectedDecision, decisionStep.ActiveIndices.ToArray()),
                        SuggestedIndices = decisionStep.ActiveIndices.ToArray(),
                        CurrentStepIndex = session.CurrentStepIndex
                    });
                }

                var decisionArrayState = decisionStep.ArrayState.ToArray();
                session.CurrentStepIndex = FindNextActionableStepIndex(session.Steps, decisionStepIndex.Value + 1);
                _sessionStore.Save(session);

                var decisionNextStep = session.Steps[session.CurrentStepIndex];
                var decisionNextActionLabel = decisionNextStep.ActionLabel.Trim().ToLowerInvariant();
                var decisionNextExpectedAction = NormalizeActionLabel(decisionNextActionLabel, decisionNextStep);
                var decisionNextSuggestedIndices = TerminalActionLabels.Contains(decisionNextActionLabel)
                    ? []
                    : decisionNextStep.ActiveIndices.ToArray();

                return Task.FromResult(new SimulationValidationResponse
                {
                    SessionId = session.SessionId,
                    Correct = true,
                    NewArrayState = decisionArrayState,
                    NextState = decisionArrayState,
                    NextExpectedAction = decisionNextExpectedAction,
                    Message = normalizedAction == "target_found"
                        ? "Target found."
                        : "Correct decision.",
                    Hint = decisionNextExpectedAction == "complete"
                        ? "No more actions are needed."
                        : BuildHint(decisionNextExpectedAction, decisionNextSuggestedIndices),
                    SuggestedIndices = decisionNextSuggestedIndices,
                    CurrentStepIndex = session.CurrentStepIndex
                });
            }

            var expectedAction = NormalizeActionLabel(expectedActionLabel, expectedStep);
            var expectedIndices = expectedStep.ActiveIndices.ToArray();
            var isCorrectAction =
                normalizedAction == expectedAction &&
                indices.Length == expectedIndices.Length &&
                indices.SequenceEqual(expectedIndices);

            if (!isCorrectAction)
            {
                return Task.FromResult(new SimulationValidationResponse
                {
                    SessionId = session.SessionId,
                    Correct = false,
                    NewArrayState = currentArrayState,
                    NextState = currentArrayState,
                    NextExpectedAction = expectedAction,
                    Message = "Incorrect step.",
                    Hint = BuildHint(expectedAction, expectedIndices),
                    SuggestedIndices = expectedIndices,
                    CurrentStepIndex = session.CurrentStepIndex
                });
            }

            var executedArrayState = expectedStep.ArrayState.ToArray();
            session.CurrentStepIndex = FindNextActionableStepIndex(session.Steps, expectedIndex + 1);
            _sessionStore.Save(session);

            var nextStep = session.Steps[session.CurrentStepIndex];
            var nextActionLabel = nextStep.ActionLabel.Trim().ToLowerInvariant();
            var nextExpectedAction = NormalizeActionLabel(nextActionLabel, nextStep);
            var nextSuggestedIndices = TerminalActionLabels.Contains(nextActionLabel)
                ? []
                : nextStep.ActiveIndices.ToArray();

            return Task.FromResult(new SimulationValidationResponse
            {
                SessionId = session.SessionId,
                Correct = true,
                NewArrayState = executedArrayState,
                NextState = executedArrayState,
                NextExpectedAction = nextExpectedAction,
                Message = nextExpectedAction == "complete"
                    ? "Correct step. Practice complete."
                    : $"Correct {expectedAction}.",
                Hint = nextExpectedAction == "complete"
                    ? "No more actions are needed."
                    : BuildHint(nextExpectedAction, nextSuggestedIndices),
                SuggestedIndices = nextSuggestedIndices,
                CurrentStepIndex = session.CurrentStepIndex
            });
        }
    }

    private IAlgorithmSimulationEngine ResolveEngine(string algorithm)
    {
        if (string.IsNullOrWhiteSpace(algorithm))
        {
            _logger.LogWarning("Simulation requested with empty algorithm name");
            throw new NotSupportedException("Algorithm is required.");
        }

        var normalizedAlgorithm = algorithm.Trim().ToLowerInvariant();

        if (!SupportedAlgorithms.Contains(normalizedAlgorithm))
        {
            _logger.LogWarning("Simulation requested for unsupported algorithm {Algorithm}", algorithm);
            throw new NotSupportedException($"Algorithm '{algorithm}' is not supported.");
        }

        var engine = _engines.FirstOrDefault(e => e.CanHandle(normalizedAlgorithm));
        if (engine is null)
        {
            _logger.LogWarning("No engine registered for algorithm {Algorithm}", algorithm);
            throw new NotSupportedException($"Algorithm '{algorithm}' is not supported.");
        }

        return engine;
    }

    private static int FindNextActionableStepIndex(IReadOnlyList<SimulationStep> steps, int startIndex)
    {
        var interactionProfile = DetermineInteractionProfile(steps);

        for (var index = Math.Max(startIndex, 0); index < steps.Count; index++)
        {
            if (IsInteractiveStep(steps[index], interactionProfile))
            {
                return index;
            }
        }

        return Math.Max(steps.Count - 1, 0);
    }

    private static bool IsInteractiveStep(SimulationStep step, InteractionProfile interactionProfile)
    {
        var actionLabel = step.ActionLabel.Trim().ToLowerInvariant();

        if (TerminalActionLabels.Contains(actionLabel))
        {
            return true;
        }

        return interactionProfile switch
        {
            InteractionProfile.QuickSort => actionLabel is "compare" or "swap" or "pivot_swap",
            InteractionProfile.BinarySearch => actionLabel is "midpoint_pick" or "pick_midpoint" or "midpoint",
            _ => actionLabel == "swap"
        };
    }

    private static InteractionProfile DetermineInteractionProfile(IReadOnlyList<SimulationStep> steps)
    {
        if (steps.Any(IsQuickSortStep))
        {
            return InteractionProfile.QuickSort;
        }

        if (steps.Any(IsBinarySearchStep))
        {
            return InteractionProfile.BinarySearch;
        }

        return InteractionProfile.Default;
    }

    private static string NormalizeActionLabel(string actionLabel, SimulationStep? stepContext = null)
    {
        var normalizedAction = actionLabel.Trim().ToLowerInvariant();

        return normalizedAction switch
        {
            "compare" => "compare",
            "swap" => "swap",
            "pivot_swap" when IsQuickSortStep(stepContext) => "swap",
            "pick_midpoint" => "midpoint_pick",
            "midpoint" => "midpoint_pick",
            "midpoint_pick" => "midpoint_pick",
            "left" => "discard_left",
            "go_left" => "discard_left",
            "discard_left" => "discard_left",
            "right" => "discard_right",
            "go_right" => "discard_right",
            "discard_right" => "discard_right",
            "found" => "target_found",
            "target_found" => "target_found",
            "not_found" => "target_not_found",
            "target_not_found" => "target_not_found",
            "complete" => "complete",
            "early_exit" => "complete",
            _ => normalizedAction
        };
    }

    private static bool IsDecisionAction(string actionLabel)
    {
        return DecisionActionLabels.Contains(actionLabel);
    }

    private static int? FindExpectedDecisionStepIndex(IReadOnlyList<SimulationStep> steps, int startIndex)
    {
        for (var index = Math.Max(startIndex, 0); index < steps.Count; index++)
        {
            var normalized = NormalizeActionLabel(steps[index].ActionLabel, steps[index]);
            if (DecisionActionLabels.Contains(normalized))
            {
                return index;
            }
        }

        return null;
    }

    private static int[] GetCurrentArrayState(SimulationSession session, int expectedIndex)
    {
        if (session.Steps.Count == 0)
        {
            return [];
        }

        if (expectedIndex <= 0)
        {
            return session.Steps[0].ArrayState.ToArray();
        }

        return session.Steps[expectedIndex - 1].ArrayState.ToArray();
    }

    private static int NormalizeSessionIndex(SimulationSession session)
    {
        if (session.Steps.Count == 0)
        {
            throw new InvalidOperationException("Simulation session does not contain any steps.");
        }

        session.CurrentStepIndex = Math.Min(
            FindNextActionableStepIndex(session.Steps, session.CurrentStepIndex),
            session.Steps.Count - 1);

        return session.CurrentStepIndex;
    }

    private static string BuildHint(string nextExpectedAction, int[] indices)
    {
        if (nextExpectedAction == "discard_left")
        {
            return "Discard the left half and continue with the right side.";
        }

        if (nextExpectedAction == "discard_right")
        {
            return "Discard the right half and continue with the left side.";
        }

        if (nextExpectedAction == "target_found")
        {
            return "The target matches the midpoint.";
        }

        if (nextExpectedAction == "swap" && indices.Length >= 2)
        {
            return $"Try swapping index {indices[0]} and {indices[1]}.";
        }

        if (nextExpectedAction == "compare" && indices.Length >= 2)
        {
            return $"Compare index {indices[0]} against index {indices[1]}.";
        }

        if (nextExpectedAction == "midpoint_pick" && indices.Length >= 1)
        {
            return $"Pick the midpoint at index {indices[0]}.";
        }

        return "No more actions are needed.";
    }

    private static SimulationSession CloneSession(SimulationSession session)
    {
        return new SimulationSession
        {
            SessionId = session.SessionId,
            CurrentStepIndex = session.CurrentStepIndex,
            TargetValue = session.TargetValue,
            Steps = session.Steps.Select(CloneStep).ToList()
        };
    }

    private static bool IsQuickSortStep(SimulationStep? step)
    {
        if (step is null)
        {
            return false;
        }

        if (step.QuickSort is not null || step.Recursion is not null)
        {
            return true;
        }

        var actionLabel = step.ActionLabel.Trim().ToLowerInvariant();
        return actionLabel is "pivot_swap"
            or "pivot_select"
            or "partition_start"
            or "recursive_call"
            or "base_case"
            or "pivot_positioned"
            or "sort_left_start"
            or "sort_left_complete"
            or "sort_right_start"
            or "sort_right_complete";
    }

    private static bool IsBinarySearchStep(SimulationStep step)
    {
        if (step.Search is not null)
        {
            return true;
        }

        var actionLabel = step.ActionLabel.Trim().ToLowerInvariant();
        return actionLabel is "midpoint_pick"
            or "pick_midpoint"
            or "midpoint"
            or "discard_left"
            or "discard_right";
    }

    private static SimulationStep CloneStep(SimulationStep step)
    {
        return new SimulationStep
        {
            StepNumber = step.StepNumber,
            ArrayState = step.ArrayState.ToArray(),
            ActiveIndices = step.ActiveIndices.ToArray(),
            LineNumber = step.LineNumber,
            ActionLabel = step.ActionLabel,
            Recursion = CloneRecursion(step.Recursion),
            Search = CloneSearch(step.Search),
            Heap = CloneHeap(step.Heap),
            QuickSort = CloneQuickSort(step.QuickSort)
        };
    }

    private static RecursionStepModel? CloneRecursion(RecursionStepModel? recursion)
    {
        return recursion is null
            ? null
            : new RecursionStepModel
            {
                State = recursion.State,
                Depth = recursion.Depth,
                CurrentFrameId = recursion.CurrentFrameId,
                Stack = recursion.Stack.Select(frame => new RecursionFrameModel
                {
                    Id = frame.Id,
                    FunctionName = frame.FunctionName,
                    Depth = frame.Depth,
                    State = frame.State,
                    LeftIndex = frame.LeftIndex,
                    RightIndex = frame.RightIndex,
                    ReturnValue = frame.ReturnValue
                }).ToList()
            };
    }

    private static SearchStepModel? CloneSearch(SearchStepModel? search)
    {
        return search is null
            ? null
            : new SearchStepModel
            {
                LowIndex = search.LowIndex,
                HighIndex = search.HighIndex,
                MidpointIndex = search.MidpointIndex,
                State = search.State,
                DiscardedSide = search.DiscardedSide,
                DiscardStartIndex = search.DiscardStartIndex,
                DiscardEndIndex = search.DiscardEndIndex,
                DiscardedIndices = search.DiscardedIndices.ToArray()
            };
    }

    private static HeapStepModel? CloneHeap(HeapStepModel? heap)
    {
        return heap is null
            ? null
            : new HeapStepModel
            {
                Phase = heap.Phase,
                HeapBoundaryEnd = heap.HeapBoundaryEnd,
                HeapIndex = heap.HeapIndex,
                ParentIndex = heap.ParentIndex,
                LeftChildIndex = heap.LeftChildIndex,
                RightChildIndex = heap.RightChildIndex,
                ComparedParentIndex = heap.ComparedParentIndex,
                ComparedChildIndex = heap.ComparedChildIndex,
                ComparedIndices = heap.ComparedIndices.ToArray(),
                ParentChildComparison = heap.ParentChildComparison,
                ExtractedValue = heap.ExtractedValue,
                ExtractedFromIndex = heap.ExtractedFromIndex,
                SortedTargetIndex = heap.SortedTargetIndex
            };
    }

    private static QuickSortStepModel? CloneQuickSort(QuickSortStepModel? quickSort)
    {
        return quickSort is null
            ? null
            : new QuickSortStepModel
            {
                Type = quickSort.Type,
                Pivot = quickSort.Pivot,
                PivotIndex = quickSort.PivotIndex,
                Range = quickSort.Range.ToArray(),
                RecursionDepth = quickSort.RecursionDepth
            };
    }
}
