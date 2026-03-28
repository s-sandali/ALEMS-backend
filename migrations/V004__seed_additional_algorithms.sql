-- =============================================================================
-- Migration: V004__seed_additional_algorithms.sql
-- Feature:   Expand algorithm catalog with additional sorting algorithms
-- Author:    BigO Dev Team
-- Date:      2026-03-28
-- =============================================================================
-- Adds descriptive-only algorithm records for frontend/backend catalog flows.
-- No simulation engines are introduced in this migration.
-- Idempotent: each insert runs only when the algorithm name is not already present.
-- =============================================================================

INSERT INTO algorithms (
    name,
    category,
    description,
    time_complexity_best,
    time_complexity_average,
    time_complexity_worst,
    created_at
)
SELECT
    'Quick Sort',
    'Sorting',
    'Partitions the array around a pivot, then recursively sorts the left and right partitions.',
    'O(n log n)',
    'O(n log n)',
    'O(n^2)',
    CURRENT_TIMESTAMP
WHERE NOT EXISTS (
    SELECT 1
    FROM algorithms
    WHERE LOWER(name) = 'quick sort'
);

INSERT INTO algorithms (
    name,
    category,
    description,
    time_complexity_best,
    time_complexity_average,
    time_complexity_worst,
    created_at
)
SELECT
    'Merge Sort',
    'Sorting',
    'Recursively splits the array into halves, sorts each half, and merges the sorted results.',
    'O(n log n)',
    'O(n log n)',
    'O(n log n)',
    CURRENT_TIMESTAMP
WHERE NOT EXISTS (
    SELECT 1
    FROM algorithms
    WHERE LOWER(name) = 'merge sort'
);

INSERT INTO algorithms (
    name,
    category,
    description,
    time_complexity_best,
    time_complexity_average,
    time_complexity_worst,
    created_at
)
SELECT
    'Insertion Sort',
    'Sorting',
    'Builds a sorted prefix one element at a time by inserting each value into its correct position.',
    'O(n)',
    'O(n^2)',
    'O(n^2)',
    CURRENT_TIMESTAMP
WHERE NOT EXISTS (
    SELECT 1
    FROM algorithms
    WHERE LOWER(name) = 'insertion sort'
);

INSERT INTO algorithms (
    name,
    category,
    description,
    time_complexity_best,
    time_complexity_average,
    time_complexity_worst,
    created_at
)
SELECT
    'Selection Sort',
    'Sorting',
    'Repeatedly selects the smallest remaining value and swaps it into the next sorted position.',
    'O(n^2)',
    'O(n^2)',
    'O(n^2)',
    CURRENT_TIMESTAMP
WHERE NOT EXISTS (
    SELECT 1
    FROM algorithms
    WHERE LOWER(name) = 'selection sort'
);

INSERT INTO algorithms (
    name,
    category,
    description,
    time_complexity_best,
    time_complexity_average,
    time_complexity_worst,
    created_at
)
SELECT
    'Heap Sort',
    'Sorting',
    'Builds a max heap and repeatedly extracts the largest value to sort the array in place.',
    'O(n log n)',
    'O(n log n)',
    'O(n log n)',
    CURRENT_TIMESTAMP
WHERE NOT EXISTS (
    SELECT 1
    FROM algorithms
    WHERE LOWER(name) = 'heap sort'
);
