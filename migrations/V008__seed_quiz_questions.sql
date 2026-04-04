-- =============================================================================
-- SAFE VERSION: V008__seed_quiz_questions.sql
-- =============================================================================

START TRANSACTION;

-- -----------------------------------------------------------------------------
-- ENSURE Binary Search EXISTS
-- -----------------------------------------------------------------------------
INSERT INTO algorithms (name, category, description, time_complexity_best, time_complexity_average, time_complexity_worst, created_at)
SELECT 'Binary Search', 'Searching',
       'Repeatedly halves the search space on a sorted array to locate a target value.',
       'O(1)', 'O(log n)', 'O(log n)', CURRENT_TIMESTAMP
WHERE NOT EXISTS (
    SELECT 1 FROM algorithms WHERE LOWER(name) = 'binary search'
);

-- =============================================================================
-- HELPER FUNCTION PATTERN (REUSED BELOW)
-- =============================================================================
-- 1. Get algorithm_id
-- 2. Insert quiz if not exists
-- 3. Get quiz_id
-- 4. Insert questions

-- =============================================================================
-- MERGE SORT
-- =============================================================================

SELECT algorithm_id INTO @algo_merge
FROM algorithms WHERE LOWER(name) = 'merge sort' LIMIT 1;

INSERT INTO quizzes (algorithm_id, created_by, title, description, time_limit_mins, pass_score, is_active)
SELECT @algo_merge, 1,
       'Merge Sort – Intermediate & Advanced',
       'Test your understanding of merge sort mechanics.',
       20, 70, TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM quizzes
    WHERE algorithm_id = @algo_merge
      AND title = 'Merge Sort – Intermediate & Advanced'
);

SELECT quiz_id INTO @merge_qid
FROM quizzes
WHERE algorithm_id = @algo_merge
  AND title = 'Merge Sort – Intermediate & Advanced'
LIMIT 1;

INSERT INTO quiz_questions
(quiz_id, question_type, question_text, option_a, option_b, option_c, option_d,
 correct_option, difficulty, explanation, order_index, is_active, xp_reward)
VALUES
(@merge_qid, 'MCQ',
 'What is the auxiliary space complexity of merge sort?',
 'O(1)','O(log n)','O(n)','O(n log n)',
 'C','medium',
 'Merge sort requires O(n) extra space.',
 1, TRUE, 20);

-- =============================================================================
-- HEAP SORT
-- =============================================================================

SELECT algorithm_id INTO @algo_heap
FROM algorithms WHERE LOWER(name) = 'heap sort' LIMIT 1;

INSERT INTO quizzes (algorithm_id, created_by, title, description, time_limit_mins, pass_score, is_active)
SELECT @algo_heap, 1,
       'Heap Sort – Intermediate & Advanced',
       'Heap construction and extraction.',
       20, 70, TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM quizzes
    WHERE algorithm_id = @algo_heap
      AND title = 'Heap Sort – Intermediate & Advanced'
);

SELECT quiz_id INTO @heap_qid
FROM quizzes
WHERE algorithm_id = @algo_heap
  AND title = 'Heap Sort – Intermediate & Advanced'
LIMIT 1;

INSERT INTO quiz_questions VALUES
(@heap_qid,'MCQ','Root of max heap?','Min','Median','Max','Random','C','medium','Root is max.',1,TRUE,20);

-- =============================================================================
-- SELECTION SORT
-- =============================================================================

SELECT algorithm_id INTO @algo_sel
FROM algorithms WHERE LOWER(name) = 'selection sort' LIMIT 1;

INSERT INTO quizzes
SELECT @algo_sel,1,
       'Selection Sort – Intermediate & Advanced',
       'Swap mechanics and comparisons.',
       20,70,TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM quizzes
    WHERE algorithm_id=@algo_sel
      AND title='Selection Sort – Intermediate & Advanced'
);

SELECT quiz_id INTO @sel_qid
FROM quizzes
WHERE algorithm_id=@algo_sel
  AND title='Selection Sort – Intermediate & Advanced'
LIMIT 1;

INSERT INTO quiz_questions VALUES
(@sel_qid,'MCQ','Max swaps?','n²','n-1','log n','n','B','medium','At most n-1 swaps.',1,TRUE,20);

-- =============================================================================
-- INSERTION SORT
-- =============================================================================

SELECT algorithm_id INTO @algo_ins
FROM algorithms WHERE LOWER(name)='insertion sort' LIMIT 1;

INSERT INTO quizzes
SELECT @algo_ins,1,
       'Insertion Sort – Intermediate & Advanced',
       'Insertion behavior.',
       20,70,TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM quizzes
    WHERE algorithm_id=@algo_ins
      AND title='Insertion Sort – Intermediate & Advanced'
);

SELECT quiz_id INTO @ins_qid
FROM quizzes
WHERE algorithm_id=@algo_ins
  AND title='Insertion Sort – Intermediate & Advanced'
LIMIT 1;

INSERT INTO quiz_questions VALUES
(@ins_qid,'MCQ','Best case?','O(n²)','O(n)','O(log n)','O(1)','B','medium','Already sorted.',1,TRUE,20);

-- =============================================================================
-- BINARY SEARCH
-- =============================================================================

SELECT algorithm_id INTO @algo_bs
FROM algorithms WHERE LOWER(name)='binary search' LIMIT 1;

INSERT INTO quizzes
SELECT @algo_bs,1,
       'Binary Search – Intermediate & Advanced',
       'Binary search logic.',
       20,70,TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM quizzes
    WHERE algorithm_id=@algo_bs
      AND title='Binary Search – Intermediate & Advanced'
);

SELECT quiz_id INTO @bs_qid
FROM quizzes
WHERE algorithm_id=@algo_bs
  AND title='Binary Search – Intermediate & Advanced'
LIMIT 1;

INSERT INTO quiz_questions VALUES
(@bs_qid,'MCQ','Max comparisons for 1000?','500','100','10','1000','C','medium','log2(1000)≈10.',1,TRUE,20);

-- -----------------------------------------------------------------------------
COMMIT;