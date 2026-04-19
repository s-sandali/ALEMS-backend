-- =============================================================================
-- Migration: V010__add_algorithm_badges.sql (FIXED)
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Step 1: Add column if it doesn't exist
-- -----------------------------------------------------------------------------
SET @c1 = (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'badges' AND COLUMN_NAME = 'algorithm_id');
SET @s1 = IF(@c1 = 0, "ALTER TABLE badges ADD COLUMN algorithm_id INT NULL COMMENT 'NULL = XP badge; set = awarded when user passes a quiz for this algorithm'", 'SELECT 1');
PREPARE stmt FROM @s1; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- -----------------------------------------------------------------------------
-- Step 2: Add FK constraint only if it doesn't already exist
-- -----------------------------------------------------------------------------
SET @fk_exists = (
    SELECT COUNT(*)
    FROM information_schema.TABLE_CONSTRAINTS
    WHERE CONSTRAINT_SCHEMA = DATABASE()
      AND TABLE_NAME = 'badges'
      AND CONSTRAINT_NAME = 'fk_badges_algorithm'
);

SET @sql = IF(@fk_exists = 0,
    'ALTER TABLE badges
     ADD CONSTRAINT fk_badges_algorithm
     FOREIGN KEY (algorithm_id)
     REFERENCES algorithms (algorithm_id)
     ON DELETE RESTRICT
     ON UPDATE CASCADE',
    'SELECT "FK already exists"'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- -----------------------------------------------------------------------------
-- Seed: one badge per algorithm
-- -----------------------------------------------------------------------------

INSERT INTO badges (badge_name, badge_description, xp_threshold, icon_type, icon_color, unlock_hint, algorithm_id)
SELECT
    'Bubble Sort Champ',
    'Passed a Bubble Sort quiz.',
    0,
    'gauge',
    '#c8ff3e',
    'Pass a Bubble Sort quiz to unlock',
    (SELECT algorithm_id FROM algorithms WHERE LOWER(name) = 'bubble sort' LIMIT 1)
WHERE EXISTS (SELECT 1 FROM algorithms WHERE LOWER(name) = 'bubble sort')
  AND NOT EXISTS (SELECT 1 FROM badges WHERE badge_name = 'Bubble Sort Champ');


INSERT INTO badges (badge_name, badge_description, xp_threshold, icon_type, icon_color, unlock_hint, algorithm_id)
SELECT
    'Binary Search Genius',
    'Passed a Binary Search quiz.',
    0,
    'bolt',
    '#00ffff',
    'Pass a Binary Search quiz to unlock',
    (SELECT algorithm_id FROM algorithms WHERE LOWER(name) = 'binary search' LIMIT 1)
WHERE EXISTS (SELECT 1 FROM algorithms WHERE LOWER(name) = 'binary search')
  AND NOT EXISTS (SELECT 1 FROM badges WHERE badge_name = 'Binary Search Genius');


INSERT INTO badges (badge_name, badge_description, xp_threshold, icon_type, icon_color, unlock_hint, algorithm_id)
SELECT
    'Quick Sort Ace',
    'Passed a Quick Sort quiz.',
    0,
    'flame',
    '#ff6b3e',
    'Pass a Quick Sort quiz to unlock',
    (SELECT algorithm_id FROM algorithms WHERE LOWER(name) = 'quick sort' LIMIT 1)
WHERE EXISTS (SELECT 1 FROM algorithms WHERE LOWER(name) = 'quick sort')
  AND NOT EXISTS (SELECT 1 FROM badges WHERE badge_name = 'Quick Sort Ace');


INSERT INTO badges (badge_name, badge_description, xp_threshold, icon_type, icon_color, unlock_hint, algorithm_id)
SELECT
    'Merge Sort Master',
    'Passed a Merge Sort quiz.',
    0,
    'trophy',
    '#ffd700',
    'Pass a Merge Sort quiz to unlock',
    (SELECT algorithm_id FROM algorithms WHERE LOWER(name) = 'merge sort' LIMIT 1)
WHERE EXISTS (SELECT 1 FROM algorithms WHERE LOWER(name) = 'merge sort')
  AND NOT EXISTS (SELECT 1 FROM badges WHERE badge_name = 'Merge Sort Master');


INSERT INTO badges (badge_name, badge_description, xp_threshold, icon_type, icon_color, unlock_hint, algorithm_id)
SELECT
    'Insertion Sort Pro',
    'Passed an Insertion Sort quiz.',
    0,
    'calendar',
    '#a78bfa',
    'Pass an Insertion Sort quiz to unlock',
    (SELECT algorithm_id FROM algorithms WHERE LOWER(name) = 'insertion sort' LIMIT 1)
WHERE EXISTS (SELECT 1 FROM algorithms WHERE LOWER(name) = 'insertion sort')
  AND NOT EXISTS (SELECT 1 FROM badges WHERE badge_name = 'Insertion Sort Pro');


INSERT INTO badges (badge_name, badge_description, xp_threshold, icon_type, icon_color, unlock_hint, algorithm_id)
SELECT
    'Selection Sort Expert',
    'Passed a Selection Sort quiz.',
    0,
    'shield',
    '#60a5fa',
    'Pass a Selection Sort quiz to unlock',
    (SELECT algorithm_id FROM algorithms WHERE LOWER(name) = 'selection sort' LIMIT 1)
WHERE EXISTS (SELECT 1 FROM algorithms WHERE LOWER(name) = 'selection sort')
  AND NOT EXISTS (SELECT 1 FROM badges WHERE badge_name = 'Selection Sort Expert');


INSERT INTO badges (badge_name, badge_description, xp_threshold, icon_type, icon_color, unlock_hint, algorithm_id)
SELECT
    'Heap Sort Hero',
    'Passed a Heap Sort quiz.',
    0,
    'star',
    '#f87171',
    'Pass a Heap Sort quiz to unlock',
    (SELECT algorithm_id FROM algorithms WHERE LOWER(name) = 'heap sort' LIMIT 1)
WHERE EXISTS (SELECT 1 FROM algorithms WHERE LOWER(name) = 'heap sort')
  AND NOT EXISTS (SELECT 1 FROM badges WHERE badge_name = 'Heap Sort Hero');