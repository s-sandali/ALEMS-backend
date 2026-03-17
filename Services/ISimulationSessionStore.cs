using backend.Models.Simulations;

namespace backend.Services;

/// <summary>
/// Stores interactive simulation sessions for practice mode.
/// </summary>
public interface ISimulationSessionStore
{
    SimulationSession Save(SimulationSession session);

    SimulationSession? Get(string sessionId);
}
