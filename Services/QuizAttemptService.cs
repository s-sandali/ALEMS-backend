using backend.DTOs;
using backend.Models;
using backend.Repositories;

namespace backend.Services;

/// <summary>
/// Handles validation, grading, and persistence for quiz attempts.
/// </summary>
public class QuizAttemptService : IQuizAttemptService
{
    private readonly IQuizRepository          _quizRepository;
    private readonly IQuizQuestionRepository  _questionRepository;
    private readonly IUserRepository          _userRepository;
    private readonly IQuizAttemptRepository   _attemptRepository;
    private readonly ILogger<QuizAttemptService> _logger;

    public QuizAttemptService(
        IQuizRepository quizRepository,
        IQuizQuestionRepository questionRepository,
        IUserRepository userRepository,
        IQuizAttemptRepository attemptRepository,
        ILogger<QuizAttemptService> logger)
    {
        _quizRepository     = quizRepository;
        _questionRepository = questionRepository;
        _userRepository     = userRepository;
        _attemptRepository  = attemptRepository;
        _logger             = logger;
    }

    /// <inheritdoc />
    public async Task<QuizAttemptResultDto> SubmitAttemptAsync(int quizId, string clerkUserId, CreateQuizAttemptDto dto)
    {
        var quiz = await _quizRepository.GetActiveByIdAsync(quizId);
        if (quiz is null)
            throw new KeyNotFoundException($"Quiz with ID {quizId} does not exist.");

        var user = await _userRepository.GetByClerkUserIdAsync(clerkUserId);
        if (user is null)
            throw new KeyNotFoundException(
                "Authenticated user does not have a local account. Complete user sync first.");

        var questions = (await _questionRepository.GetByQuizIdAsync(quizId)).ToList();
        if (questions.Count == 0)
            throw new ArgumentException("This quiz does not contain any active questions.");

        var submittedAnswers = dto.Answers ?? [];
        if (submittedAnswers.Count != questions.Count)
            throw new ArgumentException(
                $"Exactly {questions.Count} answers are required for quiz {quizId}.");

        var duplicateQuestionIds = submittedAnswers
            .GroupBy(a => a.QuestionId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateQuestionIds.Count > 0)
            throw new ArgumentException("Each question may only be answered once per attempt.");

        var quizQuestionIds = questions.Select(q => q.QuestionId).ToHashSet();
        var invalidQuestionIds = submittedAnswers
            .Where(a => !quizQuestionIds.Contains(a.QuestionId))
            .Select(a => a.QuestionId)
            .Distinct()
            .ToList();

        if (invalidQuestionIds.Count > 0)
            throw new ArgumentException(
                $"One or more submitted questions do not belong to quiz {quizId}.");

        var gradingResult = GradeAttempt(questions, submittedAnswers);

        var passed = questions.Count > 0 &&
                     (gradingResult.CorrectCount * 100.0 / questions.Count) >= quiz.PassScore;

        // XP is only awarded on the very first attempt — retries earn no additional XP.
        var isFirstAttempt = !await _attemptRepository.HasExistingAttemptAsync(user.UserId, quizId);
        var xpToAward      = isFirstAttempt ? gradingResult.XpEarned : 0;

        var now = DateTime.UtcNow;
        var attempt = await _attemptRepository.SubmitAttemptTransactionalAsync(
            new QuizAttempt
            {
                UserId         = user.UserId,
                QuizId         = quizId,
                Score          = gradingResult.CorrectCount,
                TotalQuestions = gradingResult.TotalQuestions,
                XpEarned       = xpToAward,
                Passed         = passed,
                StartedAt      = now,
                CompletedAt    = now
            },
            gradingResult.Results.Select(a => new AttemptAnswer
            {
                QuestionId     = a.QuestionId,
                SelectedOption = a.SelectedOption,
                IsCorrect      = a.IsCorrect
            }),
            xpToAward);

        gradingResult.AttemptId      = attempt.AttemptId;
        gradingResult.QuizId         = quizId;
        gradingResult.Passed         = passed;
        gradingResult.XpEarned       = xpToAward;
        gradingResult.IsFirstAttempt = isFirstAttempt;

        _logger.LogInformation(
            "SubmitAttempt: AttemptId={AttemptId} QuizId={QuizId} UserId={UserId} Score={Score}% XP={Xp}",
            attempt.AttemptId, quizId, user.UserId, gradingResult.Score, gradingResult.XpEarned);

        return gradingResult;
    }

    /// <summary>
    /// Grades the submitted answers and builds the result DTO.
    /// AttemptId, QuizId, and Passed are populated by the caller after the DB insert.
    /// </summary>
    private static QuizAttemptResultDto GradeAttempt(
        IReadOnlyCollection<QuizQuestion> questions,
        IReadOnlyCollection<QuizAttemptAnswerSubmissionDto> submittedAnswers)
    {
        var answersByQuestionId = submittedAnswers.ToDictionary(
            a => a.QuestionId,
            a => a.SelectedOption,
            EqualityComparer<int>.Default);

        var gradedAnswers = questions
            .Select(question =>
            {
                var selectedOption = answersByQuestionId[question.QuestionId];
                var isCorrect = string.Equals(
                    selectedOption,
                    question.CorrectOption,
                    StringComparison.OrdinalIgnoreCase);

                return new
                {
                    question.QuestionId,
                    SelectedOption = selectedOption,
                    question.CorrectOption,
                    IsCorrect      = isCorrect,
                    question.XpReward,
                    question.Explanation
                };
            })
            .ToList();

        var correctCount   = gradedAnswers.Count(r => r.IsCorrect);
        var totalQuestions = questions.Count;
        var xpEarned       = gradedAnswers.Where(r => r.IsCorrect).Sum(r => r.XpReward);
        var scorePercent   = totalQuestions > 0
            ? (int)Math.Round(correctCount * 100.0 / totalQuestions)
            : 0;

        var answerResults = gradedAnswers
            .Select(r => new QuizAttemptAnswerResultDto
            {
                QuestionId     = r.QuestionId,
                SelectedOption = r.SelectedOption,
                CorrectOption  = r.CorrectOption,
                IsCorrect      = r.IsCorrect,
                Explanation    = r.Explanation
            })
            .ToList();

        return new QuizAttemptResultDto
        {
            Score          = scorePercent,
            CorrectCount   = correctCount,
            TotalQuestions = totalQuestions,
            XpEarned       = xpEarned,
            Results        = answerResults
            // AttemptId, QuizId, Passed set after DB insert
        };
    }
}
