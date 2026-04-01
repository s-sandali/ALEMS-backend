using backend.DTOs;
using backend.Models;
using backend.Repositories;

namespace backend.Services;

/// <summary>
/// Handles business logic for coding question CRUD operations.
/// </summary>
public class CodingQuestionService : ICodingQuestionService
{
    private readonly ICodingQuestionRepository      _repository;
    private readonly ILogger<CodingQuestionService> _logger;

    public CodingQuestionService(ICodingQuestionRepository repository, ILogger<CodingQuestionService> logger)
    {
        _repository = repository;
        _logger     = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CodingQuestionResponseDto>> GetAllAsync()
    {
        var questions = await _repository.GetAllAsync();
        return questions.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<CodingQuestionResponseDto?> GetByIdAsync(int id)
    {
        var question = await _repository.GetByIdAsync(id);
        return question is not null ? MapToDto(question) : null;
    }

    /// <inheritdoc />
    public async Task<CodingQuestionResponseDto> CreateAsync(CreateCodingQuestionDto dto)
    {
        var question = new CodingQuestion
        {
            Title          = dto.Title,
            Description    = dto.Description,
            InputExample   = dto.InputExample,
            ExpectedOutput = dto.ExpectedOutput,
            Difficulty     = dto.Difficulty
        };

        var created = await _repository.CreateAsync(question);
        _logger.LogInformation("CreateCodingQuestion: Id={Id} created", created.Id);

        return MapToDto(created);
    }

    /// <inheritdoc />
    public async Task<CodingQuestionResponseDto> UpdateAsync(int id, UpdateCodingQuestionDto dto)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing is null)
        {
            _logger.LogWarning("UpdateCodingQuestion: not found — Id={Id}", id);
            throw new KeyNotFoundException($"Coding question with ID {id} was not found.");
        }

        existing.Title          = dto.Title;
        existing.Description    = dto.Description;
        existing.InputExample   = dto.InputExample;
        existing.ExpectedOutput = dto.ExpectedOutput;
        existing.Difficulty     = dto.Difficulty;

        await _repository.UpdateAsync(existing);
        _logger.LogInformation("UpdateCodingQuestion: Id={Id} updated", id);

        var updated = await _repository.GetByIdAsync(id);
        return MapToDto(updated!);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id)
    {
        var success = await _repository.DeleteAsync(id);

        if (success)
            _logger.LogInformation("DeleteCodingQuestion: Id={Id} deleted", id);
        else
            _logger.LogWarning("DeleteCodingQuestion: not found — Id={Id}", id);

        return success;
    }

    /// <summary>
    /// Maps a <see cref="CodingQuestion"/> domain model to a <see cref="CodingQuestionResponseDto"/>.
    /// </summary>
    private static CodingQuestionResponseDto MapToDto(CodingQuestion q) => new()
    {
        Id             = q.Id,
        Title          = q.Title,
        Description    = q.Description,
        InputExample   = q.InputExample,
        ExpectedOutput = q.ExpectedOutput,
        Difficulty     = q.Difficulty
    };
}
