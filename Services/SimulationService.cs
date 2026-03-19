using backend.Models.Simulations;
using backend.Services.Simulations;

namespace backend.Services;

/// <summary>
/// Coordinates algorithm simulation execution.
/// </summary>
public class SimulationService : ISimulationService
{
    private static readonly HashSet<string> InteractiveActionLabels =
    [
        "swap",
        "midpoint_pick",
        "pick_midpoint",
        "midpoint"
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
    public Task<SimulationResponse> RunAsync(string algorithm, int[] array)
    {
        var engine = ResolveEngine(algorithm);
        var clonedInput = array.ToArray();
        return Task.FromResult(engine.Run(clonedInput));
    }

    /// <inheritdoc />
    public Task<SimulationSession> StartSessionAsync(string algorithm, int[] array)
    {
        var simulation = ResolveEngine(algorithm).Run(array.ToArray());
        var session = new SimulationSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            Steps = simulation.Steps,
            CurrentStepIndex = FindNextActionableStepIndex(simulation.Steps, 0)
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
            var normalizedAction = NormalizeActionLabel(actionType);
            var currentArrayState = GetCurrentArrayState(session, expectedIndex);

            if (TerminalActionLabels.Contains(expectedActionLabel))
            {
                var terminalAction = NormalizeActionLabel(expectedActionLabel);
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

            var expectedAction = NormalizeActionLabel(expectedActionLabel);
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
            var nextExpectedAction = NormalizeActionLabel(nextActionLabel);
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
        var normalizedAlgorithm = algorithm.Trim().ToLowerInvariant();
        var engine = _engines.FirstOrDefault(e => e.CanHandle(normalizedAlgorithm));
        if (engine is null)
        {
            _logger.LogWarning("Simulation requested for unsupported algorithm {Algorithm}", algorithm);
            throw new NotSupportedException($"Algorithm '{algorithm}' is not supported.");
        }

        return engine;
    }

    private static int FindNextActionableStepIndex(IReadOnlyList<SimulationStep> steps, int startIndex)
    {
        for (var index = Math.Max(startIndex, 0); index < steps.Count; index++)
        {
            var actionLabel = steps[index].ActionLabel.Trim().ToLowerInvariant();
            if (InteractiveActionLabels.Contains(actionLabel) || TerminalActionLabels.Contains(actionLabel))
            {
                return index;
            }
        }

        return Math.Max(steps.Count - 1, 0);
    }

    private static string NormalizeActionLabel(string actionLabel)
    {
        return actionLabel.Trim().ToLowerInvariant() switch
        {
            "swap" => "swap",
            "pick_midpoint" => "midpoint_pick",
            "midpoint" => "midpoint_pick",
            "midpoint_pick" => "midpoint_pick",
            "found" => "target_found",
            "target_found" => "target_found",
            "not_found" => "target_not_found",
            "target_not_found" => "target_not_found",
            "complete" => "complete",
            "early_exit" => "complete",
            _ => actionLabel.Trim().ToLowerInvariant()
        };
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
        if (nextExpectedAction == "swap" && indices.Length >= 2)
        {
            return $"Try swapping index {indices[0]} and {indices[1]}.";
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
            Steps = session.Steps.Select(step => new SimulationStep
            {
                StepNumber = step.StepNumber,
                ArrayState = step.ArrayState.ToArray(),
                ActiveIndices = step.ActiveIndices.ToArray(),
                LineNumber = step.LineNumber,
                ActionLabel = step.ActionLabel,
                Search = step.Search is null
                    ? null
                    : new SearchStepModel
                    {
                        LowIndex = step.Search.LowIndex,
                        HighIndex = step.Search.HighIndex,
                        MidpointIndex = step.Search.MidpointIndex,
                        State = step.Search.State,
                        DiscardedSide = step.Search.DiscardedSide,
                        DiscardStartIndex = step.Search.DiscardStartIndex,
                        DiscardEndIndex = step.Search.DiscardEndIndex,
                        DiscardedIndices = step.Search.DiscardedIndices.ToArray()
                    }
            }).ToList()
        };
    }
}
