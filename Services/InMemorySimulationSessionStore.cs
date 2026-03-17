using System.Collections.Concurrent;
using backend.Models.Simulations;

namespace backend.Services;

/// <summary>
/// In-memory session store for practice-mode simulation sessions.
/// </summary>
public class InMemorySimulationSessionStore : ISimulationSessionStore
{
    private readonly ConcurrentDictionary<string, SimulationSession> _sessions = new();

    public SimulationSession Save(SimulationSession session)
    {
        _sessions[session.SessionId] = session;
        return session;
    }

    public SimulationSession? Get(string sessionId)
    {
        return _sessions.GetValueOrDefault(sessionId);
    }
}
