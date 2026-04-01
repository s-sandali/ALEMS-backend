using backend.DTOs;
using backend.Models;
using backend.Repositories;

namespace backend.Services;

/// <summary>
/// Handles validation and persistence for quiz attempts.
/// </summary>
public class QuizAttemptService : IQuizAttemptService
{
    private readonly IQuizRepository _quizRepository;
    private readonly IQuizQuestionRepository _questionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IQuizAttemptRepository _attemptRepository;
    private readonly ILogger<QuizAttemptService> _logger;

    public QuizAttemptService(
        IQuizRepository quizRepository,
        IQuizQuestionRepository questionRepository,
        IUserRepository userRepository,
        IQuizAttemptRepository attemptRepository,
        ILogger<QuizAttemptService> logger)
    {
        _quizRepository = quizRepository;
        _questionRepository = questionRepository;
        _userRepository = userRepository;
        _attemptRepository = attemptRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<QuizAttemptResultDto> SubmitAttemptAsync(int quizId, string clerkUserId, CreateQuizAttemptDto dto)
    {
        var quiz = await _quizRepository.GetActiveByIdAsync(quizId);
        if (quiz is null)
        {
            throw new KeyNotFoundException($"Quiz with ID {quizId} does not exist.");
        }

        var user = await _userRepository.GetByClerkUserIdAsync(clerkUserId);
        if (user is null)
        {
            throw new KeyNotFoundException(
                "Authenticated user does not have a local account. Complete user sync first.");
        }

        var questions = (await _questionRepository.GetByQuizIdAsync(quizId)).ToList();
        if (questions.Count == 0)
        {
            throw new ArgumentException("This quiz does not contain any active questions.");
        }

        var submittedAnswers = dto.Answers ?? [];
        if (submittedAnswers.Count != questions.Count)
        {
            throw new ArgumentException(
                $"Exactly {questions.Count} answers are required for quiz {quizId}.");
        }

        var duplicateQuestionIds = submittedAnswers
            .GroupBy(a => a.QuestionId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateQuestionIds.Count > 0)
        {
            throw new ArgumentException("Each question may only be answered once per attempt.");
        }

        var quizQuestionIds = questions
            .Select(q => q.QuestionId)
            .ToHashSet();

        var invalidQuestionIds = submittedAnswers
            .Where(a => !quizQuestionIds.Contains(a.QuestionId))
            .Select(a => a.QuestionId)
            .Distinct()
            .ToList();

        if (invalidQuestionIds.Count > 0)
        {
            throw new ArgumentException(
                $"One or more submitted questions do not belong to quiz {quizId}.");
        }

        var gradingResult = await GradeAttemptAsync(questions, submittedAnswers);

        var now = DateTime.UtcNow;
        var attempt = await _attemptRepository.CreateAttemptAsync(new QuizAttempt
        {
            UserId = user.UserId,
            QuizId = quizId,
            Score = gradingResult.Score,
            TotalQuestions = gradingResult.TotalQuestions,
            XpEarned = gradingResult.XpEarned,
            Passed = questions.Count > 0 &&
                     (gradingResult.Score * 100.0 / questions.Count) >= quiz.PassScore,
            StartedAt = now,
            CompletedAt = now
        });

        var detailedResultsByQuestionId = gradingResult.DetailedResults
            .ToDictionary(result => result.QuestionId, result => result.IsCorrect);

        var answers = submittedAnswers
            .Select(answer => new AttemptAnswer
            {
                AttemptId = attempt.AttemptId,
                QuestionId = answer.QuestionId,
                SelectedOption = answer.SelectedOption,
                IsCorrect = detailedResultsByQuestionId[answer.QuestionId]
            });

        await _attemptRepository.CreateAnswersAsync(answers);

        _logger.LogInformation(
            "SubmitAttempt: AttemptId={AttemptId} recorded for QuizId={QuizId}, UserId={UserId}",
            attempt.AttemptId,
            quizId,
            user.UserId);

        return gradingResult;
    }

    /// <summary>
    /// Computes correctness, raw score, and total XP for a submitted attempt.
    /// </summary>
    private static Task<QuizAttemptResultDto> GradeAttemptAsync(
        IReadOnlyCollection<QuizQuestion> questions,
        IReadOnlyCollection<QuizAttemptAnswerSubmissionDto> submittedAnswers)
    {
        var answersByQuestionId = submittedAnswers.ToDictionary(
            answer => answer.QuestionId,
            answer => answer.SelectedOption,
            comparer: EqualityComparer<int>.Default);

        var gradedQuestions = questions
            .Select(question =>
            {
                var selectedOption = answersByQuestionId[question.QuestionId];
                var isCorrect = string.Equals(
                    selectedOption,
                    question.CorrectOption,
                    StringComparison.OrdinalIgnoreCase);

                return new
                {
                    QuestionId = question.QuestionId,
                    IsCorrect = isCorrect,
                    question.XpReward
                };
            })
            .ToList();

        var detailedResults = gradedQuestions
            .Select(result => new QuizAttemptDetailedResultDto
            {
                QuestionId = result.QuestionId,
                IsCorrect = result.IsCorrect
            })
            .ToList();

        var score = gradedQuestions.Count(result => result.IsCorrect);
        var xpEarned = gradedQuestions
            .Where(result => result.IsCorrect)
            .Sum(result => result.XpReward);

        return Task.FromResult(new QuizAttemptResultDto
        {
            Score = score,
            TotalQuestions = questions.Count,
            XpEarned = xpEarned,
            DetailedResults = detailedResults
        });
    }
}
