-- =============================================================================
-- V008__seed_algorithm_quizzes_clean.sql
-- Safe version for MySQL 8+
-- =============================================================================

START TRANSACTION;

-- -----------------------------------------------------------------------------
-- HELPER: Get algorithm_id safely
-- -----------------------------------------------------------------------------

-- HEAP SORT
SELECT algorithm_id INTO @algo_heap
FROM algorithms
WHERE LOWER(name) = 'heap sort'
LIMIT 1;

-- -----------------------------------------------------------------------------
-- INSERT QUIZ (Heap Sort)
-- -----------------------------------------------------------------------------

INSERT INTO quizzes (algorithm_id, created_by, title, description, time_limit_mins, pass_score, is_active)
SELECT @algo_heap, 1,
       'Heap Sort – Intermediate to Advanced',
       'Test heap construction and extraction logic.',
       15, 70, TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM quizzes
    WHERE algorithm_id = @algo_heap
      AND title = 'Heap Sort – Intermediate to Advanced'
);

-- Get quiz id safely
SELECT quiz_id INTO @heap_qid
FROM quizzes
WHERE algorithm_id = @algo_heap
  AND title = 'Heap Sort – Intermediate to Advanced'
LIMIT 1;

-- -----------------------------------------------------------------------------
-- INSERT QUESTIONS (Heap Sort)
-- -----------------------------------------------------------------------------

INSERT INTO quiz_questions
(quiz_id, question_type, question_text, option_a, option_b, option_c, option_d,
 correct_option, difficulty, explanation, order_index, is_active, xp_reward)
VALUES
(@heap_qid, 'MCQ',
 'What is the time complexity of building a max heap?',
 'O(n log n)', 'O(n)', 'O(log n)', 'O(n²)',
 'B', 'medium',
 'Bottom-up heapify runs in O(n).',
 1, TRUE, 20),

(@heap_qid, 'MCQ',
 'What element is always at the root of a max heap?',
 'Smallest', 'Median', 'Largest', 'Random',
 'C', 'medium',
 'Max heap keeps largest element at root.',
 2, TRUE, 20);

-- -----------------------------------------------------------------------------
-- REPEAT PATTERN FOR OTHER ALGORITHMS
-- -----------------------------------------------------------------------------

-- SELECTION SORT
SELECT algorithm_id INTO @algo_sel
FROM algorithms
WHERE LOWER(name) = 'selection sort'
LIMIT 1;

INSERT INTO quizzes (algorithm_id, created_by, title, description, time_limit_mins, pass_score, is_active)
SELECT @algo_sel, 1,
       'Selection Sort – Intermediate to Advanced',
       'Test selection sort behavior.',
       15, 70, TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM quizzes
    WHERE algorithm_id = @algo_sel
      AND title = 'Selection Sort – Intermediate to Advanced'
);

SELECT quiz_id INTO @sel_qid
FROM quizzes
WHERE algorithm_id = @algo_sel
  AND title = 'Selection Sort – Intermediate to Advanced'
LIMIT 1;

INSERT INTO quiz_questions
(quiz_id, question_type, question_text, option_a, option_b, option_c, option_d,
 correct_option, difficulty, explanation, order_index, is_active, xp_reward)
VALUES
(@sel_qid, 'MCQ',
 'What is the worst-case number of swaps?',
 'O(n²)', 'n-1', 'log n', 'n',
 'B', 'medium',
 'Selection sort does at most n-1 swaps.',
 1, TRUE, 20);

-- -----------------------------------------------------------------------------
-- COMMIT
-- -----------------------------------------------------------------------------

COMMIT;