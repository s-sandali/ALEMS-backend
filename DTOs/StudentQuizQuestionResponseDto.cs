namespace backend.DTOs;

/// <summary>
/// Read model returned to students for quiz questions.
/// <para>
/// <b>Security</b>: <c>correct_option</c> and <c>explanation</c> are deliberately
/// excluded. They must never be sent to the client before the student submits —
/// doing so would allow trivial cheating. They are returned only as part of the
/// quiz attempt result (post-submission).
/// </para>
/// </summary>
public class StudentQuizQuestionResponseDto
{
    public int QuestionId { get; set; }
    public string QuestionType { get; set; } = string.Empty;   // "MCQ" | "PREDICT_STEP"
    public string QuestionText { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;     // "easy" | "medium" | "hard"
    public int OrderIndex { get; set; }
}
